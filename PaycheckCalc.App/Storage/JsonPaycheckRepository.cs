using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// JSON-file-backed implementation of <see cref="IPaycheckRepository"/>.
/// Stores all saved paychecks in a single JSON file under
/// <see cref="FileSystem.AppDataDirectory"/>.
/// </summary>
public sealed class JsonPaycheckRepository : IPaycheckRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SavedPaycheck>? _cache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonPaycheckRepository(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "saved_paychecks.json");
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

    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null) return;

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _cache = JsonSerializer.Deserialize<List<SavedPaycheck>>(json, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                // Corrupted file — start fresh
                _cache = [];
            }
        }
        else
        {
            _cache = [];
        }
    }

    private async Task PersistAsync()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
