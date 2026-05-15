using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// ViewModel for the Dashboard landing page. Surfaces three things at a
/// glance: quick-action navigation, the user's most recently saved paycheck,
/// and side-by-side YTD trackers (actual sums from saved paychecks +
/// projected annual figures from <see cref="AnnualProjectionCalculator"/>).
/// </summary>
/// <remarks>
/// Registered as a singleton (matching the project's other VMs), so the page
/// MUST call <see cref="LoadAsync"/> in its <c>OnAppearing</c> hook to refresh
/// after the user saves a new paycheck elsewhere in the app.
/// </remarks>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IPaycheckRepository _repo;
    private readonly YtdSummaryCalculator _ytd;
    private readonly PayCalculator _pay;
    private readonly AnnualProjectionCalculator _projector;
    private readonly CalculatorViewModel _calculatorVm;

    public DashboardViewModel(
        IPaycheckRepository repo,
        YtdSummaryCalculator ytd,
        PayCalculator pay,
        AnnualProjectionCalculator projector,
        CalculatorViewModel calculatorVm)
    {
        _repo         = repo;
        _ytd          = ytd;
        _pay          = pay;
        _projector    = projector;
        _calculatorVm = calculatorVm;
    }

    // ── State ───────────────────────────────────────────────

    [ObservableProperty] public partial bool IsLoading { get; set; }

    /// <summary>True when there are no saved paychecks for any year.</summary>
    [ObservableProperty] public partial bool IsEmpty { get; set; } = true;

    [ObservableProperty] public partial ResultCardModel? LatestResult { get; set; }
    [ObservableProperty] public partial string? LatestName { get; set; }
    [ObservableProperty] public partial string? LatestStateName { get; set; }
    [ObservableProperty] public partial string? LatestUpdatedAtDisplay { get; set; }
    [ObservableProperty] public partial Guid? LatestId { get; set; }

    [ObservableProperty] public partial decimal LatestFica { get; set; }

    [ObservableProperty] public partial AnnualProjectionModel? Projection { get; set; }

    /// <summary>Year that <see cref="YtdActual"/> aggregates.</summary>
    [ObservableProperty] public partial int YtdYear { get; set; } = DateTime.Now.Year;
    [ObservableProperty] public partial int YtdPaycheckCount { get; set; }
    [ObservableProperty] public partial decimal YtdGross { get; set; }
    [ObservableProperty] public partial decimal YtdTaxes { get; set; }
    [ObservableProperty] public partial decimal YtdNet { get; set; }

    /// <summary>Inverse of <see cref="IsEmpty"/> for XAML <c>IsVisible</c> binding.</summary>
    public bool HasContent => !IsEmpty;

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasContent));

    // ── Commands ────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _repo.GetAllAsync();
            var ordered = all.OrderByDescending(p => p.UpdatedAt).ToList();

            YtdYear = DateTime.Now.Year;
            var summary = _ytd.Calculate(ordered, YtdYear);
            YtdPaycheckCount = summary.PaycheckCount;
            YtdGross         = summary.TotalGross;
            YtdTaxes         = summary.TotalTaxes;
            YtdNet           = summary.TotalNet;

            var latest = ordered.FirstOrDefault();
            if (latest is null)
            {
                IsEmpty                = true;
                LatestResult           = null;
                LatestName             = null;
                LatestStateName        = null;
                LatestUpdatedAtDisplay = null;
                LatestId               = null;
                LatestFica             = 0m;
                Projection             = null;
                return;
            }

            IsEmpty                = false;
            LatestId               = latest.Id;
            LatestName             = string.IsNullOrWhiteSpace(latest.Name) ? "(Unnamed)" : latest.Name;
            LatestStateName        = EnumDisplay.UsStateName(latest.Input.State.ToString());
            LatestUpdatedAtDisplay = latest.UpdatedAt.LocalDateTime.ToString("g");
            LatestResult           = ResultCardMapper.Map(latest.Result);
            LatestFica             = latest.Result.SocialSecurityWithholding
                                   + latest.Result.MedicareWithholding
                                   + latest.Result.AdditionalMedicareWithholding;

            // Re-run the engine on the saved input so the projection picks up
            // any tax-table updates since the paycheck was saved. YTD actuals
            // above still use the saved Result (what the user actually saw).
            var fresh = _pay.Calculate(latest.Input);
            var projection = _projector.Calculate(latest.Input, fresh);
            Projection = AnnualProjectionMapper.Map(projection);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NewPaycheckAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//Inputs");
    }

    [RelayCommand]
    private async Task OpenSavedAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//Saved");
    }

    [RelayCommand]
    private async Task OpenSelfEmploymentAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//SelfEmployment");
    }

    [RelayCommand]
    private async Task OpenAnnualPlannerAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//JobsAndYtd");
    }

    /// <summary>
    /// Restores the most recent saved paycheck into <see cref="CalculatorViewModel"/>
    /// and navigates to the Inputs tab — mirrors
    /// <c>SavedPaychecksViewModel.LoadIntoCalculatorAsync</c>.
    /// </summary>
    [RelayCommand]
    private async Task LoadLatestIntoCalculatorAsync()
    {
        if (LatestId is not { } id) return;

        var paycheck = await _repo.GetByIdAsync(id);
        if (paycheck is null) return;

        PaycheckInputRestorer.Restore(_calculatorVm, paycheck.Input);
        _calculatorVm.LoadedPaycheckId   = paycheck.Id;
        _calculatorVm.LoadedPaycheckName = paycheck.Name;
        _calculatorVm.CalculateCommand.Execute(null);

        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//Inputs");
    }
}
