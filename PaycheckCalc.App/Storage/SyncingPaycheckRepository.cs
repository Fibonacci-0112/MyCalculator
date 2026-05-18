using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// Composes <see cref="HttpPaycheckRepository"/> with <see cref="JsonPaycheckRepository"/>
/// to keep the MAUI app usable offline.
///
/// V1 semantics (last-write-wins, no retry queue):
/// <list type="bullet">
///   <item><b>Read:</b> if signed in and network reachable, fetch from API
///   and replace the local cache. On any network failure, fall through to
///   whatever the cache has.</item>
///   <item><b>Write:</b> write to the cache immediately so the user sees
///   their save. Push to the API in the background; on failure, log and
///   continue (the cache copy is the local source of truth until the next
///   successful sync).</item>
///   <item><b>Delete:</b> same shape as write.</item>
/// </list>
///
/// Phase 4 will replace this with a proper pending-operation queue that
/// flushes when connectivity returns. For Phase 3 V1, an offline write
/// that never gets a chance to push is the documented limitation.
/// </summary>
public sealed class SyncingPaycheckRepository : IPaycheckRepository
{
    private readonly HttpPaycheckRepository _remote;
    private readonly JsonPaycheckRepository _cache;
    private readonly MauiUserContext _userContext;

    public SyncingPaycheckRepository(
        HttpPaycheckRepository remote,
        JsonPaycheckRepository cache,
        MauiUserContext userContext)
    {
        _remote = remote;
        _cache = cache;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        if (!await _userContext.IsAuthenticatedAsync())
            return await _cache.GetAllAsync();

        try
        {
            var remote = await _remote.GetAllAsync();
            await ReplaceCacheAsync(remote);
            return remote;
        }
        catch (HttpRequestException)
        {
            return await _cache.GetAllAsync();
        }
        catch (TaskCanceledException)
        {
            return await _cache.GetAllAsync();
        }
    }

    public async Task<SavedPaycheck?> GetByIdAsync(Guid id)
    {
        if (!await _userContext.IsAuthenticatedAsync())
            return await _cache.GetByIdAsync(id);

        try
        {
            var remote = await _remote.GetByIdAsync(id);
            if (remote is not null) await _cache.SaveAsync(remote);
            return remote;
        }
        catch (HttpRequestException)
        {
            return await _cache.GetByIdAsync(id);
        }
        catch (TaskCanceledException)
        {
            return await _cache.GetByIdAsync(id);
        }
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        await _cache.SaveAsync(paycheck);

        if (!await _userContext.IsAuthenticatedAsync()) return;

        try
        {
            await _remote.SaveAsync(paycheck);
        }
        catch (HttpRequestException)
        {
            // V1: offline writes succeed locally; Phase 4 adds a pending queue.
        }
        catch (TaskCanceledException)
        {
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _cache.DeleteAsync(id);

        if (!await _userContext.IsAuthenticatedAsync()) return;

        try
        {
            await _remote.DeleteAsync(id);
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task ReplaceCacheAsync(IReadOnlyList<SavedPaycheck> remote)
    {
        var existing = await _cache.GetAllAsync();
        var remoteIds = remote.Select(p => p.Id).ToHashSet();

        foreach (var local in existing.Where(p => !remoteIds.Contains(p.Id)).ToList())
            await _cache.DeleteAsync(local.Id);

        foreach (var p in remote)
            await _cache.SaveAsync(p);
    }
}
