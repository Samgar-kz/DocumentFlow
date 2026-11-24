using System.Net;
using System.Text.Json;
using DocumentFlow.Application.Auth;
using DocumentFlow.Application.Common;
using DocumentFlow.Application.Documents;
using DocumentFlow.Application.Workspace;
using DocumentFlow.Domain.Audit;
using DocumentFlow.Domain.Documents;
using DocumentFlow.Domain.Users;
using DocumentFlow.IntegrationTests.Infrastructure;

namespace DocumentFlow.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class WorkflowTests(ApiFactory factory)
{
    [Fact]
    public async Task Document_goes_through_a_two_step_approval()
    {
        var (owner, _) = await factory.CreateUserAsync("Owner");
        var (legal, legalUser) = await factory.CreateUserAsync("Legal");
        var (finance, financeUser) = await factory.CreateUserAsync("Finance");

        var created = await (await owner.PostAsync("/api/documents", ApiFactory.DocumentForm("Supply contract"))).ReadAsync<DocumentDetailDto>();
        Assert.Equal(DocumentStatus.Draft, created.Status);
        Assert.Equal(1, Assert.Single(created.Versions).Number);
        Assert.True(created.Permissions.CanSubmit);

        var submitted = await (await owner.PostJsonAsync($"/api/documents/{created.Id}/submit",
            new SubmitRequest([legalUser.Id, financeUser.Id]))).ReadAsync<DocumentDetailDto>();
        Assert.Equal(DocumentStatus.InReview, submitted.Status);

        // Only the current approver sees it in their inbox and may decide.
        Assert.Contains(await Inbox(legal), d => d.Id == created.Id);
        Assert.DoesNotContain(await Inbox(finance), d => d.Id == created.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PostJsonAsync($"/api/documents/{created.Id}/approve", new DecisionRequest(null))).StatusCode);

        await (await legal.PostJsonAsync($"/api/documents/{created.Id}/approve", new DecisionRequest("Legal OK"))).ReadAsync<DocumentDetailDto>();
        Assert.Contains(await Inbox(finance), d => d.Id == created.Id);

        var approved = await (await finance.PostJsonAsync($"/api/documents/{created.Id}/approve", new DecisionRequest(null))).ReadAsync<DocumentDetailDto>();
        Assert.Equal(DocumentStatus.Approved, approved.Status);
        Assert.All(approved.Steps, s => Assert.Equal(StepStatus.Approved, s.Status));
        Assert.Equal("Legal OK", approved.Steps[0].Comment);

        var feed = await (await owner.GetAsync("/api/notifications")).ReadAsync<NotificationFeed>();
        Assert.Contains(feed.Items, n => n.DocumentId == created.Id && n.Message.Contains("approved", StringComparison.Ordinal));

