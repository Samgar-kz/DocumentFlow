using DocumentFlow.Application.Abstractions;
using DocumentFlow.Application.Auth;
using DocumentFlow.Application.Common;
using DocumentFlow.Application.Documents;
using DocumentFlow.Domain.Audit;
using DocumentFlow.Domain.Documents;
using DocumentFlow.Domain.Notifications;
using DocumentFlow.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlow.Application.Workspace;

public sealed record DirectoryUserDto(Guid Id, string FullName, string Email, string Department);

/// <summary>People directory used to pick approvers.</summary>
public sealed class DirectoryService(IAppDbContext db)
{
    public async Task<IReadOnlyList<DirectoryUserDto>> SearchAsync(string? search, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            users = users.Where(u => u.FullName.ToLower().Contains(term) || u.Email.Contains(term) || u.Department.ToLower().Contains(term));
        }

        return await users
            .OrderBy(u => u.FullName)
            .Take(50)
            .Select(u => new DirectoryUserDto(u.Id, u.FullName, u.Email, u.Department))
            .ToListAsync(ct);
    }
}

public sealed record NotificationDto(Guid Id, NotificationType Type, Guid DocumentId, string Message, bool IsRead, DateTimeOffset CreatedAt);

public sealed record NotificationFeed(int UnreadCount, IReadOnlyList<NotificationDto> Items);

public sealed class NotificationService(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<NotificationFeed> GetFeedAsync(CancellationToken ct)
    {
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == currentUser.Id);
        var unread = await mine.CountAsync(n => !n.IsRead, ct);
        var items = await mine
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new NotificationDto(n.Id, n.Type, n.DocumentId, n.Message, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
        return new NotificationFeed(unread, items);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == currentUser.Id, ct)
            ?? throw new NotFoundException("Notification", id);
        notification.MarkRead();
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct) =>
        await db.Notifications
            .Where(n => n.UserId == currentUser.Id && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
}

public sealed record DashboardDto(
    int AwaitingMyApproval,
    int MyDrafts,
    int MyInReview,
    int MyApprovedLast30Days,
    IReadOnlyList<DocumentSummaryDto> Awaiting,
    IReadOnlyList<DocumentSummaryDto> RecentlyUpdated,
    IReadOnlyList<ActivityDto> Activity);

public sealed class DashboardService(IAppDbContext db, ICurrentUser currentUser, DocumentService documents, TimeProvider clock)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var me = currentUser.Id;
        var since = clock.GetUtcNow().AddDays(-30);
        var mine = db.Documents.AsNoTracking().Where(d => d.OwnerId == me);

        var awaiting = await documents.SearchAsync(new DocumentQuery { Scope = DocumentScope.AwaitingMe, PageSize = 5 }, ct);
        var recent = await documents.SearchAsync(new DocumentQuery { Scope = DocumentScope.Mine, PageSize = 5 }, ct);

        var involvedDocumentIds = db.Documents
            .Where(d => d.OwnerId == me || d.Steps.Any(s => s.ApproverId == me))
            .Select(d => (Guid?)d.Id);
        var entries = await db.AuditEntries.AsNoTracking()
            .Where(a => involvedDocumentIds.Contains(a.DocumentId) && a.Action != AuditAction.VersionDownloaded)
            .OrderByDescending(a => a.OccurredAt)
            .Take(10)
            .ToListAsync(ct);
        var users = await documents.LoadUsersAsync(entries.Select(e => e.ActorId), ct);

        return new DashboardDto(
            awaiting.TotalCount,
            await mine.CountAsync(d => d.Status == DocumentStatus.Draft, ct),
            await mine.CountAsync(d => d.Status == DocumentStatus.InReview, ct),
            await mine.CountAsync(d => d.Status == DocumentStatus.Approved && d.CompletedAt >= since, ct),
            awaiting.Items,
            recent.Items,
            entries.Select(e => DocumentService.ToActivity(e, users)).ToList());
    }
}

public sealed class AuditQuery
{
    public AuditAction? Action { get; init; }

    public Guid? ActorId { get; init; }

    public Guid? DocumentId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}

public sealed record AdminUserDto(Guid Id, string Email, string FullName, string Department, UserRole Role, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);

public sealed record ChangeRoleRequest(UserRole Role);

public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator() => RuleFor(x => x.Role).IsInEnum();
}

/// <summary>Administration: the organisation-wide audit trail and user roles.</summary>
public sealed class AdminService(
    IAppDbContext db,
    ICurrentUser currentUser,
    DocumentService documents,
    TimeProvider clock,
    IValidator<ChangeRoleRequest> roleValidator)
{
    public async Task<PagedResult<ActivityDto>> SearchAuditAsync(AuditQuery query, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var entries = db.AuditEntries.AsNoTracking();

        if (query.Action is { } action)
        {
            entries = entries.Where(a => a.Action == action);
        }

        if (query.ActorId is { } actor)
        {
            entries = entries.Where(a => a.ActorId == actor);
        }

        if (query.DocumentId is { } document)
        {
            entries = entries.Where(a => a.DocumentId == document);
        }

        if (query.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(a => a.OccurredAt >= start);
        }

        if (query.To is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(a => a.OccurredAt < end);
        }

        var total = await entries.CountAsync(ct);
        var rows = await entries
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var users = await documents.LoadUsersAsync(rows.Select(r => r.ActorId), ct);

        return new PagedResult<ActivityDto>(rows.Select(r => DocumentService.ToActivity(r, users)).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<AdminUserDto>> ListUsersAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new AdminUserDto(u.Id, u.Email, u.FullName, u.Department, u.Role, u.CreatedAt, u.LastLoginAt))
            .ToListAsync(ct);

    public async Task<UserDto> ChangeRoleAsync(Guid userId, ChangeRoleRequest request, CancellationToken ct)
    {
        await roleValidator.ValidateAndThrowAsync(request, ct);
        if (userId == currentUser.Id)
        {
            throw new ConflictException("You cannot change your own role.");
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User", userId);
        var previous = user.Role;
        user.ChangeRole(request.Role);
        db.AuditEntries.Add(new AuditEntry(
            currentUser.Id, AuditAction.UserRoleChanged, clock.GetUtcNow(), null, null,
            $"Changed role of {user.FullName} ({user.Email}) from {previous} to {request.Role}"));
        await db.SaveChangesAsync(ct);
        return AuthService.ToDto(user);
    }
}
