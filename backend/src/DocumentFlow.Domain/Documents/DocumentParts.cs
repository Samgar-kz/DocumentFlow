using DocumentFlow.Domain.Common;

namespace DocumentFlow.Domain.Documents;

public enum DocumentStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3,
}

public enum DocumentCategory
{
    Contract = 0,
    Invoice = 1,
    Memo = 2,
    Policy = 3,
    Report = 4,
    Other = 5,
}

public enum StepStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Skipped = 3,
}

/// <summary>Metadata of a stored file. The content itself lives in file storage under <see cref="StorageKey"/>.</summary>
public sealed record StoredFile(string FileName, string ContentType, long SizeBytes, string Sha256, string StorageKey);

/// <summary>An immutable uploaded revision of a document's file.</summary>
public sealed class DocumentVersion : Entity
{
    public const int CommentMaxLength = 500;

    private DocumentVersion()
    {
        // Required by EF Core.
    }

    internal DocumentVersion(Guid documentId, int number, StoredFile file, Guid uploadedById, DateTimeOffset uploadedAt, string? comment)
    {
        if (comment is not null && comment.Length > CommentMaxLength)
        {
            throw new DomainException($"Version comment must be at most {CommentMaxLength} characters.");
        }

        DocumentId = documentId;
        Number = number;
        FileName = file.FileName;
        ContentType = file.ContentType;
        SizeBytes = file.SizeBytes;
        Sha256 = file.Sha256;
        StorageKey = file.StorageKey;
        UploadedById = uploadedById;
        UploadedAt = uploadedAt;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    public Guid DocumentId { get; private set; }

    public int Number { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>SHA-256 of the content, hex encoded. Lets reviewers prove which exact file they approved.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public Guid UploadedById { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public string? Comment { get; private set; }
}

/// <summary>One reviewer's position in a document's sequential approval route.</summary>
public sealed class ApprovalStep : Entity
{
    private ApprovalStep()
    {
        // Required by EF Core.
    }

    internal ApprovalStep(Guid documentId, int order, Guid approverId)
    {
        DocumentId = documentId;
        Order = order;
        ApproverId = approverId;
        Status = StepStatus.Pending;
    }

    public Guid DocumentId { get; private set; }

    public int Order { get; private set; }

    public Guid ApproverId { get; private set; }

    public StepStatus Status { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    internal void Approve(string? comment, DateTimeOffset at) => Decide(StepStatus.Approved, comment, at);

    internal void Reject(string comment, DateTimeOffset at) => Decide(StepStatus.Rejected, comment, at);

    internal void Skip() => Status = StepStatus.Skipped;

    private void Decide(StepStatus status, string? comment, DateTimeOffset at)
    {
        Status = status;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        DecidedAt = at;
    }
}

public sealed class DocumentComment : Entity
{
    public const int BodyMaxLength = 2000;

    private DocumentComment()
    {
        // Required by EF Core.
    }

    internal DocumentComment(Guid documentId, Guid authorId, string body, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > BodyMaxLength)
        {
            throw new DomainException($"Comment is required and must be at most {BodyMaxLength} characters.");
        }

        DocumentId = documentId;
        AuthorId = authorId;
        Body = body.Trim();
        CreatedAt = createdAt;
    }

    public Guid DocumentId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
