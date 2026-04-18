using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.Services;

/// <summary>
/// Shared annual-tax state. Lives as a DI singleton so every Phase 8
/// annual flyout page (Annual Projection, Jobs &amp; YTD, Other Income &amp;
/// Adjustments, Credits, Quarterly Estimates, What-If) reads and writes
/// the same underlying Form 1040 profile without duplicating fields across
/// per-page view models.
///
/// <para>
/// All fields are <see cref="ObservableProperty"/>-backed so XAML can bind
/// through a sub view-model's <c>Session</c> facade. This type owns data
/// only — mappers translate it to a <see cref="TaxYearProfile"/>, and
/// <c>Form1040Calculator</c> turns the profile into an
/// <see cref="AnnualTaxResultModel"/>.
/// </para>
/// </summary>
public partial class AnnualTaxSession : ObservableObject
{
    public AnnualTaxSession()
    {
        SelectedFederalPickerItem = FederalStatuses[0];
        SelectedState = UsState.TX;
        SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == SelectedState);
        W2Jobs.Add(new W2JobItemViewModel { Name = "Employer 1" });
    }

    // ── Basics ──────────────────────────────────────────────
    public ObservableCollection<PickerItem<FederalFilingStatus>> FederalStatuses { get; } = new(
        Enum.GetValues<FederalFilingStatus>()
            .Select(s => new PickerItem<FederalFilingStatus>(s, EnumDisplay.FederalFilingStatus(s.ToString()))));

    [ObservableProperty] public partial PickerItem<FederalFilingStatus>? SelectedFederalPickerItem { get; set; }

    partial void OnSelectedFederalPickerItemChanged(PickerItem<FederalFilingStatus>? value)
    {
        if (value != null) FilingStatus = value.Value;
    }

    [ObservableProperty]
    public partial FederalFilingStatus FilingStatus { get; set; }
        = FederalFilingStatus.SingleOrMarriedSeparately;

    [ObservableProperty] public partial int TaxYear { get; set; } = 2026;
    [ObservableProperty] public partial int QualifyingChildren { get; set; }
    [ObservableProperty] public partial decimal ItemizedDeductionsOverStandard { get; set; }

    [ObservableProperty] public partial UsState SelectedState { get; set; }
    [ObservableProperty] public partial PickerItem<UsState>? SelectedStatePickerItem { get; set; }

    partial void OnSelectedStatePickerItemChanged(PickerItem<UsState>? value)
    {
        if (value != null) SelectedState = value.Value;
    }

    partial void OnSelectedStateChanged(UsState value)
    {
        if (SelectedStatePickerItem?.Value != value)
            SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == value);
    }

    private IReadOnlyList<PickerItem<UsState>>? _statePickerItems;
    public IReadOnlyList<PickerItem<UsState>> StatePickerItems =>
        _statePickerItems ??= Enum.GetValues<UsState>()
            .Select(s => new PickerItem<UsState>(s, EnumDisplay.UsStateName(s.ToString())))
            .ToList();

    // ── W-2 jobs ────────────────────────────────────────────
    public ObservableCollection<W2JobItemViewModel> W2Jobs { get; } = new();

    // ── Schedule 1: other income ────────────────────────────
    [ObservableProperty] public partial decimal TaxableInterest { get; set; }
    [ObservableProperty] public partial decimal OrdinaryDividends { get; set; }
    [ObservableProperty] public partial decimal QualifiedDividends { get; set; }
    [ObservableProperty] public partial decimal CapitalGainOrLoss { get; set; }
    [ObservableProperty] public partial decimal UnemploymentCompensation { get; set; }
    [ObservableProperty] public partial decimal TaxableSocialSecurity { get; set; }
    [ObservableProperty] public partial decimal TaxableStateLocalRefunds { get; set; }
    [ObservableProperty] public partial decimal OtherAdditionalIncome { get; set; }

    // ── Schedule 1: adjustments ─────────────────────────────
    [ObservableProperty] public partial decimal StudentLoanInterest { get; set; }
    [ObservableProperty] public partial decimal HsaDeduction { get; set; }
    [ObservableProperty] public partial decimal TraditionalIraDeduction { get; set; }
    [ObservableProperty] public partial decimal EducatorExpenses { get; set; }
    [ObservableProperty] public partial decimal SelfEmployedHealthInsurance { get; set; }
    [ObservableProperty] public partial decimal SelfEmployedRetirement { get; set; }
    [ObservableProperty] public partial decimal OtherAdjustments { get; set; }

    // ── Credits (legacy lump-sums) ──────────────────────────
    [ObservableProperty] public partial decimal NonrefundableCredits { get; set; }
    [ObservableProperty] public partial decimal RefundableCredits { get; set; }
    [ObservableProperty] public partial decimal PrecomputedChildTaxCredit { get; set; }

    // ── CTC structured input ────────────────────────────────
    [ObservableProperty] public partial int CtcQualifyingChildren { get; set; }
    [ObservableProperty] public partial int CtcOtherDependents { get; set; }
    [ObservableProperty] public partial decimal CtcEarnedIncome { get; set; }
    [ObservableProperty] public partial bool UseStructuredChildTaxCredit { get; set; }

    // ── Education credits (Form 8863) ───────────────────────
    public ObservableCollection<EducationStudentItemViewModel> EducationStudents { get; } = new();
    [ObservableProperty] public partial decimal EducationModifiedAgiOverride { get; set; }
    [ObservableProperty] public partial bool UseStructuredEducationCredits { get; set; }

    // ── Saver's credit (Form 8880) ──────────────────────────
    [ObservableProperty] public partial decimal SaversTaxpayerContributions { get; set; }
    [ObservableProperty] public partial decimal SaversSpouseContributions { get; set; }
    [ObservableProperty] public partial bool UseStructuredSaversCredit { get; set; }

    // ── NIIT (Form 8960) ────────────────────────────────────
    [ObservableProperty] public partial decimal NiitNetInvestmentIncome { get; set; }
    [ObservableProperty] public partial decimal NiitModifiedAgiOverride { get; set; }
    [ObservableProperty] public partial bool UseStructuredNiit { get; set; }

    // ── Legacy other taxes / payments ───────────────────────
    [ObservableProperty] public partial decimal NetInvestmentIncomeTax { get; set; }
    [ObservableProperty] public partial decimal OtherSchedule2Taxes { get; set; }
    [ObservableProperty] public partial decimal EstimatedTaxPayments { get; set; }
    [ObservableProperty] public partial decimal AdditionalExpectedWithholding { get; set; }

    // ── Prior-year 1040-ES safe harbor ──────────────────────
    [ObservableProperty] public partial decimal PriorYearTotalTax { get; set; }
    [ObservableProperty] public partial decimal PriorYearAdjustedGrossIncome { get; set; }
    [ObservableProperty] public partial bool PriorYearWasFullYear { get; set; } = true;
    [ObservableProperty] public partial bool UsePriorYearSafeHarbor { get; set; }

    // ── Latest computed result ──────────────────────────────
    [ObservableProperty] public partial AnnualTaxResultModel? ResultModel { get; set; }
    public bool HasResult => ResultModel is not null;

    partial void OnResultModelChanged(AnnualTaxResultModel? value)
        => OnPropertyChanged(nameof(HasResult));

    // ── Tracking: loaded-scenario id for overwrite-on-save ──
    [ObservableProperty] public partial Guid? LoadedScenarioId { get; set; }
    [ObservableProperty] public partial string LoadedScenarioName { get; set; } = "";
}

/// <summary>
/// Row for a single student claimed on Form 8863.
/// </summary>
public partial class EducationStudentItemViewModel : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial decimal QualifiedExpenses { get; set; }
    [ObservableProperty] public partial bool ClaimAmericanOpportunityCredit { get; set; }
    [ObservableProperty] public partial bool ClaimLifetimeLearningCredit { get; set; }
}
