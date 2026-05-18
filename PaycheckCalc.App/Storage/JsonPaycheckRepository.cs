using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// JSON-file-backed implementation of <see cref="IPaycheckRepository"/>.
/// Stores saved paychecks in a per-user JSON file under
/// <c>{baseDirectory}/users/{userId}/saved_paychecks.json</c> (or
/// <c>{baseDirectory}/anonymous/saved_paychecks.json</c> when no user is
/// signed in, e.g. for the Phase 5 importer to read pre-account data).
///
/// In Phase 3 this is wrapped by <see cref="SyncingPaycheckRepository"/>
/// to serve as the offline cache layer behind <see cref="HttpPaycheckRepository"/>.
/// The cache invalidates on <see cref="MauiUserContext.UserChanged"/> so a
/// sign-in/sign-out within one app session never leaks the previous user's
/// data into the next user's view.
/// </summary>
public sealed class JsonPaycheckRepository : IPaycheckRepository
{
    private readonly string _baseDirectory;
    private readonly MauiUserContext _userContext;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SavedPaycheck>? _cache;
    private string? _cachedUserId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonPaycheckRepository(string baseDirectory, MauiUserContext userContext)
    {
        _baseDirectory = baseDirectory;
        _userContext = userContext;
        _userContext.UserChanged += () => _cache = null;
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
            await EnsureLoadedAsync_NoLock();

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
            await EnsureLoadedAsync_NoLock();
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
                _cache = JsonSerializer.Deserialize<List<SavedPaycheck>>(json, JsonOptions) ?? [];
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
        return Path.Combine(_baseDirectory, "users", folder, "saved_paychecks.json");
    }

    /// <summary>
    /// Email addresses (which double as the user id since Identity bearer
    /// tokens are opaque — see <c>AuthApiClient</c>) contain '@', which is
    /// fine on Linux/Android but awkward on Windows file paths. Normalize
    /// to a filesystem-safe form.
    /// </summary>
    private static string SanitizeUserFolder(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return "anonymous";
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(userId.Select(c => invalid.Contains(c) || c == '@' ? '_' : c));
        return sanitized;
    }
}
