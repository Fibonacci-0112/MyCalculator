using Blazored.LocalStorage;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.Web.Storage;

/// <summary>
/// <see cref="IPaycheckRepository"/> implementation backed by browser <c>localStorage</c>
/// via <see cref="ILocalStorageService"/> (Blazored.LocalStorage).
/// Serializes to/from JSON using <see cref="System.Text.Json"/>.
/// </summary>
public sealed class LocalStoragePaycheckRepository : IPaycheckRepository
{
    private const string StorageKey = "paycheckcalc_saved_paychecks";

    private readonly ILocalStorageService _storage;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public LocalStoragePaycheckRepository(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        var json = await _storage.GetItemAsStringAsync(StorageKey);
        if (string.IsNullOrEmpty(json))
            return Array.Empty<SavedPaycheck>();

        return JsonSerializer.Deserialize<List<SavedPaycheck>>(json, JsonOptions)
               ?? [];
    }

    public async Task<SavedPaycheck?> GetByIdAsync(Guid id)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(p => p.Id == id);
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        var all = new List<SavedPaycheck>(await GetAllAsync());

        var existing = all.FindIndex(p => p.Id == paycheck.Id);
        if (existing >= 0)
            all[existing] = paycheck;
        else
            all.Add(paycheck);

        await _storage.SetItemAsStringAsync(StorageKey, JsonSerializer.Serialize(all, JsonOptions));
    }

    public async Task DeleteAsync(Guid id)
    {
        var all = new List<SavedPaycheck>(await GetAllAsync());
        all.RemoveAll(p => p.Id == id);
        await _storage.SetItemAsStringAsync(StorageKey, JsonSerializer.Serialize(all, JsonOptions));
    }
}
