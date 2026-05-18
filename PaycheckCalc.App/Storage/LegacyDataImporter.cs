using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// Reads pre-user-scoping JSON files from
/// <c>FileSystem.AppDataDirectory/saved_paychecks.json</c> and
/// <c>FileSystem.AppDataDirectory/saved_annual_scenarios.json</c>
/// (where Phase 1's MAUI app wrote everything before paths became
/// user-scoped in Phase 3) and imports them into the signed-in
/// account via the live <see cref="IPaycheckRepository"/> /
/// <see cref="IAnnualScenarioRepository"/>.
///
/// On success, renames the legacy files to <c>*.imported-{stamp}.bak</c>
/// rather than deleting them — gives the user a recovery path if
/// anything goes sideways.
/// </summary>
public sealed class LegacyDataImporter
{
    private readonly IPaycheckRepository _paycheckRepo;
    private readonly IAnnualScenarioRepository _scenarioRepo;
    private readonly string _baseDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public LegacyDataImporter(IPaycheckRepository paycheckRepo, IAnnualScenarioRepository scenarioRepo)
    {
        _paycheckRepo = paycheckRepo;
        _scenarioRepo = scenarioRepo;
        _baseDirectory = FileSystem.AppDataDirectory;
    }

    private string PaycheckLegacyPath => Path.Combine(_baseDirectory, "saved_paychecks.json");
    private string ScenarioLegacyPath => Path.Combine(_baseDirectory, "saved_annual_scenarios.json");

    public LegacyDataSummary Inspect()
    {
        var paycheckCount = CountFromFile<SavedPaycheck>(PaycheckLegacyPath);
        var scenarioCount = CountFromFile<SavedAnnualScenario>(ScenarioLegacyPath);
        return new LegacyDataSummary(paycheckCount, scenarioCount);
    }

    public async Task<LegacyImportResult> ImportAsync()
    {
        var paychecks = ReadFromFile<SavedPaycheck>(PaycheckLegacyPath);
        var scenarios = ReadFromFile<SavedAnnualScenario>(ScenarioLegacyPath);

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

        // Rename to .bak instead of deleting — recovery path if import had
        // partial errors.
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        TryRenameToBackup(PaycheckLegacyPath, stamp);
        TryRenameToBackup(ScenarioLegacyPath, stamp);

        return new LegacyImportResult(importedPaychecks, importedScenarios, errors);
    }

    private static int CountFromFile<T>(string path)
    {
        if (!File.Exists(path)) return 0;
        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
            return list?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static List<T> ReadFromFile<T>(string path)
    {
        if (!File.Exists(path)) return new();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static void TryRenameToBackup(string path, string stamp)
    {
        if (!File.Exists(path)) return;
        try
        {
            var backup = $"{path}.imported-{stamp}.bak";
            File.Move(path, backup, overwrite: true);
        }
        catch
        {
            // Best-effort — if the rename fails the import still succeeded
            // and the importer's Inspect() will report 0 on next pass if
            // file got partially cleared, or repeat the import otherwise.
        }
    }
}

public sealed record LegacyDataSummary(int PaycheckCount, int ScenarioCount)
{
    public bool HasAny => PaycheckCount > 0 || ScenarioCount > 0;
}

public sealed record LegacyImportResult(int ImportedPaychecks, int ImportedScenarios, List<string> Errors);
