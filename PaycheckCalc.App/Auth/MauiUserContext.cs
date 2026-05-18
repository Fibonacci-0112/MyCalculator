namespace PaycheckCalc.App.Auth;

/// <summary>
/// MAUI-side equivalent of the Blazor IUserContext. Wraps
/// <see cref="AuthTokenStore"/> so consumers (e.g. the user-scoped
/// <c>JsonPaycheckRepository</c>) can ask "who's signed in?" without
/// touching SecureStorage directly. Forwards the store's <see cref="UserChanged"/>
/// event for cache invalidation.
/// </summary>
public sealed class MauiUserContext
{
    private readonly AuthTokenStore _store;

    public MauiUserContext(AuthTokenStore store)
    {
        _store = store;
        _store.UserChanged += () => UserChanged?.Invoke();
    }

    public event Action? UserChanged;

    /// <summary>
    /// Returns the signed-in user's Identity id, or null if anonymous.
    /// </summary>
    public async Task<string?> GetCurrentUserIdAsync()
    {
        var tokens = await _store.GetAsync();
        return tokens?.UserId;
    }

    public async Task<string?> GetCurrentEmailAsync()
    {
        var tokens = await _store.GetAsync();
        return tokens?.Email;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var tokens = await _store.GetAsync();
        return tokens is not null && !string.IsNullOrEmpty(tokens.AccessToken);
    }
}
