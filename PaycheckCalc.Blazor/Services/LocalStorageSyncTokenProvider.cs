using Microsoft.JSInterop;
using PaycheckCalc.CloudSync;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// <see cref="ISyncTokenProvider"/> backed by browser localStorage via the
/// existing paycheckStorage JS interop helpers. Scoped per Blazor circuit.
/// </summary>
public sealed class LocalStorageSyncTokenProvider : ISyncTokenProvider
{
    private const string StorageKey = "paycheckcalc.syncToken";
    private readonly IJSRuntime _js;

    public LocalStorageSyncTokenProvider(IJSRuntime js) => _js = js;

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
            return await _js.InvokeAsync<string?>("paycheckStorage.get", StorageKey);
        }
        catch (InvalidOperationException)
        {
            // Prerender — JS interop not yet available.
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            await _js.InvokeVoidAsync("paycheckStorage.set", StorageKey, token);
        }
        catch (InvalidOperationException)
        {
            // Prerender — ignore; call site is always OnAfterRenderAsync.
        }
    }
}
