using PaycheckCalc.CloudSync;

namespace PaycheckCalc.App.Services;

/// <summary>
/// <see cref="ISyncTokenProvider"/> backed by MAUI SecureStorage on Android
/// and an in-memory fallback on platforms where SecureStorage is unavailable.
/// </summary>
public sealed class SecureStorageSyncTokenProvider : ISyncTokenProvider
{
    internal const string StorageKey = "CloudSyncToken";
    private string? _inMemory;

    public async Task<string> GetOrCreateTokenAsync()
    {
        var existing = await GetTokenAsync();
        if (existing is not null) return existing;
        var token = Guid.NewGuid().ToString();
        await SetTokenAsync(token);
        return token;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var stored = await Microsoft.Maui.Storage.SecureStorage.Default
                .GetAsync(StorageKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stored)) return stored;
        }
        catch (Exception)
        {
            // SecureStorage unavailable on some platforms/configurations.
        }
        return _inMemory;
    }

    public async Task SetTokenAsync(string token)
    {
        _inMemory = token;
        try
        {
            await Microsoft.Maui.Storage.SecureStorage.Default
                .SetAsync(StorageKey, token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // In-memory fallback captured above.
        }
    }
}
