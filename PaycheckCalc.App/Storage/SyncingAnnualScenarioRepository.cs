using PaycheckCalc.App.Auth;
using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// Annual-scenario peer to <see cref="SyncingPaycheckRepository"/>. Same
/// queue-backed offline retry behavior.
/// </summary>
public sealed class SyncingAnnualScenarioRepository : IAnnualScenarioRepository
{
    private readonly HttpAnnualScenarioRepository _remote;
    private readonly JsonAnnualScenarioRepository _cache;
    private readonly PendingAnnualScenarioQueue _queue;
    private readonly MauiUserContext _userContext;
    private readonly SyncStatus _status;

    public SyncingAnnualScenarioRepository(
        HttpAnnualScenarioRepository remote,
        JsonAnnualScenarioRepository cache,
        PendingAnnualScenarioQueue queue,
        MauiUserContext userContext,
        SyncStatus status)
    {
        _remote = remote;
        _cache = cache;
        _queue = queue;
        _userContext = userContext;
        _status = status;
    }

    public async Task<IReadOnlyList<SavedAnnualScenario>> GetAllAsync()
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

    public async Task<SavedAnnualScenario?> GetByIdAsync(Guid id)
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

    public async Task SaveAsync(SavedAnnualScenario scenario)
    {
        await _cache.SaveAsync(scenario);

        if (!await _userContext.IsAuthenticatedAsync()) return;

        await FlushPendingAsync();

        try
        {
            await _remote.SaveAsync(scenario);
        }
        catch (HttpRequestException) { await EnqueueSaveAsync(scenario); }
        catch (TaskCanceledException) { await EnqueueSaveAsync(scenario); }
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

    private async Task EnqueueSaveAsync(SavedAnnualScenario scenario)
    {
        await _queue.EnqueueSaveAsync(scenario);
        await UpdateStatusAsync();
    }

    private async Task EnqueueDeleteAsync(Guid id)
    {
        await _queue.EnqueueDeleteAsync(id);
        await UpdateStatusAsync();
    }

    private async Task UpdateStatusAsync()
    {
        _status.PendingScenarioOps = await _queue.CountAsync();
    }

    private async Task ReplaceCacheAsync(IReadOnlyList<SavedAnnualScenario> remote)
    {
        var existing = await _cache.GetAllAsync();
        var remoteIds = remote.Select(s => s.Id).ToHashSet();

        foreach (var local in existing.Where(s => !remoteIds.Contains(s.Id)).ToList())
            await _cache.DeleteAsync(local.Id);

        foreach (var s in remote)
            await _cache.SaveAsync(s);
    }
}
