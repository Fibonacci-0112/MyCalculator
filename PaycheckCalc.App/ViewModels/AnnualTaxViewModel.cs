using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using PaycheckCalc.Core.Tax.State;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// Single entry in the annual W-2 jobs list. Kept as a separate
/// <see cref="ObservableObject"/> so the view can bind each row
/// independently and so removal/addition keeps PropertyChanged semantics.
/// </summary>
public partial class W2JobItemViewModel : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial decimal WagesBox1 { get; set; }
    [ObservableProperty] public partial decimal FederalWithholdingBox2 { get; set; }
    [ObservableProperty] public partial decimal SocialSecurityWagesBox3 { get; set; }
    [ObservableProperty] public partial decimal SocialSecurityTaxBox4 { get; set; }
    [ObservableProperty] public partial decimal MedicareWagesBox5 { get; set; }
    [ObservableProperty] public partial decimal MedicareTaxBox6 { get; set; }
    [ObservableProperty] public partial decimal StateWagesBox16 { get; set; }
    [ObservableProperty] public partial decimal StateWithholdingBox17 { get; set; }

    [ObservableProperty] public partial bool IsSpouse { get; set; }
}

/// <summary>
/// ViewModel for the annual Form 1040 input + results flow. Composes every
/// field needed to build a <see cref="TaxYearProfile"/> and runs the Core
/// <see cref="Form1040Calculator"/> / <see cref="AnnualStateTaxCalculator"/>
/// pipeline. Peer to <see cref="SelfEmploymentViewModel"/> in structure and
/// wiring.
/// </summary>
public partial class AnnualTaxViewModel : ObservableObject
{
    private readonly Form1040Calculator _calc;

    public AnnualTaxViewModel(Form1040Calculator calc)
    {
        _calc = calc;

        SelectedFederalPickerItem = FederalStatuses[0];
        SelectedState = UsState.TX;
        SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == SelectedState);

        // Seed one empty W-2 row so the UI shows an input block on first load.
        W2Jobs.Add(new W2JobItemViewModel { Name = "Employer 1" });
    }

    // ── Tab selection (radio-style) ─────────────────────────
    [ObservableProperty] public partial int SelectedTab { get; set; } = 0;

    public bool IsTab0Visible => SelectedTab == 0;
    public bool IsTab1Visible => SelectedTab == 1;
    public bool IsTab2Visible => SelectedTab == 2;
    public bool IsTab3Visible => SelectedTab == 3;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTab0Visible));
        OnPropertyChanged(nameof(IsTab1Visible));
        OnPropertyChanged(nameof(IsTab2Visible));
        OnPropertyChanged(nameof(IsTab3Visible));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = int.Parse(tab);

    // ── Basics: filing status, tax year, residence state ────
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

    // ── W-2 jobs list ───────────────────────────────────────
    public ObservableCollection<W2JobItemViewModel> W2Jobs { get; } = new();

    [RelayCommand]
    private void AddW2Job()
    {
        W2Jobs.Add(new W2JobItemViewModel { Name = $"Employer {W2Jobs.Count + 1}" });
    }

    [RelayCommand]
    private void RemoveW2Job(W2JobItemViewModel job)
    {
        if (job != null && W2Jobs.Contains(job))
            W2Jobs.Remove(job);
    }

    // ── Schedule 1: other income ────────────────────────────
    [ObservableProperty] public partial decimal TaxableInterest { get; set; }
    [ObservableProperty] public partial decimal OrdinaryDividends { get; set; }
    [ObservableProperty] public partial decimal QualifiedDividends { get; set; }
    [ObservableProperty] public partial decimal CapitalGainOrLoss { get; set; }
    [ObservableProperty] public partial decimal UnemploymentCompensation { get; set; }
    [ObservableProperty] public partial decimal TaxableSocialSecurity { get; set; }
    [ObservableProperty] public partial decimal OtherAdditionalIncome { get; set; }

    // ── Schedule 1: adjustments ─────────────────────────────
    [ObservableProperty] public partial decimal StudentLoanInterest { get; set; }
    [ObservableProperty] public partial decimal HsaDeduction { get; set; }
    [ObservableProperty] public partial decimal TraditionalIraDeduction { get; set; }
    [ObservableProperty] public partial decimal EducatorExpenses { get; set; }
    [ObservableProperty] public partial decimal OtherAdjustments { get; set; }

    // ── Credits (simple lump-sum fields only) ───────────────
    [ObservableProperty] public partial decimal NonrefundableCredits { get; set; }
    [ObservableProperty] public partial decimal RefundableCredits { get; set; }
    [ObservableProperty] public partial decimal PrecomputedChildTaxCredit { get; set; }

    // ── Other taxes & payments ──────────────────────────────
    [ObservableProperty] public partial decimal NetInvestmentIncomeTax { get; set; }
    [ObservableProperty] public partial decimal OtherSchedule2Taxes { get; set; }
    [ObservableProperty] public partial decimal EstimatedTaxPayments { get; set; }

    // ── Result ──────────────────────────────────────────────
    [ObservableProperty] public partial AnnualTaxResultModel? ResultModel { get; set; }
    public bool HasResult => ResultModel is not null;

    partial void OnResultModelChanged(AnnualTaxResultModel? value)
    {
        OnPropertyChanged(nameof(HasResult));
    }

    // ── Calculate ───────────────────────────────────────────
    [RelayCommand]
    private async Task CalculateAsync()
    {
        var profile = AnnualTaxInputMapper.Map(this);
        var domainResult = _calc.Calculate(profile);
        ResultModel = AnnualTaxResultMapper.Map(domainResult);

        // Navigate to the results page on successful calculation.
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//AnnualResults");
    }
}
