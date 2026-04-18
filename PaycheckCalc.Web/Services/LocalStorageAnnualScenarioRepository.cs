using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Web.Services;

/// <summary>
/// Browser implementation of <see cref="IAnnualScenarioRepository"/> that
/// persists scenarios to <c>localStorage</c>. JSON shape matches
/// <c>JsonAnnualScenarioRepository</c> for cross-head portability.
/// </summary>
public sealed class LocalStorageAnnualScenarioRepository : IAnnualScenarioRepository
{
    /// <summary>Name of the file produced by <c>JsonAnnualScenarioRepository</c> on the desktop/mobile heads.</summary>
    public const string ExportFileName = "saved_annual_scenarios.json";

    private const string StorageKey = "paycheckcalc.saved_annual_scenarios";

    private readonly IJSRuntime _js;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SavedAnnualScenario>? _cache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

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
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();

            var index = _cache!.FindIndex(s => s.Id == scenario.Id);
            if (index >= 0)
                _cache[index] = scenario;
            else
                _cache.Add(scenario);

            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            _cache!.RemoveAll(s => s.Id == id);
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> ExportJsonAsync()
    {
        await EnsureLoadedAsync();
        return JsonSerializer.Serialize(_cache, JsonOptions);
    }

    public async Task<int> ImportJsonAsync(string json)
    {
        var imported = JsonSerializer.Deserialize<List<SavedAnnualScenario>>(json, JsonOptions)
                       ?? new List<SavedAnnualScenario>();

        await _lock.WaitAsync();
        try
        {
            _cache = imported;
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }

        return imported.Count;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null) return;

        string? raw;
        try
        {
            raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch
        {
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
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
