using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// JSON-file-backed implementation of <see cref="IAnnualScenarioRepository"/>.
/// Stores all saved annual scenarios in a single JSON file under
/// <see cref="FileSystem.AppDataDirectory"/> (or a caller-supplied directory
/// for tests). Mirrors <see cref="JsonPaycheckRepository"/>.
/// </summary>
public sealed class JsonAnnualScenarioRepository : IAnnualScenarioRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SavedAnnualScenario>? _cache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonAnnualScenarioRepository(string dataDirectory)
    {
        _filePath = Path.Combine(dataDirectory, "saved_annual_scenarios.json");
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

    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null) return;

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _cache = JsonSerializer.Deserialize<List<SavedAnnualScenario>>(json, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                // Corrupted file — start fresh, matching JsonPaycheckRepository.
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
