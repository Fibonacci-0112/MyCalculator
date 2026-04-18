using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Web.Services;

/// <summary>
/// Browser implementation of <see cref="IPaycheckRepository"/> that persists
/// the saved-paychecks list to <c>localStorage</c> under a single key.
/// <para>
/// The serialized JSON shape matches <c>JsonPaycheckRepository</c> (camelCase
/// names, <see cref="JsonStringEnumConverter"/>) so saved files are portable
/// between the MAUI heads and the web head via JSON export/import.
/// </para>
/// </summary>
public sealed class LocalStoragePaycheckRepository : IPaycheckRepository
{
    /// <summary>Name of the file produced by <see cref="JsonPaycheckRepository"/> on the desktop/mobile heads.</summary>
    public const string ExportFileName = "saved_paychecks.json";

    private const string StorageKey = "paycheckcalc.saved_paychecks";

    private readonly IJSRuntime _js;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SavedPaycheck>? _cache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public LocalStoragePaycheckRepository(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _cache!.AsReadOnly();
    }

    public async Task<SavedPaycheck?> GetByIdAsync(Guid id)
    {
        await EnsureLoadedAsync();
        return _cache!.FirstOrDefault(p => p.Id == id);
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();

            var index = _cache!.FindIndex(p => p.Id == paycheck.Id);
            if (index >= 0)
                _cache[index] = paycheck;
            else
                _cache.Add(paycheck);

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
            _cache!.RemoveAll(p => p.Id == id);
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the current saved list serialized in the same JSON shape as
    /// <c>saved_paychecks.json</c> on the desktop/mobile heads. Suitable for
    /// "Export" buttons that hand the user a portable file.
    /// </summary>
    public async Task<string> ExportJsonAsync()
    {
        await EnsureLoadedAsync();
        return JsonSerializer.Serialize(_cache, JsonOptions);
    }

    /// <summary>
    /// Replaces the saved list with the contents of <paramref name="json"/>,
    /// which must be in the <c>saved_paychecks.json</c> format. Use to import
    /// data exported from another head. Returns the number of entries imported.
    /// </summary>
    public async Task<int> ImportJsonAsync(string json)
    {
        var imported = JsonSerializer.Deserialize<List<SavedPaycheck>>(json, JsonOptions)
                       ?? new List<SavedPaycheck>();

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
            // Pre-render or storage disabled — fall back to an empty list.
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
            _cache = JsonSerializer.Deserialize<List<SavedPaycheck>>(raw, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            // Corrupted entry — start fresh, matching JsonPaycheckRepository.
            _cache = [];
        }
    }

    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
