using PaycheckCalc.App.Auth;
using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// Composes <see cref="HttpPaycheckRepository"/> with
/// <see cref="JsonPaycheckRepository"/> (user-scoped cache) and a
/// <see cref="PendingPaycheckQueue"/> (offline-write retry queue) to
/// keep the MAUI app usable offline without silently dropping writes.
///
/// Sync semantics (last-write-wins, no conflict UX):
/// <list type="bullet">
///   <item><b>Read:</b> if signed in and network reachable, fetch from
///   API and replace the local cache. On any network failure, fall
///   through to whatever the cache has.</item>
///   <item><b>Write:</b> write to cache immediately (user sees the
///   save), then drain any prior pending ops, then push this op. If
///   the push fails the op is queued; the next successful read or the
///   <see cref="ConnectivityWatcher"/> "back online" event will retry.</item>
/// </list>
/// </summary>
public sealed class SyncingPaycheckRepository : IPaycheckRepository
{
    private readonly HttpPaycheckRepository _remote;
    private readonly JsonPaycheckRepository _cache;
    private readonly PendingPaycheckQueue _queue;
    private readonly MauiUserContext _userContext;
    private readonly SyncStatus _status;

    public SyncingPaycheckRepository(
        HttpPaycheckRepository remote,
        JsonPaycheckRepository cache,
        PendingPaycheckQueue queue,
        MauiUserContext userContext,
        SyncStatus status)
    {
        _remote = remote;
        _cache = cache;
        _queue = queue;
        _userContext = userContext;
        _status = status;
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        if (!await _userContext.IsAuthenticatedAsync())
            return await _cache.GetAllAsync();

        await FlushPendingAsync();

        try
        {
            var remote = await _remote.GetAllAsync();
            await ReplaceCacheAsync(remote);
            return remote;
        }
        catch (HttpRequestException) { return await _cache.GetAllAsync(); }
        catch (TaskCanceledException) { return await _cache.GetAllAsync(); }
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
        catch (HttpRequestException) { return await _cache.GetByIdAsync(id); }
        catch (TaskCanceledException) { return await _cache.GetByIdAsync(id); }
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        await _cache.SaveAsync(paycheck);

        if (!await _userContext.IsAuthenticatedAsync()) return;

        await FlushPendingAsync();

        try
        {
            await _remote.SaveAsync(paycheck);
        }
        catch (HttpRequestException) { await EnqueueSaveAsync(paycheck); }
        catch (TaskCanceledException) { await EnqueueSaveAsync(paycheck); }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _cache.DeleteAsync(id);

        if (!await _userContext.IsAuthenticatedAsync()) return;

        await FlushPendingAsync();

        try
        {
            await _remote.DeleteAsync(id);
        }
        catch (HttpRequestException) { await EnqueueDeleteAsync(id); }
        catch (TaskCanceledException) { await EnqueueDeleteAsync(id); }
    }

    /// <summary>
    /// Drains the pending-ops queue against the API. Stops on the first
    /// network failure so the remaining ops stay queued for the next
    /// attempt. Called by <see cref="ConnectivityWatcher"/> on network
    /// restore and on every read/write before contacting the API.
    /// </summary>
    public async Task FlushPendingAsync()
    {
        var ops = await _queue.SnapshotAsync();
        foreach (var op in ops)
        {
            try
            {
                if (op.OpType == PendingOpType.Save && op.Payload is not null)
                    await _remote.SaveAsync(op.Payload);
                else if (op.OpType == PendingOpType.Delete)
                    await _remote.DeleteAsync(op.Id);

                await _queue.RemoveAsync(op.Id);
            }
            catch (HttpRequestException) { break; }
            catch (TaskCanceledException) { break; }
        }
        await UpdateStatusAsync();
    }

    private async Task EnqueueSaveAsync(SavedPaycheck paycheck)
    {
        await _queue.EnqueueSaveAsync(paycheck);
        await UpdateStatusAsync();
    }

    private async Task EnqueueDeleteAsync(Guid id)
    {
        await _queue.EnqueueDeleteAsync(id);
        await UpdateStatusAsync();
    }

    private async Task UpdateStatusAsync()
    {
        _status.PendingPaycheckOps = await _queue.CountAsync();
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
