using DocumentFlow.Application.Abstractions;
using DocumentFlow.Application.Common;
using DocumentFlow.Application.Files;
using DocumentFlow.Domain.Audit;
using DocumentFlow.Domain.Common;
using DocumentFlow.Domain.Documents;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlow.Application.Documents;

public sealed class DocumentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IFileStorage storage,
    TimeProvider clock,
    IValidator<SaveDocumentRequest> saveValidator,
    IValidator<SubmitRequest> submitValidator,
    IValidator<DecisionRequest> decisionValidator,
    IValidator<CommentRequest> commentValidator,
    IValidator<DocumentQuery> queryValidator)
{
    public async Task<PagedResult<DocumentSummaryDto>> SearchAsync(DocumentQuery query, CancellationToken ct)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);
        var me = currentUser.Id;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var documents = db.Documents.AsNoTracking();
        documents = query.Scope switch
        {
            DocumentScope.Mine => documents.Where(d => d.OwnerId == me),
            DocumentScope.AwaitingMe => documents.Where(d =>
                d.Status == DocumentStatus.InReview &&
                d.Steps.Where(s => s.Status == StepStatus.Pending).OrderBy(s => s.Order).Select(s => s.ApproverId).FirstOrDefault() == me),
            DocumentScope.Involved => documents.Where(d => d.Steps.Any(s => s.ApproverId == me)),
            _ when currentUser.IsAdmin => documents,
            _ => throw new NotPermittedException("Only administrators can list all documents."),
        };

        if (query.Status is { } status)
        {
            documents = documents.Where(d => d.Status == status);
        }

        if (query.Category is { } category)
        {
            documents = documents.Where(d => d.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            documents = documents.Where(d => d.Title.ToLower().Contains(term));
        }

        var total = await documents.CountAsync(ct);
        var rows = await documents
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.Category,
                d.Status,
                d.OwnerId,
                VersionCount = d.Versions.Count,
                CurrentApproverId = d.Status == DocumentStatus.InReview
                    ? d.Steps.Where(s => s.Status == StepStatus.Pending).OrderBy(s => s.Order).Select(s => (Guid?)s.ApproverId).FirstOrDefault()
                    : null,
                d.CreatedAt,
                LastActivityAt = d.UpdatedAt ?? d.CreatedAt,
            })
            .ToListAsync(ct);

        var users = await LoadUsersAsync(rows.Select(r => r.OwnerId).Concat(rows.Select(r => r.CurrentApproverId ?? Guid.Empty)), ct);
        var items = rows
            .Select(r => new DocumentSummaryDto(
                r.Id,
                r.Title,
                r.Category,
                r.Status,
                users.Ref(r.OwnerId),
                r.VersionCount,
                r.CurrentApproverId is { } approver ? users.Ref(approver) : null,
                r.CreatedAt,
                r.LastActivityAt))
            .ToList();

        return new PagedResult<DocumentSummaryDto>(items, page, pageSize, total);
    }

    public async Task<DocumentDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        await ToDetailAsync(await LoadVisibleAsync(id, ct), ct);

    public async Task<DocumentDetailDto> CreateAsync(SaveDocumentRequest request, UploadedFile file, CancellationToken ct)
    {
        await saveValidator.ValidateAndThrowAsync(request, ct);
        var inspected = await FileInspector.InspectAsync(file, ct);
        var now = clock.GetUtcNow();

        var document = Document.Create(currentUser.Id, request.Title, request.Description, request.Category, now);
        var stored = await StoreAsync(document.Id, inspected, ct);
        document.AddVersion(currentUser.Id, stored, "Initial version", now);
        db.Documents.Add(document);

        await SaveOrDiscardFileAsync(stored, ct);
        return await ToDetailAsync(document, ct);
    }

    public async Task<DocumentDetailDto> UpdateAsync(Guid id, SaveDocumentRequest request, CancellationToken ct)
    {
        await saveValidator.ValidateAndThrowAsync(request, ct);
        var document = await LoadVisibleAsync(id, ct);
        document.UpdateDetails(currentUser.Id, request.Title, request.Description, request.Category, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(document, ct);
    }

    public async Task<DocumentDetailDto> UploadVersionAsync(Guid id, UploadedFile file, string? comment, CancellationToken ct)
    {
        if (comment is { Length: > DocumentVersion.CommentMaxLength })
        {
            throw new ValidationException([new ValidationFailure("comment", $"Comment must be at most {DocumentVersion.CommentMaxLength} characters.")]);
        }

        var document = await LoadVisibleAsync(id, ct);
        var inspected = await FileInspector.InspectAsync(file, ct);
        var stored = await StoreAsync(document.Id, inspected, ct);

        try
        {
            document.AddVersion(currentUser.Id, stored, comment, clock.GetUtcNow());
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }

        await SaveOrDiscardFileAsync(stored, ct);
        return await ToDetailAsync(document, ct);
    }

    /// <summary>Streams a version's file. Downloads are audited because reviewers act on what they read.</summary>
    public async Task<FileDownload> DownloadAsync(Guid id, int versionNumber, CancellationToken ct)
    {
        var document = await LoadVisibleAsync(id, ct);
        var version = document.Versions.SingleOrDefault(v => v.Number == versionNumber)
            ?? throw new NotFoundException("Version", versionNumber);

        var content = await storage.OpenReadAsync(version.StorageKey, ct);
        db.AuditEntries.Add(new AuditEntry(
            currentUser.Id,
            AuditAction.VersionDownloaded,
            clock.GetUtcNow(),
            document.Id,
            document.Title,
            $"Downloaded version {version.Number} ({version.FileName})"));
        await db.SaveChangesAsync(ct);

        return new FileDownload(content, version.FileName, version.ContentType);
    }

    public async Task<DocumentDetailDto> SubmitAsync(Guid id, SubmitRequest request, CancellationToken ct)
    {
        await submitValidator.ValidateAndThrowAsync(request, ct);
        var document = await LoadVisibleAsync(id, ct);

        var existing = await db.Users.CountAsync(u => request.ApproverIds.Contains(u.Id), ct);
        if (existing != request.ApproverIds.Count)
        {
            throw new ValidationException([new ValidationFailure("approverIds", "One or more approvers do not exist.")]);
        }

        document.Submit(currentUser.Id, request.ApproverIds, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(document, ct);
    }

    public Task<DocumentDetailDto> ApproveAsync(Guid id, DecisionRequest request, CancellationToken ct) =>
        DecideAsync(id, request, (document, now) => document.Approve(currentUser.Id, request.Comment, now), ct);

    public Task<DocumentDetailDto> RejectAsync(Guid id, DecisionRequest request, CancellationToken ct) =>
        DecideAsync(id, request, (document, now) => document.Reject(currentUser.Id, request.Comment ?? string.Empty, now), ct);

    public async Task<DocumentDetailDto> WithdrawAsync(Guid id, CancellationToken ct)
    {
        var document = await LoadVisibleAsync(id, ct);
        document.Withdraw(currentUser.Id, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(document, ct);
    }

    public async Task<DocumentDetailDto> AddCommentAsync(Guid id, CommentRequest request, CancellationToken ct)
    {
        await commentValidator.ValidateAndThrowAsync(request, ct);
        var document = await LoadVisibleAsync(id, ct);
        document.AddComment(currentUser.Id, request.Body, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(document, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var document = await LoadVisibleAsync(id, ct);
        document.MarkDeleted(currentUser.Id, clock.GetUtcNow());
        var keys = document.Versions.Select(v => v.StorageKey).ToList();

        db.Documents.Remove(document);
        await db.SaveChangesAsync(ct);

        foreach (var key in keys)
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<ActivityDto>> GetActivityAsync(Guid id, CancellationToken ct)
    {
        await LoadVisibleAsync(id, ct);
        var entries = await db.AuditEntries.AsNoTracking()
            .Where(a => a.DocumentId == id)
            .OrderByDescending(a => a.OccurredAt)
            .Take(200)
            .ToListAsync(ct);

        var users = await LoadUsersAsync(entries.Select(e => e.ActorId), ct);
        return entries.Select(e => ToActivity(e, users)).ToList();
    }

    internal static ActivityDto ToActivity(AuditEntry entry, UserLookup users) =>
        new(entry.Id, entry.Action, users.Find(entry.ActorId), entry.DocumentId, entry.DocumentTitle, entry.Details, entry.OccurredAt);

    internal async Task<UserLookup> LoadUsersAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .Select(u => new UserRef(u.Id, u.FullName, u.Department))
            .ToDictionaryAsync(u => u.Id, ct);
        return new UserLookup(users);
    }

    private async Task<DocumentDetailDto> DecideAsync(
        Guid id, DecisionRequest request, Action<Document, DateTimeOffset> decide, CancellationToken ct)
    {
        await decisionValidator.ValidateAndThrowAsync(request, ct);
        var document = await LoadVisibleAsync(id, ct);
        decide(document, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(document, ct);
    }

    /// <summary>Loads a document the caller may see (owner, approver or admin); otherwise reports it as not found.</summary>
    private async Task<Document> LoadVisibleAsync(Guid id, CancellationToken ct)
    {
        var document = await db.Documents
            .Include(d => d.Versions)
            .Include(d => d.Steps)
            .Include(d => d.Comments)
            .SingleOrDefaultAsync(d => d.Id == id, ct);

        if (document is null || (!document.IsParticipant(currentUser.Id) && !currentUser.IsAdmin))
        {
            throw new NotFoundException("Document", id);
        }

        return document;
    }

    private async Task<StoredFile> StoreAsync(Guid documentId, InspectedFile file, CancellationToken ct)
    {
        var key = $"{documentId:N}/{Guid.CreateVersion7():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        using var content = new MemoryStream(file.Bytes, writable: false);
        await storage.SaveAsync(key, content, ct);
        return new StoredFile(file.FileName, file.ContentType, file.Bytes.LongLength, file.Sha256, key);
    }

    /// <summary>Commits the database change; if that fails the just-stored file is removed so no orphans remain.</summary>
    private async Task SaveOrDiscardFileAsync(StoredFile stored, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    private async Task<DocumentDetailDto> ToDetailAsync(Document d, CancellationToken ct)
    {
        var me = currentUser.Id;
        var users = await LoadUsersAsync(
            d.Versions.Select(v => v.UploadedById)
                .Concat(d.Steps.Select(s => s.ApproverId))
                .Concat(d.Comments.Select(c => c.AuthorId))
                .Append(d.OwnerId),
            ct);

        var isOwner = d.OwnerId == me;
        var current = d.CurrentStep;
        var permissions = new DocumentPermissions(
            CanEdit: isOwner && d.IsEditable,
            CanUploadVersion: isOwner && d.IsEditable,
            CanSubmit: isOwner && d.Status == DocumentStatus.Draft && d.Versions.Count > 0,
            CanWithdraw: isOwner && d.Status == DocumentStatus.InReview,
            CanDecide: d.IsCurrentApprover(me),
            CanDelete: isOwner && d.Status == DocumentStatus.Draft,
            CanComment: d.IsParticipant(me));

        return new DocumentDetailDto(
            d.Id,
            d.Title,
            d.Description,
            d.Category,
            d.Status,
            users.Ref(d.OwnerId),
            d.CreatedAt,
            d.SubmittedAt,
            d.CompletedAt,
            d.Versions.OrderByDescending(v => v.Number)
                .Select(v => new VersionDto(v.Number, v.FileName, v.ContentType, v.SizeBytes, v.Sha256, users.Ref(v.UploadedById), v.UploadedAt, v.Comment))
                .ToList(),
            d.Steps.OrderBy(s => s.Order)
                .Select(s => new StepDto(s.Order, users.Ref(s.ApproverId), s.Status, s.Comment, s.DecidedAt, s == current))
                .ToList(),
            d.Comments.OrderBy(c => c.CreatedAt)
                .Select(c => new CommentDto(c.Id, users.Ref(c.AuthorId), c.Body, c.CreatedAt))
                .ToList(),
            permissions);
    }
}

/// <summary>Resolves user ids to display references, tolerating users that no longer exist.</summary>
internal sealed class UserLookup(IReadOnlyDictionary<Guid, UserRef> users)
{
    public UserRef? Find(Guid id) => users.GetValueOrDefault(id);

    public UserRef Ref(Guid id) => users.GetValueOrDefault(id) ?? new UserRef(id, "Unknown user", string.Empty);
}
