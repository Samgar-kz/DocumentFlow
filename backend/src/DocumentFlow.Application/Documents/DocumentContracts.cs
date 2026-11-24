using DocumentFlow.Domain.Audit;
using DocumentFlow.Domain.Documents;
using FluentValidation;

namespace DocumentFlow.Application.Documents;

public sealed record UserRef(Guid Id, string FullName, string Department);

public sealed record DocumentSummaryDto(
    Guid Id,
    string Title,
    DocumentCategory Category,
    DocumentStatus Status,
    UserRef Owner,
    int VersionCount,
    UserRef? CurrentApprover,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);

public sealed record VersionDto(
    int Number,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    UserRef UploadedBy,
    DateTimeOffset UploadedAt,
    string? Comment);

public sealed record StepDto(int Order, UserRef Approver, StepStatus Status, string? Comment, DateTimeOffset? DecidedAt, bool IsCurrent);

public sealed record CommentDto(Guid Id, UserRef Author, string Body, DateTimeOffset CreatedAt);

/// <summary>What the caller may do with the document, computed server-side so the UI never re-implements the rules.</summary>
public sealed record DocumentPermissions(
    bool CanEdit,
    bool CanUploadVersion,
    bool CanSubmit,
    bool CanWithdraw,
    bool CanDecide,
    bool CanDelete,
    bool CanComment);

public sealed record DocumentDetailDto(
    Guid Id,
    string Title,
    string? Description,
    DocumentCategory Category,
    DocumentStatus Status,
    UserRef Owner,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<VersionDto> Versions,
    IReadOnlyList<StepDto> Steps,
    IReadOnlyList<CommentDto> Comments,
    DocumentPermissions Permissions);

public sealed record ActivityDto(
    Guid Id,
    AuditAction Action,
    UserRef? Actor,
    Guid? DocumentId,
    string? DocumentTitle,
    string Details,
    DateTimeOffset OccurredAt);

public enum DocumentScope
{
    /// <summary>Documents the caller owns.</summary>
    Mine = 0,

    /// <summary>Documents whose current step is assigned to the caller.</summary>
    AwaitingMe = 1,

    /// <summary>Documents where the caller is on the approval route.</summary>
    Involved = 2,

    /// <summary>Every document (administrators only).</summary>
    All = 3,
}

public sealed class DocumentQuery
{
    public DocumentScope Scope { get; init; } = DocumentScope.Mine;

    public DocumentStatus? Status { get; init; }

    public DocumentCategory? Category { get; init; }

    public string? Search { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}

public sealed record SaveDocumentRequest(string Title, string? Description, DocumentCategory Category);

public sealed record SubmitRequest(IReadOnlyList<Guid> ApproverIds);

public sealed record DecisionRequest(string? Comment);

public sealed record CommentRequest(string Body);

public sealed record FileDownload(Stream Content, string FileName, string ContentType);

public sealed class SaveDocumentRequestValidator : AbstractValidator<SaveDocumentRequest>
{
    public SaveDocumentRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Document.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(Document.DescriptionMaxLength);
        RuleFor(x => x.Category).IsInEnum();
    }
}

public sealed class SubmitRequestValidator : AbstractValidator<SubmitRequest>
{
    public SubmitRequestValidator()
    {
        RuleFor(x => x.ApproverIds)
            .NotNull()
            .Must(ids => ids is { Count: >= 1 and <= Document.MaxApprovers })
            .WithMessage($"Choose between 1 and {Document.MaxApprovers} approvers.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Each approver can appear only once.");
        RuleForEach(x => x.ApproverIds).NotEmpty();
    }
}

public sealed class DecisionRequestValidator : AbstractValidator<DecisionRequest>
{
    public DecisionRequestValidator() => RuleFor(x => x.Comment).MaximumLength(1000);
}

public sealed class CommentRequestValidator : AbstractValidator<CommentRequest>
{
    public CommentRequestValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(DocumentComment.BodyMaxLength);
}

public sealed class DocumentQueryValidator : AbstractValidator<DocumentQuery>
{
    public DocumentQueryValidator()
    {
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Category).IsInEnum().When(x => x.Category is not null);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}
