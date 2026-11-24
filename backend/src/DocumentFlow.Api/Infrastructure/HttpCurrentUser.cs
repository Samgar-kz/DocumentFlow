using System.Security.Claims;
using DocumentFlow.Application.Abstractions;
using DocumentFlow.Application.Common;
using DocumentFlow.Domain.Users;
using DocumentFlow.Infrastructure.Security;

namespace DocumentFlow.Api.Infrastructure;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id =>
        Guid.TryParse(Principal?.FindFirstValue(DocumentFlowClaims.Subject), out var id)
            ? id
            : throw new UnauthorizedException("Authentication is required.");

    public bool IsAdmin => Principal?.IsInRole(nameof(UserRole.Admin)) == true;

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
}
