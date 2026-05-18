using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// Annual-scenario peer to <see cref="SyncingPaycheckRepository"/>. Same
/// V1 semantics: read prefers remote with cache fallback; writes are
/// cache-first with best-effort remote push.
/// </summary>
public sealed class SyncingAnnualScenarioRepository : IAnnualScenarioRepository
{
    private readonly HttpAnnualScenarioRepository _remote;
    private readonly JsonAnnualScenarioRepository _cache;
    private readonly MauiUserContext _userContext;

    public SyncingAnnualScenarioRepository(
        HttpAnnualScenarioRepository remote,
        JsonAnnualScenarioRepository cache,
        MauiUserContext userContext)
    {
        _remote = remote;
        _cache = cache;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<SavedAnnualScenario>> GetAllAsync()
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
        catch (HttpRequestException)
        {
            return await _cache.GetByIdAsync(id);
        }
        catch (TaskCanceledException)
        {
            return await _cache.GetByIdAsync(id);
        }
    }

    public async Task SaveAsync(SavedAnnualScenario scenario)
    {
        await _cache.SaveAsync(scenario);

        if (!await _userContext.IsAuthenticatedAsync()) return;

        try
        {
            await _remote.SaveAsync(scenario);
        }
        catch (HttpRequestException)
        {
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
