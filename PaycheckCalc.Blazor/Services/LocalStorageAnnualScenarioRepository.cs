using Microsoft.JSInterop;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// Browser localStorage-backed implementation of
/// <see cref="IAnnualScenarioRepository"/> for the Blazor Server head. All
/// saved annual scenarios for a user live under a single localStorage key
/// as a JSON array, mirroring <see cref="LocalStoragePaycheckRepository"/>.
/// Scoped per circuit so the in-memory cache matches the browser's view.
/// </summary>
public sealed class LocalStorageAnnualScenarioRepository : IAnnualScenarioRepository
{
    private const string StorageKey = "paycheckcalc.savedAnnualScenarios";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IJSRuntime _js;
    private List<SavedAnnualScenario>? _cache;

    public LocalStorageAnnualScenarioRepository(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<IReadOnlyList<SavedAnnualScenario>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _cache!.AsReadOnly();
    }

    public async Task<SavedAnnualScenario?> GetByIdAsync(Guid id)
    {
        await EnsureLoadedAsync();
        return _cache!.FirstOrDefault(s => s.Id == id);
    }

    public async Task SaveAsync(SavedAnnualScenario scenario)
    {
        await EnsureLoadedAsync();
        var index = _cache!.FindIndex(s => s.Id == scenario.Id);
        if (index >= 0)
            _cache[index] = scenario;
        else
            _cache.Add(scenario);
        await PersistAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await EnsureLoadedAsync();
        _cache!.RemoveAll(s => s.Id == id);
        await PersistAsync();
    }

    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null) return;

        string? raw;
        try
        {
            raw = await _js.InvokeAsync<string?>("paycheckStorage.get", StorageKey);
        }
        catch (InvalidOperationException)
        {
            // JS interop unavailable during prerender — start with an empty
            // cache; the next user-driven call will be after the circuit is
            // connected and will overwrite via PersistAsync if needed.
            _cache = [];
            return;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            _cache = [];
            return;
        }

        try
        {
            _cache = JsonSerializer.Deserialize<List<SavedAnnualScenario>>(raw, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            _cache = [];
        }
    }

    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        await _js.InvokeVoidAsync("paycheckStorage.set", StorageKey, json);
    }
}
