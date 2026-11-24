using DocumentFlow.Domain.Common;
using DocumentFlow.Domain.Documents;

namespace DocumentFlow.UnitTests;

public sealed class DocumentWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly Guid Legal = Guid.CreateVersion7();
    private static readonly Guid Finance = Guid.CreateVersion7();
    private static readonly Guid Outsider = Guid.CreateVersion7();

    private static StoredFile File(int n = 1) => new($"contract-v{n}.pdf", "application/pdf", 1024, new string('a', 64), $"key-{n}");

    private static Document Draft()
    {
        var document = Document.Create(Owner, "Vendor agreement", "Three-year term", DocumentCategory.Contract, Now);
        document.AddVersion(Owner, File(), null, Now);
        document.DequeueEvents();
        return document;
    }

    private static Document InReview()
    {
        var document = Draft();
        document.Submit(Owner, [Legal, Finance], Now);
        document.DequeueEvents();
        return document;
    }

    [Fact]
    public void Creating_a_document_starts_a_draft_and_records_the_event()
    {
        var document = Document.Create(Owner, "  Memo  ", null, DocumentCategory.Memo, Now);

        Assert.Equal(DocumentStatus.Draft, document.Status);
        Assert.Equal("Memo", document.Title);
        Assert.IsType<DocumentCreated>(Assert.Single(document.PeekEvents()));
    }

    [Fact]
    public void Versions_are_numbered_sequentially()
    {
        var document = Draft();

        var second = document.AddVersion(Owner, File(2), "Fixed typos", Now);

        Assert.Equal(2, second.Number);
        Assert.Equal(second, document.LatestVersion);
        Assert.Equal("Fixed typos", second.Comment);
    }

    [Fact]
    public void Submitting_requires_a_file()
    {
        var document = Document.Create(Owner, "Empty", null, DocumentCategory.Memo, Now);

        var error = Assert.Throws<DomainException>(() => document.Submit(Owner, [Legal], Now));
        Assert.Contains("Upload a file", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Submitting_creates_an_ordered_route_and_asks_the_first_approver()
    {
        var document = Draft();

        document.Submit(Owner, [Legal, Finance], Now);

        Assert.Equal(DocumentStatus.InReview, document.Status);
        Assert.Equal([Legal, Finance], document.Steps.OrderBy(s => s.Order).Select(s => s.ApproverId));
        Assert.Equal(Legal, document.CurrentStep!.ApproverId);
        var request = Assert.Single(document.PeekEvents().OfType<ApprovalRequested>());
        Assert.Equal(Legal, request.ApproverId);
    }

    [Theory]
    [MemberData(nameof(InvalidRoutes))]
    public void Invalid_approval_routes_are_rejected(Guid[] approvers)
    {
        Assert.Throws<DomainException>(() => Draft().Submit(Owner, approvers, Now));
    }

    public static TheoryData<Guid[]> InvalidRoutes() =>
    [
        [],
        [Legal, Legal],
        [Owner],
        [.. Enumerable.Range(0, Document.MaxApprovers + 1).Select(_ => Guid.CreateVersion7())],
    ];

    [Fact]
    public void Only_the_owner_can_submit()
    {
        Assert.Throws<NotPermittedException>(() => Draft().Submit(Legal, [Finance], Now));
    }

    [Fact]
    public void Approvers_act_in_order()
    {
        var document = InReview();

        Assert.Throws<NotPermittedException>(() => document.Approve(Finance, null, Now));

        document.Approve(Legal, "Clauses OK", Now);

        Assert.Equal(DocumentStatus.InReview, document.Status);
        Assert.Equal(Finance, document.CurrentStep!.ApproverId);
        Assert.Contains(document.PeekEvents(), e => e is ApprovalRequested { ApproverId: var id } && id == Finance);
    }

    [Fact]
    public void Last_approval_completes_the_document()
    {
        var document = InReview();

        document.Approve(Legal, null, Now);
        document.Approve(Finance, null, Now.AddHours(1));

        Assert.Equal(DocumentStatus.Approved, document.Status);
        Assert.Equal(Now.AddHours(1), document.CompletedAt);
        Assert.Null(document.CurrentStep);
        Assert.All(document.Steps, s => Assert.Equal(StepStatus.Approved, s.Status));
        Assert.Single(document.PeekEvents().OfType<DocumentApproved>());
    }

    [Fact]
    public void Rejection_needs_a_reason_and_skips_remaining_steps()
    {
        var document = InReview();

        Assert.Throws<DomainException>(() => document.Reject(Legal, "  ", Now));

        document.Reject(Legal, "Missing signature page", Now);

        Assert.Equal(DocumentStatus.Rejected, document.Status);
        Assert.Equal(StepStatus.Rejected, document.Steps.Single(s => s.ApproverId == Legal).Status);
        Assert.Equal(StepStatus.Skipped, document.Steps.Single(s => s.ApproverId == Finance).Status);
        Assert.Equal("Missing signature page", Assert.Single(document.PeekEvents().OfType<DocumentRejected>()).Comment);
    }

    [Fact]
    public void Uploading_a_new_version_reopens_a_rejected_document()
    {
        var document = InReview();
        document.Reject(Legal, "Fix section 2", Now);

        document.AddVersion(Owner, File(2), "Section 2 fixed", Now);

        Assert.Equal(DocumentStatus.Draft, document.Status);
        Assert.Empty(document.Steps);
        document.Submit(Owner, [Legal], Now);
        Assert.Equal(DocumentStatus.InReview, document.Status);
    }

    [Fact]
    public void Files_cannot_change_while_in_review_or_after_approval()
    {
        var document = InReview();
        Assert.Throws<DomainException>(() => document.AddVersion(Owner, File(2), null, Now));

        document.Approve(Legal, null, Now);
        document.Approve(Finance, null, Now);
        Assert.Throws<DomainException>(() => document.AddVersion(Owner, File(2), null, Now));
        Assert.Throws<DomainException>(() => document.UpdateDetails(Owner, "New title", null, DocumentCategory.Contract, Now));
    }

    [Fact]
    public void Withdrawing_returns_the_document_to_draft()
    {
        var document = InReview();

        Assert.Throws<NotPermittedException>(() => document.Withdraw(Legal, Now));
        document.Withdraw(Owner, Now);

        Assert.Equal(DocumentStatus.Draft, document.Status);
        Assert.Empty(document.Steps);
        Assert.Null(document.SubmittedAt);
    }

    [Fact]
    public void Only_participants_can_comment_and_others_are_notified()
    {
        var document = InReview();

        Assert.Throws<NotPermittedException>(() => document.AddComment(Outsider, "Hello", Now));

        document.AddComment(Legal, "Please clarify clause 7", Now);

        var added = Assert.Single(document.PeekEvents().OfType<CommentAdded>());
        Assert.Equal(new[] { Finance, Owner }.Order(), added.RecipientIds.Order());
        Assert.Single(document.Comments);
    }

    [Fact]
    public void Only_drafts_can_be_deleted()
    {
        Assert.Throws<DomainException>(() => InReview().MarkDeleted(Owner, Now));
        Assert.Throws<NotPermittedException>(() => Draft().MarkDeleted(Legal, Now));

        var draft = Draft();
        draft.MarkDeleted(Owner, Now);
        Assert.IsType<DocumentDeleted>(Assert.Single(draft.PeekEvents()));
    }

    [Fact]
    public void Every_transition_records_last_activity()
    {
        var document = InReview();

        document.Approve(Legal, null, Now.AddHours(2));

        // Updating the aggregate row on each step is what lets its concurrency token reject racing decisions.
        Assert.Equal(Now.AddHours(2), document.UpdatedAt);
    }

    [Fact]
    public void Participants_include_owner_and_approvers_only()
    {
        var document = InReview();

        Assert.True(document.IsParticipant(Owner));
        Assert.True(document.IsParticipant(Finance));
        Assert.False(document.IsParticipant(Outsider));
        Assert.True(document.IsCurrentApprover(Legal));
        Assert.False(document.IsCurrentApprover(Finance));
    }
}
