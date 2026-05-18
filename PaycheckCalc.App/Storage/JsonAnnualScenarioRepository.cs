using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// JSON-file-backed implementation of <see cref="IAnnualScenarioRepository"/>.
/// Per-user file at <c>{baseDirectory}/users/{userId}/saved_annual_scenarios.json</c>.
/// Peer to <see cref="JsonPaycheckRepository"/>.
/// </summary>
public sealed class JsonAnnualScenarioRepository : IAnnualScenarioRepository
{
    private readonly string _baseDirectory;
    private readonly MauiUserContext _userContext;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SavedAnnualScenario>? _cache;
    private string? _cachedUserId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonAnnualScenarioRepository(string baseDirectory, MauiUserContext userContext)
    {
        _baseDirectory = baseDirectory;
        _userContext = userContext;
        _userContext.UserChanged += () => _cache = null;
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
            await EnsureLoadedAsync_NoLock();
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
            await EnsureLoadedAsync_NoLock();
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
        await _lock.WaitAsync();
        try { await EnsureLoadedAsync_NoLock(); }
        finally { _lock.Release(); }
    }

    private async Task EnsureLoadedAsync_NoLock()
    {
        var currentUserId = await _userContext.GetCurrentUserIdAsync();
        if (_cache is not null && _cachedUserId == currentUserId) return;
        _cachedUserId = currentUserId;

        var filePath = GetFilePath(currentUserId);
        if (File.Exists(filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                _cache = JsonSerializer.Deserialize<List<SavedAnnualScenario>>(json, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
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
        var filePath = GetFilePath(_cachedUserId);
        var directory = Path.GetDirectoryName(filePath);
        if (directory is not null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private string GetFilePath(string? userId)
    {
        var folder = SanitizeUserFolder(userId);
        return Path.Combine(_baseDirectory, "users", folder, "saved_annual_scenarios.json");
    }

    private static string SanitizeUserFolder(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return "anonymous";
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(userId.Select(c => invalid.Contains(c) || c == '@' ? '_' : c));
        return sanitized;
    }
}
