using DocumentFlow.Domain.Common;

namespace DocumentFlow.Domain.Audit;

public enum AuditAction
{
    DocumentCreated = 0,
    DetailsUpdated = 1,
    VersionUploaded = 2,
    VersionDownloaded = 3,
    Submitted = 4,
    StepApproved = 5,
    Approved = 6,
    Rejected = 7,
    Withdrawn = 8,
    CommentAdded = 9,
    Deleted = 10,
    UserRoleChanged = 11,
}

/// <summary>
/// Append-only record of who did what and when. There is deliberately no foreign key to the
/// document and the title is copied, so the trail survives even when a draft is deleted.
/// </summary>
public sealed class AuditEntry : Entity
{
    public const int DetailsMaxLength = 1000;

    private AuditEntry()
    {
        // Required by EF Core.
    }

    public AuditEntry(Guid actorId, AuditAction action, DateTimeOffset occurredAt, Guid? documentId, string? documentTitle, string details)
    {
        ActorId = actorId;
        Action = action;
        OccurredAt = occurredAt;
        DocumentId = documentId;
        DocumentTitle = documentTitle;
        Details = details.Length > DetailsMaxLength ? details[..DetailsMaxLength] : details;
    }

    public Guid ActorId { get; private set; }

    public AuditAction Action { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? DocumentId { get; private set; }

    public string? DocumentTitle { get; private set; }

    public string Details { get; private set; } = string.Empty;
}
