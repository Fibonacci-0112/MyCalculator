using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace PaycheckCalc.Blazor.Auth;

/// <summary>
/// <see cref="IUserContext"/> implementation that works in both Blazor
/// Server scoped components AND minimal API endpoints.
///
/// Resolution order:
///   1. <see cref="AuthenticationStateProvider"/> — in an interactive Blazor
///      circuit this has the live user identity after sign-in/sign-out
///      events. In an API endpoint context the default
///      <c>ServerAuthenticationStateProvider</c> returns Anonymous and we
///      fall through.
///   2. <see cref="HttpContext.User"/> — populated by the bearer or cookie
///      auth middleware once authentication has run on the request.
///
/// We do NOT use <c>IHttpContextAccessor</c> alone for Blazor: <c>HttpContext</c>
/// is the initial request's context and is stale after sign-in/sign-out
/// events that happen later in the circuit.
/// </summary>
public sealed class CompositeUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpAccessor;
    private readonly AuthenticationStateProvider _authProvider;

    public CompositeUserContext(IHttpContextAccessor httpAccessor, AuthenticationStateProvider authProvider)
    {
        _httpAccessor = httpAccessor;
        _authProvider = authProvider;
    }

    public async Task<string?> GetUserIdAsync()
    {
        try
        {
            var state = await _authProvider.GetAuthenticationStateAsync();
            if (state.User?.Identity?.IsAuthenticated == true)
            {
                var id = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(id)) return id;
            }
        }
        catch
        {
            // AuthenticationStateProvider may throw in some non-circuit contexts.
            // Swallow and fall through to HttpContext.
        }

        var httpUser = _httpAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
            return httpUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return null;
    }
}
