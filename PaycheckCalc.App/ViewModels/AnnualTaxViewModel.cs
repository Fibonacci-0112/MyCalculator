using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Storage;
using PaycheckCalc.Core.Tax.Federal.Annual;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// Compatibility facade for the annual Form 1040 UI. Phase 8 split the
/// previous monolithic annual-tax view model into per-flyout sub
/// view-models (Annual Projection, Jobs &amp; YTD, Other Income &amp;
/// Adjustments, Credits, Quarterly Estimates, What-If) that all read and
/// write the shared <see cref="AnnualTaxSession"/>.
///
/// <para>
/// This class provides a stable binding surface for the shared Annual
/// Results page and owns the cross-page Save/Load/Delete scenario
/// commands that operate on the <see cref="IAnnualScenarioRepository"/>.
/// </para>
/// </summary>
public partial class AnnualTaxViewModel : ObservableObject
{
    private readonly Form1040Calculator _calc;
    private readonly IAnnualScenarioRepository _repo;

    public AnnualTaxViewModel(
        Form1040Calculator calc,
        AnnualTaxSession session,
        IAnnualScenarioRepository repo)
    {
        _calc = calc;
        _repo = repo;
        Session = session;
    }

    /// <summary>Shared annual-tax input state.</summary>
    public AnnualTaxSession Session { get; }

    /// <summary>Bindable: latest result, sourced from the shared session.</summary>
    public AnnualTaxResultModel? ResultModel => Session.ResultModel;

    /// <summary>Filing status as pretty string, for the results header.</summary>
    public string FilingStatusDisplay
        => PaycheckCalc.App.Helpers.EnumDisplay.FederalFilingStatus(Session.FilingStatus.ToString());

    public bool HasResult => Session.HasResult;

    /// <summary>Saved annual scenarios populated by <see cref="LoadScenariosAsync"/>.</summary>
    public ObservableCollection<SavedAnnualScenarioItemViewModel> SavedScenarios { get; } = new();

    [ObservableProperty] public partial string ScenarioNameEntry { get; set; } = "";

    /// <summary>
    /// Re-runs <see cref="Form1040Calculator"/> using the current session
    /// state and navigates to the Annual Results flyout.
    /// </summary>
    [RelayCommand]
    private async Task CalculateAsync()
    {
        var profile = AnnualTaxInputMapper.Map(Session);
        var domainResult = _calc.Calculate(profile);
        Session.ResultModel = AnnualTaxResultMapper.Map(domainResult);
        OnPropertyChanged(nameof(ResultModel));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(FilingStatusDisplay));

        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//AnnualResults");
    }

    /// <summary>
    /// Persists the current session as a named <see cref="Core.Models.SavedAnnualScenario"/>.
    /// Overwrites when a scenario is currently loaded.
    /// </summary>
    [RelayCommand]
    public async Task SaveScenarioAsync()
    {
        var name = string.IsNullOrWhiteSpace(ScenarioNameEntry)
            ? (string.IsNullOrWhiteSpace(Session.LoadedScenarioName)
                ? $"Scenario {DateTimeOffset.Now:yyyy-MM-dd HH:mm}"
                : Session.LoadedScenarioName)
            : ScenarioNameEntry.Trim();

        var scenario = AnnualScenarioMapper.ToSaved(Session, name, Session.LoadedScenarioId);
        await _repo.SaveAsync(scenario);

        Session.LoadedScenarioId = scenario.Id;
        Session.LoadedScenarioName = scenario.Name;
        await LoadScenariosAsync();
    }

    /// <summary>Refreshes <see cref="SavedScenarios"/> from the repository.</summary>
    [RelayCommand]
    public async Task LoadScenariosAsync()
    {
        var all = await _repo.GetAllAsync();
        SavedScenarios.Clear();
        foreach (var s in all)
        {
            SavedScenarios.Add(new SavedAnnualScenarioItemViewModel
            {
                Id = s.Id,
                Name = s.Name,
                TaxYear = s.Profile.TaxYear,
                FilingStatusDisplay = PaycheckCalc.App.Helpers.EnumDisplay.FederalFilingStatus(
                    s.Profile.FilingStatus.ToString()),
                StateDisplay = PaycheckCalc.App.Helpers.EnumDisplay.UsStateName(
                    s.Profile.ResidenceState.ToString()),
                UpdatedAt = s.UpdatedAt
            });
        }
    }

    /// <summary>Loads the named scenario into the session.</summary>
    [RelayCommand]
    public async Task LoadScenarioAsync(Guid id)
    {
        var scenario = await _repo.GetByIdAsync(id);
        if (scenario is null) return;

        AnnualScenarioMapper.Restore(Session, scenario);
        OnPropertyChanged(nameof(ResultModel));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(FilingStatusDisplay));
    }

    /// <summary>Deletes the named scenario from the repository.</summary>
    [RelayCommand]
    public async Task DeleteScenarioAsync(Guid id)
    {
        await _repo.DeleteAsync(id);
        if (Session.LoadedScenarioId == id)
        {
            Session.LoadedScenarioId = null;
            Session.LoadedScenarioName = "";
        }
        await LoadScenariosAsync();
    }

    /// <summary>
    /// Resets the session back to a blank profile, clearing any loaded
    /// scenario pointer.
    /// </summary>
    [RelayCommand]
    public void ResetScenario()
    {
        AnnualScenarioMapper.Restore(Session, new Core.Models.SavedAnnualScenario
        {
            Name = "",
            Profile = new Core.Models.TaxYearProfile()
        });
        Session.LoadedScenarioId = null;
        Session.LoadedScenarioName = "";
        OnPropertyChanged(nameof(ResultModel));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(FilingStatusDisplay));
    }
}
