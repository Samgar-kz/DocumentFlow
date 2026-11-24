using DocumentFlow.Domain.Common;

namespace DocumentFlow.Domain.Documents;

/// <summary>Common shape of every document event: which document, who acted and when.</summary>
public abstract record DocumentEvent(Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DocumentCreated(Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record DocumentDetailsUpdated(Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record VersionUploaded(
    Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt, int VersionNumber, string FileName)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record DocumentSubmitted(
    Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt, IReadOnlyList<Guid> ApproverIds)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

/// <summary>A step became active: <see cref="ApproverId"/> must now review the document.</summary>
public sealed record ApprovalRequested(
    Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt, Guid ApproverId, int StepOrder)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record StepApproved(
    Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt, int StepOrder, string? Comment)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record DocumentApproved(Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt, Guid OwnerId)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record DocumentRejected(
    Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt, Guid OwnerId, string Comment)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record DocumentWithdrawn(Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record CommentAdded(
    Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt, IReadOnlyList<Guid> RecipientIds)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);

public sealed record DocumentDeleted(Guid DocumentId, string DocumentTitle, Guid ActorId, DateTimeOffset OccurredAt)
    : DocumentEvent(DocumentId, DocumentTitle, ActorId, OccurredAt);
