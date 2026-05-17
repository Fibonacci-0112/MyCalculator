using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PaycheckCalc.Blazor.Auth;

/// <summary>
/// <see cref="IUserContext"/> implementation for Blazor Server scoped
/// services and components. Resolves the user via the circuit's
/// <see cref="AuthenticationStateProvider"/>, which works correctly during
/// both initial render and subsequent interactive updates (unlike
/// <c>IHttpContextAccessor</c>, whose <c>HttpContext</c> is null for
/// interactive renders).
/// </summary>
public sealed class CircuitUserContext : IUserContext
{
    private readonly AuthenticationStateProvider _authProvider;

    public CircuitUserContext(AuthenticationStateProvider authProvider)
    {
        _authProvider = authProvider;
    }

    public async Task<string?> GetUserIdAsync()
    {
        var state = await _authProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
