using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Auth;
using PaycheckCalc.App.Services;
using PaycheckCalc.App.Storage;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for <c>AccountPage</c>. Shows the signed-in user, a Sign
/// Out button, the live <see cref="SyncStatus"/> (online state + count
/// of operations waiting to sync), and — when present — a one-shot
/// importer for pre-account on-device data.
/// </summary>
public partial class AccountViewModel : ObservableObject
{
    private readonly AuthTokenStore _tokens;
    private readonly MauiUserContext _userContext;
    private readonly LegacyDataImporter _importer;

    public AccountViewModel(
        AuthTokenStore tokens,
        MauiUserContext userContext,
        SyncStatus syncStatus,
        LegacyDataImporter importer)
    {
        _tokens = tokens;
        _userContext = userContext;
        _importer = importer;
        SyncStatus = syncStatus;
    }

    public SyncStatus SyncStatus { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedOut))]
    [NotifyPropertyChangedFor(nameof(CanImportLegacyData))]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty] public partial string? CurrentEmail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLegacyData))]
    [NotifyPropertyChangedFor(nameof(CanImportLegacyData))]
    [NotifyPropertyChangedFor(nameof(LegacyDataSummary))]
    public partial int LegacyPaycheckCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLegacyData))]
    [NotifyPropertyChangedFor(nameof(CanImportLegacyData))]
    [NotifyPropertyChangedFor(nameof(LegacyDataSummary))]
    public partial int LegacyScenarioCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotImporting))]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImportResult))]
    public partial string? ImportResultMessage { get; set; }

    public bool IsSignedOut => !IsSignedIn;
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);
    public bool HasLegacyData => LegacyPaycheckCount > 0 || LegacyScenarioCount > 0;
    public bool CanImportLegacyData => IsSignedIn && HasLegacyData;
    public bool IsNotImporting => !IsImporting;
    public bool HasImportResult => !string.IsNullOrEmpty(ImportResultMessage);

    public string LegacyDataSummary
    {
        get
        {
            var parts = new List<string>();
            if (LegacyPaycheckCount > 0)
                parts.Add($"{LegacyPaycheckCount} saved paycheck{(LegacyPaycheckCount == 1 ? "" : "s")}");
            if (LegacyScenarioCount > 0)
                parts.Add($"{LegacyScenarioCount} annual scenario{(LegacyScenarioCount == 1 ? "" : "s")}");
            return string.Join(" and ", parts);
        }
    }

    public async Task RefreshAsync()
    {
        IsSignedIn = await _userContext.IsAuthenticatedAsync();
        CurrentEmail = await _userContext.GetCurrentEmailAsync();

        var summary = _importer.Inspect();
        LegacyPaycheckCount = summary.PaycheckCount;
        LegacyScenarioCount = summary.ScenarioCount;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _tokens.ClearAsync();
        StatusMessage = "Signed out.";
        await RefreshAsync();
        await Shell.Current.GoToAsync("//Login");
    }

    [RelayCommand]
    private async Task GoToSignInAsync()
    {
        await Shell.Current.GoToAsync("//Login");
    }

    [RelayCommand]
    private async Task ImportLegacyDataAsync()
    {
        if (IsImporting) return;
        if (!await _userContext.IsAuthenticatedAsync())
        {
            ImportResultMessage = "Sign in first so we know which account to import into.";
            return;
        }

        IsImporting = true;
        ImportResultMessage = null;
        try
        {
            var result = await _importer.ImportAsync();
            var pieces = new List<string>();
            if (result.ImportedPaychecks > 0)
                pieces.Add($"{result.ImportedPaychecks} paycheck{(result.ImportedPaychecks == 1 ? "" : "s")}");
            if (result.ImportedScenarios > 0)
                pieces.Add($"{result.ImportedScenarios} scenario{(result.ImportedScenarios == 1 ? "" : "s")}");

            var summary = pieces.Count == 0
                ? "Nothing to import."
                : $"Imported {string.Join(" and ", pieces)}.";
            if (result.Errors.Count > 0)
                summary += $" {result.Errors.Count} item{(result.Errors.Count == 1 ? "" : "s")} could not be imported.";
            ImportResultMessage = summary;

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ImportResultMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }
}
