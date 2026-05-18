using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// Type of a pending sync operation queued for replay against the server.
/// </summary>
public enum PendingOpType { Save, Delete }

/// <summary>
/// A single sync operation that couldn't be pushed to the server when the
/// user took the action (typically because they were offline). The queue
/// guarantees at-most-one pending op per <see cref="Id"/> — a newer op
/// for the same id replaces the older one — which gives us last-write-wins
/// semantics without ever pushing stale data.
/// </summary>
public sealed record PendingPaycheckOp(
    PendingOpType OpType,
    Guid Id,
    DateTimeOffset QueuedAt,
    SavedPaycheck? Payload);

/// <summary>
/// User-scoped JSON-file-backed queue of pending paycheck operations.
/// Path: <c>{baseDirectory}/users/{sanitizedUserId}/pending_paycheck_ops.json</c>.
///
/// Lifecycle managed by <see cref="SyncingPaycheckRepository"/> — writes
/// that fail to push enqueue here; reads (and the
/// <see cref="ConnectivityWatcher"/>) call <see cref="FlushAsync"/> to
/// drain them when the network is back.
/// </summary>
public sealed class PendingPaycheckQueue
{
    private readonly string _baseDirectory;
    private readonly MauiUserContext _userContext;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<PendingPaycheckOp>? _cache;
    private string? _cachedUserId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public PendingPaycheckQueue(string baseDirectory, MauiUserContext userContext)
    {
        _baseDirectory = baseDirectory;
        _userContext = userContext;
        _userContext.UserChanged += () => _cache = null;
    }

    public async Task EnqueueSaveAsync(SavedPaycheck paycheck)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync_NoLock();
            _cache!.RemoveAll(op => op.Id == paycheck.Id);
            _cache.Add(new PendingPaycheckOp(PendingOpType.Save, paycheck.Id, DateTimeOffset.UtcNow, paycheck));
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
            _cache.Add(new PendingPaycheckOp(PendingOpType.Delete, id, DateTimeOffset.UtcNow, null));
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

    public async Task<IReadOnlyList<PendingPaycheckOp>> SnapshotAsync()
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
                _cache = JsonSerializer.Deserialize<List<PendingPaycheckOp>>(json, JsonOptions) ?? [];
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
        return Path.Combine(_baseDirectory, "users", folder, "pending_paycheck_ops.json");
    }

    private static string SanitizeUserFolder(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return "anonymous";
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(userId.Select(c => invalid.Contains(c) || c == '@' ? '_' : c));
    }
}
