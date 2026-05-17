namespace PaycheckCalc.Blazor.Auth;

/// <summary>
/// Resolves the current authenticated user id. Two implementations exist —
/// <see cref="CircuitUserContext"/> for Blazor Server scoped components
/// (wraps <c>AuthenticationStateProvider</c>) and
/// <see cref="HttpContextUserContext"/> for minimal API endpoints (wraps
/// <c>HttpContext.User</c>). Do not inject <c>IHttpContextAccessor</c>
/// into Blazor scoped services: HttpContext is null during interactive
/// Server rendering.
/// </summary>
public interface IUserContext
{
    /// <summary>Returns the current user's ASP.NET Core Identity id, or null if anonymous.</summary>
    Task<string?> GetUserIdAsync();
}