        var activity = await (await owner.GetAsync($"/api/documents/{created.Id}/activity")).ReadAsync<List<ActivityDto>>();
        Assert.Equal(
            [AuditAction.Approved, AuditAction.StepApproved, AuditAction.StepApproved, AuditAction.Submitted, AuditAction.VersionUploaded, AuditAction.DocumentCreated],
            activity.Select(a => a.Action));
    }

    [Fact]
    public async Task Rejected_document_can_be_revised_and_resubmitted()
    {
        var (owner, _) = await factory.CreateUserAsync();
        var (reviewer, reviewerUser) = await factory.CreateUserAsync();
        var document = await CreateAndSubmit(owner, reviewerUser.Id);

        var noReason = await reviewer.PostJsonAsync($"/api/documents/{document.Id}/reject", new DecisionRequest(null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);

        var rejected = await (await reviewer.PostJsonAsync($"/api/documents/{document.Id}/reject",
            new DecisionRequest("Add the PO number"))).ReadAsync<DocumentDetailDto>();
        Assert.Equal(DocumentStatus.Rejected, rejected.Status);

        var ownerFeed = await (await owner.GetAsync("/api/notifications")).ReadAsync<NotificationFeed>();
        Assert.Contains(ownerFeed.Items, n => n.Message.Contains("Add the PO number", StringComparison.Ordinal));

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(ApiFactory.SamplePdf), "file", "contract-v2.pdf" },
            { new StringContent("PO number added"), "comment" },
        };
        var revised = await (await owner.PostAsync($"/api/documents/{document.Id}/versions", form)).ReadAsync<DocumentDetailDto>();
        Assert.Equal(DocumentStatus.Draft, revised.Status);
        Assert.Equal([2, 1], revised.Versions.Select(v => v.Number));

        var resubmitted = await (await owner.PostJsonAsync($"/api/documents/{document.Id}/submit",
            new SubmitRequest([reviewerUser.Id]))).ReadAsync<DocumentDetailDto>();
        Assert.Equal(DocumentStatus.InReview, resubmitted.Status);
    }

    [Fact]
    public async Task Documents_are_private_to_their_participants()
    {
        var (owner, _) = await factory.CreateUserAsync();
        var (outsider, _) = await factory.CreateUserAsync();
        var document = await (await owner.PostAsync("/api/documents", ApiFactory.DocumentForm("Salary review"))).ReadAsync<DocumentDetailDto>();

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/documents/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/documents/{document.Id}/versions/1/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/documents?scope=All")).StatusCode);
    }

    [Fact]
    public async Task Disguised_or_disallowed_files_are_rejected()
    {
        var (owner, _) = await factory.CreateUserAsync();

        var exe = await owner.PostAsync("/api/documents", ApiFactory.DocumentForm("Fake", [0x4D, 0x5A, 0x90, 0x00], "invoice.pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, exe.StatusCode);
        using var problem = JsonDocument.Parse(await exe.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("file", out _));

        var html = await owner.PostAsync("/api/documents", ApiFactory.DocumentForm("Page", "<html>"u8.ToArray(), "page.html"));
        Assert.Equal(HttpStatusCode.BadRequest, html.StatusCode);
    }

    [Fact]
    public async Task Downloads_return_the_file_and_are_audited()
    {
        var (owner, _) = await factory.CreateUserAsync();
        var document = await (await owner.PostAsync("/api/documents", ApiFactory.DocumentForm("Board pack", fileName: "board pack.pdf"))).ReadAsync<DocumentDetailDto>();

        var response = await owner.GetAsync($"/api/documents/{document.Id}/versions/1/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ApiFactory.SamplePdf, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("board pack.pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var activity = await (await owner.GetAsync($"/api/documents/{document.Id}/activity")).ReadAsync<List<ActivityDto>>();
        Assert.Equal(AuditAction.VersionDownloaded, activity[0].Action);
    }

    [Fact]
    public async Task Withdrawn_drafts_can_be_deleted_and_the_audit_trail_survives()
    {
        var (owner, ownerUser) = await factory.CreateUserAsync();
        var (reviewer, reviewerUser) = await factory.CreateUserAsync();
        var document = await CreateAndSubmit(owner, reviewerUser.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.DeleteAsync($"/api/documents/{document.Id}")).StatusCode);

        var withdrawn = await (await owner.PostAsync($"/api/documents/{document.Id}/withdraw", null)).ReadAsync<DocumentDetailDto>();
        Assert.Equal(DocumentStatus.Draft, withdrawn.Status);
        Assert.Empty(withdrawn.Steps);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync($"/api/documents/{document.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/documents/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/documents/{document.Id}")).StatusCode);

        var admin = await factory.LoginAsync(ApiFactory.AdminEmail, ApiFactory.Password);
        var trail = await (await admin.GetAsync($"/api/admin/audit?documentId={document.Id}")).ReadAsync<PagedResult<ActivityDto>>();
        Assert.Contains(trail.Items, a => a.Action == AuditAction.Deleted && a.Actor!.Id == ownerUser.Id);
        Assert.Contains(trail.Items, a => a.Action == AuditAction.Withdrawn);
    }

    [Fact]
    public async Task Comments_notify_the_other_participants()
    {
        var (owner, ownerUser) = await factory.CreateUserAsync();
        var (reviewer, reviewerUser) = await factory.CreateUserAsync();
        var document = await CreateAndSubmit(owner, reviewerUser.Id);

        var detail = await (await reviewer.PostJsonAsync($"/api/documents/{document.Id}/comments",
            new CommentRequest("Can you confirm the totals?"))).ReadAsync<DocumentDetailDto>();

        var comment = Assert.Single(detail.Comments);
        Assert.Equal(reviewerUser.Id, comment.Author.Id);

        var ownerFeed = await (await owner.GetAsync("/api/notifications")).ReadAsync<NotificationFeed>();
        Assert.Contains(ownerFeed.Items, n => n.Type == Domain.Notifications.NotificationType.CommentAdded);
        Assert.True(ownerFeed.UnreadCount >= 1);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await (await owner.GetAsync("/api/notifications")).ReadAsync<NotificationFeed>()).UnreadCount);
        Assert.NotEqual(ownerUser.Id, reviewerUser.Id);
    }

    [Fact]
    public async Task Admin_area_is_restricted_and_role_changes_are_audited()
    {
        var (member, memberUser) = await factory.CreateUserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/audit")).StatusCode);

        var admin = await factory.LoginAsync(ApiFactory.AdminEmail, ApiFactory.Password);
        var promoted = await (await admin.PutJsonAsync($"/api/admin/users/{memberUser.Id}/role", new ChangeRoleRequest(UserRole.Admin))).ReadAsync<UserDto>();
        Assert.Equal(UserRole.Admin, promoted.Role);

        var trail = await (await admin.GetAsync("/api/admin/audit?action=UserRoleChanged")).ReadAsync<PagedResult<ActivityDto>>();
        Assert.Contains(trail.Items, a => a.Details.Contains(memberUser.Email, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seeded_demo_user_has_documents_waiting_for_approval()
    {
        var demo = await factory.LoginAsync(ApiFactory.DemoEmail, ApiFactory.Password);

        var dashboard = await (await demo.GetAsync("/api/dashboard")).ReadAsync<DashboardDto>();

        Assert.Equal(2, dashboard.AwaitingMyApproval);
        Assert.Equal(1, dashboard.MyDrafts);
        Assert.NotEmpty(dashboard.Activity);

        var directory = await (await demo.GetAsync("/api/users?search=legal")).ReadAsync<List<DirectoryUserDto>>();
        Assert.Contains(directory, u => u.FullName == "Priya Shah");
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private static async Task<DocumentDetailDto> CreateAndSubmit(HttpClient owner, Guid approverId)
    {
        var document = await (await owner.PostAsync("/api/documents", ApiFactory.DocumentForm("Invoice 1001", category: "Invoice"))).ReadAsync<DocumentDetailDto>();
        return await (await owner.PostJsonAsync($"/api/documents/{document.Id}/submit", new SubmitRequest([approverId]))).ReadAsync<DocumentDetailDto>();
    }

    private static async Task<IReadOnlyList<DocumentSummaryDto>> Inbox(HttpClient client) =>
        (await (await client.GetAsync("/api/documents?scope=AwaitingMe")).ReadAsync<PagedResult<DocumentSummaryDto>>()).Items;
}
