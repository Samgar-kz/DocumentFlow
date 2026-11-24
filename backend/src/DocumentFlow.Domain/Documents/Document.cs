using DocumentFlow.Domain.Common;

namespace DocumentFlow.Domain.Documents;

/// <summary>
/// Aggregate root for a business document and its approval workflow.
/// <code>
///   Draft ──submit──▶ InReview ──last approval──▶ Approved (final)
///     ▲                  │  └──────reject──────▶ Rejected
///     └────withdraw──────┘                          │
///     ▲                                             │
///     └──────────── upload new version ◀────────────┘
/// </code>
/// Approvers review sequentially in the order chosen by the owner. Every transition raises a
/// domain event that is turned into an audit entry and notifications.
/// </summary>
public sealed class Document : Entity, IAuditable
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const int MaxApprovers = 5;

    private readonly List<DocumentVersion> _versions = [];
    private readonly List<ApprovalStep> _steps = [];
    private readonly List<DocumentComment> _comments = [];

    private Document()
    {
        // Required by EF Core.
    }

    private Document(Guid ownerId, string title, string? description, DocumentCategory category)
    {
        OwnerId = ownerId;
        SetDetails(title, description, category);
        Status = DocumentStatus.Draft;
    }

    public Guid OwnerId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DocumentCategory Category { get; private set; }

    public DocumentStatus Status { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>): prevents double approvals from racing requests.</summary>
    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyList<DocumentVersion> Versions => _versions;

    public IReadOnlyList<ApprovalStep> Steps => _steps;

    public IReadOnlyList<DocumentComment> Comments => _comments;

    public DocumentVersion? LatestVersion => _versions.MaxBy(v => v.Number);

    /// <summary>The step waiting for a decision, if the document is in review.</summary>
    public ApprovalStep? CurrentStep =>
        Status == DocumentStatus.InReview
            ? _steps.Where(s => s.Status == StepStatus.Pending).MinBy(s => s.Order)
            : null;

    public static Document Create(Guid ownerId, string title, string? description, DocumentCategory category, DateTimeOffset now)
    {
        var document = new Document(ownerId, title, description, category);
        document.Raise(new DocumentCreated(document.Id, document.Title, ownerId, now));
        return document;
    }

    /// <summary>True for the owner and for anyone on the approval route.</summary>
    public bool IsParticipant(Guid userId) => OwnerId == userId || _steps.Any(s => s.ApproverId == userId);

    public bool IsCurrentApprover(Guid userId) => CurrentStep?.ApproverId == userId;

    public bool IsEditable => Status is DocumentStatus.Draft or DocumentStatus.Rejected;

    public void UpdateDetails(Guid actorId, string title, string? description, DocumentCategory category, DateTimeOffset now)
    {
        EnsureOwner(actorId, "edit");
        if (!IsEditable)
        {
            throw new DomainException("Only draft or rejected documents can be edited.");
        }

        SetDetails(title, description, category);
        Touch(now);
        Raise(new DocumentDetailsUpdated(Id, Title, actorId, now));
    }

    /// <summary>
    /// Adds a new file revision. Uploading to a rejected document reopens it as a draft so it can
    /// be revised and resubmitted.
    /// </summary>
    public DocumentVersion AddVersion(Guid actorId, StoredFile file, string? comment, DateTimeOffset now)
    {
        EnsureOwner(actorId, "upload versions of");
        switch (Status)
        {
            case DocumentStatus.InReview:
                throw new DomainException("Withdraw the document from review before uploading a new version.");
            case DocumentStatus.Approved:
                throw new DomainException("Approved documents are final and cannot be changed.");
            case DocumentStatus.Rejected:
                _steps.Clear();
                Status = DocumentStatus.Draft;
                CompletedAt = null;
                break;
        }

        var version = new DocumentVersion(Id, (LatestVersion?.Number ?? 0) + 1, file, actorId, now, comment);
        _versions.Add(version);
        Touch(now);
        Raise(new VersionUploaded(Id, Title, actorId, now, version.Number, version.FileName));
        return version;
    }

    /// <summary>Starts a sequential review by the given approvers, in order.</summary>
    public void Submit(Guid actorId, IReadOnlyList<Guid> approverIds, DateTimeOffset now)
    {
        EnsureOwner(actorId, "submit");
        if (Status != DocumentStatus.Draft)
        {
            throw new DomainException("Only draft documents can be submitted for approval.");
        }

        if (_versions.Count == 0)
        {
            throw new DomainException("Upload a file before submitting the document.");
        }

        if (approverIds.Count is 0 or > MaxApprovers)
        {
            throw new DomainException($"Choose between 1 and {MaxApprovers} approvers.");
        }

        if (approverIds.Distinct().Count() != approverIds.Count)
        {
            throw new DomainException("Each approver can appear only once in the route.");
        }

        if (approverIds.Contains(OwnerId))
        {
            throw new DomainException("You cannot approve your own document.");
        }

        _steps.Clear();
        for (var i = 0; i < approverIds.Count; i++)
        {
            _steps.Add(new ApprovalStep(Id, i + 1, approverIds[i]));
        }

        Status = DocumentStatus.InReview;
        SubmittedAt = now;
        CompletedAt = null;
        Touch(now);

        Raise(new DocumentSubmitted(Id, Title, actorId, now, approverIds));
        Raise(new ApprovalRequested(Id, Title, actorId, now, approverIds[0], 1));
    }

    public void Approve(Guid actorId, string? comment, DateTimeOffset now)
    {
        var step = RequireTurn(actorId);
        step.Approve(comment, now);
        Touch(now);
        Raise(new StepApproved(Id, Title, actorId, now, step.Order, step.Comment));

        if (CurrentStep is { } next)
        {
            Raise(new ApprovalRequested(Id, Title, actorId, now, next.ApproverId, next.Order));
            return;
        }

        Status = DocumentStatus.Approved;
        CompletedAt = now;
        Raise(new DocumentApproved(Id, Title, actorId, now, OwnerId));
    }

    public void Reject(Guid actorId, string comment, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new DomainException("Explain why the document is rejected.");
        }

        var step = RequireTurn(actorId);
        step.Reject(comment, now);
        foreach (var remaining in _steps.Where(s => s.Status == StepStatus.Pending))
        {
            remaining.Skip();
        }

        Status = DocumentStatus.Rejected;
        CompletedAt = now;
        Touch(now);
        Raise(new DocumentRejected(Id, Title, actorId, now, OwnerId, step.Comment!));
    }

    /// <summary>Pulls a document back from review so it can be changed. The audit trail keeps the history.</summary>
    public void Withdraw(Guid actorId, DateTimeOffset now)
    {
        EnsureOwner(actorId, "withdraw");
        if (Status != DocumentStatus.InReview)
        {
            throw new DomainException("Only documents in review can be withdrawn.");
        }

        _steps.Clear();
        Status = DocumentStatus.Draft;
        SubmittedAt = null;
        Touch(now);
        Raise(new DocumentWithdrawn(Id, Title, actorId, now));
    }

    public DocumentComment AddComment(Guid actorId, string body, DateTimeOffset now)
    {
        if (!IsParticipant(actorId))
        {
            throw new NotPermittedException("Only the owner and approvers can comment on this document.");
        }

        var comment = new DocumentComment(Id, actorId, body, now);
        _comments.Add(comment);
        Touch(now);

        var recipients = _steps.Select(s => s.ApproverId).Append(OwnerId).Distinct().Where(id => id != actorId).ToList();
        Raise(new CommentAdded(Id, Title, actorId, now, recipients));
        return comment;
    }

    /// <summary>Validates deletion and records it. Only drafts can be deleted, by their owner.</summary>
    public void MarkDeleted(Guid actorId, DateTimeOffset now)
    {
        EnsureOwner(actorId, "delete");
        if (Status != DocumentStatus.Draft)
        {
            throw new DomainException("Only draft documents can be deleted.");
        }

        Raise(new DocumentDeleted(Id, Title, actorId, now));
    }

    /// <summary>
    /// Marks the aggregate as changed. Besides tracking last activity, this guarantees the document
    /// row is updated on every transition, so its concurrency token rejects racing decisions.
    /// </summary>
    private void Touch(DateTimeOffset now) => UpdatedAt = now;

    private ApprovalStep RequireTurn(Guid actorId)
    {
        if (Status != DocumentStatus.InReview)
        {
            throw new DomainException("The document is not awaiting approval.");
        }

        var step = CurrentStep!;
        if (step.ApproverId != actorId)
        {
            throw new NotPermittedException("It is not your turn to review this document.");
        }

        return step;
    }

    private void EnsureOwner(Guid actorId, string action)
    {
        if (actorId != OwnerId)
        {
            throw new NotPermittedException($"Only the owner can {action} this document.");
        }
    }

    private void SetDetails(string title, string? description, DocumentCategory category)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > TitleMaxLength)
        {
            throw new DomainException($"Title is required and must be at most {TitleMaxLength} characters.");
        }

        if (description is not null && description.Length > DescriptionMaxLength)
        {
            throw new DomainException($"Description must be at most {DescriptionMaxLength} characters.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new DomainException("Unknown document category.");
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Category = category;
    }
}
