using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// Scoped service that subscribes to
/// <see cref="AuthenticationStateProvider.AuthenticationStateChanged"/>
/// for the current Blazor circuit and resets all three session-state
/// services when the authenticated user changes. This is the safety net
/// against the worst-case failure mode for a tax app — silent leakage of
/// one user's W-4 / hourly rate / state inputs into another user's view
/// when they log out and a different user logs in inside the same
/// browser tab without a full reload.
///
/// Instantiated by being injected into <c>MainLayout</c>; lifetime
/// matches the circuit's scope.
/// </summary>
public sealed class SessionStateLifecycle : IDisposable
{
    private readonly AuthenticationStateProvider _authProvider;
    private readonly CalculatorSessionState _calc;
    private readonly SelfEmploymentSessionState _se;
    private readonly AnnualTaxSessionState _annual;
    private string? _lastUserId;

    public SessionStateLifecycle(
        AuthenticationStateProvider authProvider,
        CalculatorSessionState calc,
        SelfEmploymentSessionState se,
        AnnualTaxSessionState annual)
    {
        _authProvider = authProvider;
        _calc = calc;
        _se = se;
        _annual = annual;

        // Capture the initial user id so the first event after sign-in
        // is treated as a change, not as a no-op against "no previous user".
        _lastUserId = TryGetCurrentUserId();

        _authProvider.AuthenticationStateChanged += OnAuthStateChanged;
    }

    private string? TryGetCurrentUserId()
    {
        try
        {
            var stateTask = _authProvider.GetAuthenticationStateAsync();
            if (stateTask.IsCompletedSuccessfully)
                return stateTask.Result.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            // Best-effort: if the provider isn't ready yet, treat as anonymous.
        }
        return null;
    }

    private async void OnAuthStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            var state = await task;
            var newUserId = state.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (newUserId == _lastUserId) return;

            _lastUserId = newUserId;
            _calc.Reset();
            _se.Reset();
            _annual.Reset();
        }
        catch
        {
            // Swallowing avoids crashing the circuit on an event handler
            // exception; the next sign-in/out cycle will retry.
        }
    }

    public void Dispose()
    {
        _authProvider.AuthenticationStateChanged -= OnAuthStateChanged;
    }
}
