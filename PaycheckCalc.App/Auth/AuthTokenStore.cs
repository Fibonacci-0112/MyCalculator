using Microsoft.Maui.Storage;

namespace PaycheckCalc.App.Auth;

/// <summary>
/// <see cref="SecureStorage"/>-backed wrapper for the API bearer + refresh
/// tokens, with the same defensive try/catch pattern as
/// <c>SecureStorageGoogleMapsApiKeyProvider</c> (SecureStorage isn't
/// available on unpackaged Windows desktop apps, so falls back to an
/// in-memory copy for the process lifetime).
///
/// Raises <see cref="UserChanged"/> when the stored user id changes so the
/// user-scoped <c>JsonPaycheckRepository</c> can invalidate its in-memory
/// cache (otherwise the previous user's saved paychecks would be visible
/// to the new one until app restart).
/// </summary>
public sealed class AuthTokenStore
{
    private const string AccessKey   = "pccalc.auth.access";
    private const string RefreshKey  = "pccalc.auth.refresh";
    private const string ExpiresKey  = "pccalc.auth.expires_at";
    private const string UserIdKey   = "pccalc.auth.user_id";
    private const string EmailKey    = "pccalc.auth.email";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private AuthTokens? _inMemory;
    private bool _loaded;

    /// <summary>Fired after the stored user id changes (sign-in, sign-out, or refresh into a different account).</summary>
    public event Action? UserChanged;

    public async Task<AuthTokens?> GetAsync()
    {
        await EnsureLoadedAsync();
        return _inMemory;
    }

    public async Task SaveAsync(AuthTokens tokens)
    {
        await _lock.WaitAsync();
        try
        {
            var previousUserId = _inMemory?.UserId;
            _inMemory = tokens;
            _loaded = true;

            try
            {
                await SecureStorage.Default.SetAsync(AccessKey,  tokens.AccessToken);
                await SecureStorage.Default.SetAsync(RefreshKey, tokens.RefreshToken);
                await SecureStorage.Default.SetAsync(ExpiresKey, tokens.ExpiresAt.ToString("O"));
                await SecureStorage.Default.SetAsync(UserIdKey,  tokens.UserId);
                await SecureStorage.Default.SetAsync(EmailKey,   tokens.Email);
            }
            catch
            {
                // SecureStorage unavailable (e.g. unpackaged Windows) —
                // keep the in-memory copy for the process lifetime.
            }

            if (previousUserId != tokens.UserId)
                UserChanged?.Invoke();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var hadUser = _inMemory is not null;
            _inMemory = null;
            _loaded = true;

            try
            {
                SecureStorage.Default.Remove(AccessKey);
                SecureStorage.Default.Remove(RefreshKey);
                SecureStorage.Default.Remove(ExpiresKey);
                SecureStorage.Default.Remove(UserIdKey);
                SecureStorage.Default.Remove(EmailKey);
            }
            catch
            {
                // Ignore — in-memory clear already happened.
            }

            if (hadUser)
                UserChanged?.Invoke();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _lock.WaitAsync();
        try
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var access  = await SecureStorage.Default.GetAsync(AccessKey);
                var refresh = await SecureStorage.Default.GetAsync(RefreshKey);
                var expires = await SecureStorage.Default.GetAsync(ExpiresKey);
                var userId  = await SecureStorage.Default.GetAsync(UserIdKey);
                var email   = await SecureStorage.Default.GetAsync(EmailKey);

                if (!string.IsNullOrEmpty(access) &&
                    !string.IsNullOrEmpty(refresh) &&
                    !string.IsNullOrEmpty(userId) &&
                    DateTimeOffset.TryParse(expires, out var parsedExpires))
                {
                    _inMemory = new AuthTokens(access, refresh, parsedExpires, userId, email ?? "");
                }
            }
            catch
            {
                // SecureStorage unavailable — leave _inMemory null (anonymous).
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
