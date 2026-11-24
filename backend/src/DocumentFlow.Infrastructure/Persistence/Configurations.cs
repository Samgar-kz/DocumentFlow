using DocumentFlow.Domain.Audit;
using DocumentFlow.Domain.Documents;
using DocumentFlow.Domain.Notifications;
using DocumentFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentFlow.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(254);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FullName).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.Department).HasMaxLength(User.DepartmentMaxLength);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.Title).HasMaxLength(Document.TitleMaxLength);
        builder.Property(d => d.Description).HasMaxLength(Document.DescriptionMaxLength);
        builder.Property(d => d.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Version).IsRowVersion();

        builder.Ignore(d => d.LatestVersion);
        builder.Ignore(d => d.CurrentStep);
        builder.Ignore(d => d.IsEditable);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.Steps).WithOne().HasForeignKey(s => s.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.Comments).WithOne().HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(d => d.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(d => d.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(d => new { d.OwnerId, d.Status });
        builder.HasIndex(d => d.Status);
    }
}

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.Property(v => v.FileName).HasMaxLength(200);
        builder.Property(v => v.ContentType).HasMaxLength(120);
        builder.Property(v => v.Sha256).HasMaxLength(64).IsFixedLength();
        builder.Property(v => v.StorageKey).HasMaxLength(200);
        builder.Property(v => v.Comment).HasMaxLength(DocumentVersion.CommentMaxLength);
        builder.HasIndex(v => new { v.DocumentId, v.Number }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Comment).HasMaxLength(1000);
        builder.HasIndex(s => new { s.DocumentId, s.Order }).IsUnique();
        builder.HasIndex(s => new { s.ApproverId, s.Status });
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.ApproverId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentCommentConfiguration : IEntityTypeConfiguration<DocumentComment>
{
    public void Configure(EntityTypeBuilder<DocumentComment> builder)
    {
        builder.Property(c => c.Body).HasMaxLength(DocumentComment.BodyMaxLength);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.DocumentTitle).HasMaxLength(Document.TitleMaxLength);
        builder.Property(a => a.Details).HasMaxLength(AuditEntry.DetailsMaxLength);

        // Intentionally no FK to documents: the audit trail must outlive deleted drafts.
        builder.HasIndex(a => new { a.DocumentId, a.OccurredAt });
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => a.ActorId);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.Message).HasMaxLength(300);
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
