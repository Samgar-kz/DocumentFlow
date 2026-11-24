using DocumentFlow.Application.Abstractions;
using DocumentFlow.Domain.Audit;
using DocumentFlow.Domain.Common;
using DocumentFlow.Domain.Documents;
using DocumentFlow.Domain.Notifications;
using DocumentFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DocumentFlow.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IEnumerable<IDomainEventHandler> eventHandlers)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>
    /// Dispatches pending domain events before saving, so audit entries and notifications are
    /// committed in the same transaction as the change that produced them.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        DispatchDomainEvents();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        DispatchDomainEvents();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Ids are generated in the domain (UUIDv7). Marking them as never store-generated lets EF
        // recognise new child entities added to a loaded aggregate (e.g. a new version) as inserts.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.FindProperty(nameof(Entity.Id))?.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }

    private void DispatchDomainEvents()
    {
        var events = ChangeTracker.Entries<Entity>()
            .SelectMany(entry => entry.Entity.DequeueEvents())
            .OrderBy(e => e.OccurredAt)
            .ToList();

        // Events raised by one operation (e.g. "step approved" then "document approved") share a
        // timestamp. Offsetting each by 1 µs (PostgreSQL's precision) keeps their real order when
        // the audit trail is sorted by time.
        var offset = 0;
        foreach (var domainEvent in events)
        {
            var stamped = domainEvent is DocumentEvent documentEvent
                ? documentEvent with { OccurredAt = documentEvent.OccurredAt.AddTicks(10 * offset++) }
                : domainEvent;

            foreach (var handler in eventHandlers)
            {
                handler.Handle(stamped, this);
            }
        }
    }
}

/// <summary>Stamps <see cref="IAuditable"/> timestamps (keeping explicitly set creation times, e.g. from seeding).</summary>
public sealed class AuditableEntityInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
