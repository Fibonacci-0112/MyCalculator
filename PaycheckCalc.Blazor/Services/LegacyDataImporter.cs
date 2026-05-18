using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// Reads the pre-account browser localStorage data
/// (<c>paycheckcalc.savedPaychecks</c> and
/// <c>paycheckcalc.savedAnnualScenarios</c>) and imports it into the
/// signed-in user's account by POSTing through the live
/// <see cref="IPaycheckRepository"/> / <see cref="IAnnualScenarioRepository"/>
/// (which the Phase 2 swap pointed at the EF repos).
///
/// Skipping this would silently strand every pre-upgrade user's saves —
/// a non-trivial trust break. The flag <c>paycheckcalc.imported-flag</c>
/// in the same localStorage namespace records when import ran so we
/// don't prompt twice. Original keys are left intact (not deleted) so
/// the user can recover via the browser dev tools if anything was lost.
/// </summary>
public sealed class LegacyDataImporter
{
    private const string PaycheckKey = "paycheckcalc.savedPaychecks";
    private const string ScenarioKey = "paycheckcalc.savedAnnualScenarios";
    private const string ImportedFlagKey = "paycheckcalc.imported-flag";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IJSRuntime _js;
    private readonly IPaycheckRepository _paycheckRepo;
    private readonly IAnnualScenarioRepository _scenarioRepo;

    public LegacyDataImporter(
        IJSRuntime js,
        IPaycheckRepository paycheckRepo,
        IAnnualScenarioRepository scenarioRepo)
    {
        _js = js;
        _paycheckRepo = paycheckRepo;
        _scenarioRepo = scenarioRepo;
    }

    /// <summary>
    /// Returns a summary of what's in browser localStorage and whether the
    /// per-browser "already imported" flag has been set. Returns null when
    /// JS interop is unavailable (prerender or pre-circuit-connected).
    /// </summary>
    public async Task<LegacyDataSummary?> InspectAsync()
    {
        try
        {
            var paychecks = await CountAsync<SavedPaycheck>(PaycheckKey);
            var scenarios = await CountAsync<SavedAnnualScenario>(ScenarioKey);
            var flag = await _js.InvokeAsync<string?>("paycheckStorage.get", ImportedFlagKey);
            return new LegacyDataSummary(paychecks, scenarios, AlreadyImported: !string.IsNullOrEmpty(flag));
        }
        catch (InvalidOperationException)
        {
            // JS interop not yet available (e.g. prerender). Caller polls
            // again from OnAfterRenderAsync.
            return null;
        }
    }

    public async Task<LegacyImportResult> ImportAsync()
    {
        var paychecks = await ReadAsync<SavedPaycheck>(PaycheckKey);
        var scenarios = await ReadAsync<SavedAnnualScenario>(ScenarioKey);
        var errors = new List<string>();

        int importedPaychecks = 0;
        foreach (var p in paychecks)
        {
            try
            {
                await _paycheckRepo.SaveAsync(p);
                importedPaychecks++;
            }
            catch (Exception ex)
            {
                errors.Add($"Paycheck \"{p.Name}\": {ex.Message}");
            }
        }

        int importedScenarios = 0;
        foreach (var s in scenarios)
        {
            try
            {
                await _scenarioRepo.SaveAsync(s);
                importedScenarios++;
            }
            catch (Exception ex)
            {
                errors.Add($"Scenario \"{s.Name}\": {ex.Message}");
            }
        }

        // Mark imported so we don't prompt again on next page load.
        var stamp = DateTimeOffset.UtcNow.ToString("O");
        await _js.InvokeVoidAsync("paycheckStorage.set", ImportedFlagKey, stamp);

        return new LegacyImportResult(importedPaychecks, importedScenarios, errors);
    }

    /// <summary>
    /// Marks the import as complete without actually copying anything —
    /// used by the "Skip" button so the prompt doesn't keep nagging.
    /// </summary>
    public async Task SkipAsync()
    {
        var stamp = DateTimeOffset.UtcNow.ToString("O");
        await _js.InvokeVoidAsync("paycheckStorage.set", ImportedFlagKey, $"skipped:{stamp}");
    }

    private async Task<int> CountAsync<T>(string key)
    {
        var raw = await _js.InvokeAsync<string?>("paycheckStorage.get", key);
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        try
        {
            var list = JsonSerializer.Deserialize<List<T>>(raw, JsonOptions);
            return list?.Count ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private async Task<List<T>> ReadAsync<T>(string key)
    {
        var raw = await _js.InvokeAsync<string?>("paycheckStorage.get", key);
        if (string.IsNullOrWhiteSpace(raw)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(raw, JsonOptions) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}

public sealed record LegacyDataSummary(int PaycheckCount, int ScenarioCount, bool AlreadyImported)
{
    public bool HasAny => PaycheckCount > 0 || ScenarioCount > 0;
    public bool ShouldPrompt => HasAny && !AlreadyImported;
}

public sealed record LegacyImportResult(int ImportedPaychecks, int ImportedScenarios, List<string> Errors);
