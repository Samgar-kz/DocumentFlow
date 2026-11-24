using DocumentFlow.Application.Common;
using DocumentFlow.Application.Documents;
using DocumentFlow.Application.Files;
using DocumentFlow.Domain.Documents;
using Microsoft.AspNetCore.Mvc;

namespace DocumentFlow.Api.Controllers;

public sealed class CreateDocumentForm
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public DocumentCategory Category { get; init; }

    public IFormFile? File { get; init; }
}

public sealed class UploadVersionForm
{
    public IFormFile? File { get; init; }

    public string? Comment { get; init; }
}

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentService documents) : ControllerBase
{
    private const long UploadLimit = 12 * 1024 * 1024;

    [HttpGet]
    public Task<PagedResult<DocumentSummaryDto>> Search([FromQuery] DocumentQuery query, CancellationToken ct) =>
        documents.SearchAsync(query, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DocumentDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<DocumentDetailDto> Get(Guid id, CancellationToken ct) => documents.GetAsync(id, ct);

    /// <summary>Creates a draft document with its first file (multipart/form-data).</summary>
    [HttpPost]
    [RequestSizeLimit(UploadLimit)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<DocumentDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromForm] CreateDocumentForm form, CancellationToken ct)
    {
        await using var content = RequireFile(form.File, out var upload);
        var document = await documents.CreateAsync(new SaveDocumentRequest(form.Title, form.Description, form.Category), upload(content), ct);
        return CreatedAtAction(nameof(Get), new { id = document.Id }, document);
    }

    [HttpPut("{id:guid}")]
    public Task<DocumentDetailDto> Update(Guid id, SaveDocumentRequest request, CancellationToken ct) =>
        documents.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/versions")]
    [RequestSizeLimit(UploadLimit)]
    [Consumes("multipart/form-data")]
    public async Task<DocumentDetailDto> UploadVersion(Guid id, [FromForm] UploadVersionForm form, CancellationToken ct)
    {
        await using var content = RequireFile(form.File, out var upload);
        return await documents.UploadVersionAsync(id, upload(content), form.Comment, ct);
    }

    [HttpGet("{id:guid}/versions/{number:int}/download")]
    public async Task<IActionResult> Download(Guid id, int number, CancellationToken ct)
    {
        var file = await documents.DownloadAsync(id, number, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/submit")]
    public Task<DocumentDetailDto> Submit(Guid id, SubmitRequest request, CancellationToken ct) =>
        documents.SubmitAsync(id, request, ct);

    [HttpPost("{id:guid}/approve")]
    public Task<DocumentDetailDto> Approve(Guid id, DecisionRequest request, CancellationToken ct) =>
        documents.ApproveAsync(id, request, ct);

    [HttpPost("{id:guid}/reject")]
    public Task<DocumentDetailDto> Reject(Guid id, DecisionRequest request, CancellationToken ct) =>
        documents.RejectAsync(id, request, ct);

    [HttpPost("{id:guid}/withdraw")]
    public Task<DocumentDetailDto> Withdraw(Guid id, CancellationToken ct) => documents.WithdrawAsync(id, ct);

    [HttpPost("{id:guid}/comments")]
    public Task<DocumentDetailDto> Comment(Guid id, CommentRequest request, CancellationToken ct) =>
        documents.AddCommentAsync(id, request, ct);

    [HttpGet("{id:guid}/activity")]
    public Task<IReadOnlyList<ActivityDto>> Activity(Guid id, CancellationToken ct) => documents.GetActivityAsync(id, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return NoContent();
    }

    private static Stream RequireFile(IFormFile? file, out Func<Stream, UploadedFile> toUpload)
    {
        if (file is null)
        {
            throw new FluentValidation.ValidationException(
                [new FluentValidation.Results.ValidationFailure("file", "A file is required.")]);
        }

        toUpload = stream => new UploadedFile(file.FileName, file.Length, stream);
        return file.OpenReadStream();
    }
}
