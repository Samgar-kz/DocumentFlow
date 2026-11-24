using DocumentFlow.Application.Abstractions;
using DocumentFlow.Application.Auth;
using DocumentFlow.Application.Common;
using DocumentFlow.Application.Documents;
using DocumentFlow.Application.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentFlow.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(DirectoryService directory) : ControllerBase
{
    /// <summary>People directory, used to choose approvers.</summary>
    [HttpGet]
    public Task<IReadOnlyList<DirectoryUserDto>> Search([FromQuery] string? search, CancellationToken ct) =>
        directory.SearchAsync(search, ct);
}

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) => dashboard.GetAsync(ct);
}

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<NotificationFeed> Feed(CancellationToken ct) => notifications.GetFeedAsync(ct);

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(AdminService admin) : ControllerBase
{
    /// <summary>Organisation-wide audit trail with filters.</summary>
    [HttpGet("audit")]
    public Task<PagedResult<ActivityDto>> Audit([FromQuery] AuditQuery query, CancellationToken ct) =>
        admin.SearchAuditAsync(query, ct);

    [HttpGet("users")]
    public Task<IReadOnlyList<AdminUserDto>> Users(CancellationToken ct) => admin.ListUsersAsync(ct);

    [HttpPut("users/{id:guid}/role")]
    public Task<UserDto> ChangeRole(Guid id, ChangeRoleRequest request, CancellationToken ct) =>
        admin.ChangeRoleAsync(id, request, ct);
}
