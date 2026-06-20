using System.Security.Claims;
using Finances.Application.Common;

namespace Finances.Api.Auth;

/// <summary>Implementacion de ICurrentUser basada en los claims del HttpContext.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public bool IsAdmin => Principal?.IsInRole("Admin") ?? false;

    public string RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("There is no authenticated user..");
}
