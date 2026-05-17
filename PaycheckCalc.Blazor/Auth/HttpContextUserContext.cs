using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PaycheckCalc.Blazor.Auth;

/// <summary>
/// <see cref="IUserContext"/> implementation for minimal API endpoints.
/// <c>HttpContext.User</c> is populated by the authentication middleware
/// after the bearer token is validated, so this works correctly inside
/// any endpoint handler that has gone through <c>.RequireAuthorization()</c>.
/// </summary>
public sealed class HttpContextUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextUserContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Task<string?> GetUserIdAsync()
    {
        var userId = _accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Task.FromResult(userId);
    }
}
