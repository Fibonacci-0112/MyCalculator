using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Storage;

public sealed record PendingAnnualScenarioOp(
    PendingOpType OpType,
    Guid Id,
    DateTimeOffset QueuedAt,
    SavedAnnualScenario? Payload);

/// <summary>
/// Annual-scenario peer to <see cref="PendingPaycheckQueue"/>.
/// </summary>
public sealed class PendingAnnualScenarioQueue
{
    private readonly string _baseDirectory;
    private readonly MauiUserContext _userContext;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<PendingAnnualScenarioOp>? _cache;
    private string? _cachedUserId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public PendingAnnualScenarioQueue(string baseDirectory, MauiUserContext userContext)
    {
        _baseDirectory = baseDirectory;
        _userContext = userContext;
        _userContext.UserChanged += () => _cache = null;
    }

    public async Task EnqueueSaveAsync(SavedAnnualScenario scenario)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync_NoLock();
            _cache!.RemoveAll(op => op.Id == scenario.Id);
            _cache.Add(new PendingAnnualScenarioOp(PendingOpType.Save, scenario.Id, DateTimeOffset.UtcNow, scenario));
            await PersistAsync();
        }
        finally { _lock.Release(); }
    }

    public async Task EnqueueDeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync_NoLock();
            _cache!.RemoveAll(op => op.Id == id);
            _cache.Add(new PendingAnnualScenarioOp(PendingOpType.Delete, id, DateTimeOffset.UtcNow, null));
            await PersistAsync();
        }
        finally { _lock.Release(); }
    }

    public async Task<int> CountAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync_NoLock();
            return _cache!.Count;
        }
        finally { _lock.Release(); }
    }

    public async Task<IReadOnlyList<PendingAnnualScenarioOp>> SnapshotAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync_NoLock();
            return _cache!.ToList().AsReadOnly();
        }
        finally { _lock.Release(); }
    }

    public async Task RemoveAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync_NoLock();
            if (_cache!.RemoveAll(op => op.Id == id) > 0)
                await PersistAsync();
        }
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
                _cache = JsonSerializer.Deserialize<List<PendingAnnualScenarioOp>>(json, JsonOptions) ?? [];
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
        return Path.Combine(_baseDirectory, "users", folder, "pending_scenario_ops.json");
    }

    private static string SanitizeUserFolder(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return "anonymous";
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(userId.Select(c => invalid.Contains(c) || c == '@' ? '_' : c));
    }
}
