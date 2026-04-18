using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for the What-If flyout. Captures a baseline snapshot of the
/// current <see cref="AnnualTaxSession"/> result and lets the user edit a
/// small set of high-signal deltas (filing status, residence state, extra
/// federal withholding, additional pre-tax 401(k) via HSA-style reduction,
/// CTC qualifying children) to produce a side-by-side
/// <see cref="WhatIfComparisonModel"/>. All math runs through
/// <see cref="Form1040Calculator"/> against a cloned profile.
/// </summary>
public partial class WhatIfViewModel : ObservableObject
{
    private readonly Form1040Calculator _calc;
    private TaxYearProfile? _baselineProfile;
    private AnnualTaxResult? _baselineResult;

    public WhatIfViewModel(AnnualTaxSession session, Form1040Calculator calc)
    {
        Session = session;
        _calc = calc;

        FederalStatuses = new ObservableCollection<PickerItem<FederalFilingStatus>>(
            Enum.GetValues<FederalFilingStatus>()
                .Select(s => new PickerItem<FederalFilingStatus>(
                    s, EnumDisplay.FederalFilingStatus(s.ToString()))));
        StatePickerItems = Enum.GetValues<UsState>()
            .Select(s => new PickerItem<UsState>(s, EnumDisplay.UsStateName(s.ToString())))
            .ToList();

        VariantFilingStatusPickerItem = FederalStatuses[0];
    }

    public AnnualTaxSession Session { get; }

    public ObservableCollection<PickerItem<FederalFilingStatus>> FederalStatuses { get; }
    public IReadOnlyList<PickerItem<UsState>> StatePickerItems { get; }

    // ── Baseline snapshot ───────────────────────────────────
    [ObservableProperty] public partial AnnualTaxResultModel? Baseline { get; set; }
    public bool HasBaseline => Baseline is not null;

    partial void OnBaselineChanged(AnnualTaxResultModel? value)
        => OnPropertyChanged(nameof(HasBaseline));

    // ── Variant deltas (user-editable) ──────────────────────
    [ObservableProperty] public partial PickerItem<FederalFilingStatus>? VariantFilingStatusPickerItem { get; set; }
    [ObservableProperty] public partial PickerItem<UsState>? VariantStatePickerItem { get; set; }
    [ObservableProperty] public partial decimal VariantAdditionalPreTax401k { get; set; }
    [ObservableProperty] public partial decimal VariantAdditionalExpectedWithholding { get; set; }
    [ObservableProperty] public partial int VariantQualifyingChildrenDelta { get; set; }

    // ── Variant result + comparison ─────────────────────────
    [ObservableProperty] public partial AnnualTaxResultModel? Variant { get; set; }
    [ObservableProperty] public partial WhatIfComparisonModel? Comparison { get; set; }

    public bool HasVariant => Variant is not null;
    partial void OnVariantChanged(AnnualTaxResultModel? value)
        => OnPropertyChanged(nameof(HasVariant));

    /// <summary>
    /// Snapshot the session's current profile + result as the baseline.
    /// </summary>
    [RelayCommand]
    public void CaptureBaseline()
    {
        var profile = AnnualTaxInputMapper.Map(Session);
        var result = _calc.Calculate(profile);
        _baselineProfile = profile;
        _baselineResult = result;
        Baseline = AnnualTaxResultMapper.Map(result);

        // Seed the variant pickers from the baseline so deltas start at zero.
        VariantFilingStatusPickerItem = FederalStatuses.FirstOrDefault(f => f.Value == profile.FilingStatus);
        VariantStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == profile.ResidenceState);
    }

    /// <summary>
    /// Compute the variant against the current baseline and build the
    /// side-by-side comparison model.
    /// </summary>
    [RelayCommand]
    public void RunVariant()
    {
        if (_baselineProfile is null || _baselineResult is null)
            CaptureBaseline();

        var variantProfile = CloneWithVariant(_baselineProfile!);
        var variantResult = _calc.Calculate(variantProfile);
        Variant = AnnualTaxResultMapper.Map(variantResult);
        Comparison = BuildComparison(_baselineResult!, variantResult);
    }

    /// <summary>
    /// Applies the variant deltas to a copy of the baseline profile. Kept
    /// internal so tests can compare against hand-built profiles.
    /// </summary>
    public TaxYearProfile CloneWithVariant(TaxYearProfile baseline)
    {
        var newStatus = VariantFilingStatusPickerItem?.Value ?? baseline.FilingStatus;
        var newState = VariantStatePickerItem?.Value ?? baseline.ResidenceState;

        // Extra pre-tax 401(k): reduce the first W-2 job's Box 1 and state
        // wages (keeping FICA wages unchanged — 401(k) is pre-FIT not pre-FICA).
        var adjustedJobs = baseline.W2Jobs
            .Select((j, i) => i == 0
                ? new W2JobInput
                {
                    Name = j.Name,
                    Holder = j.Holder,
                    WagesBox1 = Math.Max(0m, j.WagesBox1 - VariantAdditionalPreTax401k),
                    FederalWithholdingBox2 = j.FederalWithholdingBox2,
                    SocialSecurityWagesBox3 = j.SocialSecurityWagesBox3,
                    SocialSecurityTaxBox4 = j.SocialSecurityTaxBox4,
                    MedicareWagesBox5 = j.MedicareWagesBox5,
                    MedicareTaxBox6 = j.MedicareTaxBox6,
                    StateWagesBox16 = Math.Max(0m, (j.StateWagesBox16 > 0m ? j.StateWagesBox16 : j.WagesBox1) - VariantAdditionalPreTax401k),
                    StateWithholdingBox17 = j.StateWithholdingBox17,
                    SourceState = j.SourceState
                }
                : j)
            .ToList();

        int newQualifyingChildren = Math.Max(0, baseline.QualifyingChildren + VariantQualifyingChildrenDelta);
        var newCtc = baseline.Credits.ChildTaxCreditInput is { } oldCtc
            ? new ChildTaxCreditInput
            {
                QualifyingChildren = Math.Max(0, oldCtc.QualifyingChildren + VariantQualifyingChildrenDelta),
                OtherDependents = oldCtc.OtherDependents,
                EarnedIncome = oldCtc.EarnedIncome
            }
            : baseline.Credits.ChildTaxCreditInput;

        return new TaxYearProfile
        {
            TaxYear = baseline.TaxYear,
            FilingStatus = newStatus,
            QualifyingChildren = newQualifyingChildren,
            ResidenceState = newState,
            W2Jobs = adjustedJobs,
            SelfEmployment = baseline.SelfEmployment,
            OtherIncome = baseline.OtherIncome,
            Adjustments = baseline.Adjustments,
            ItemizedDeductionsOverStandard = baseline.ItemizedDeductionsOverStandard,
            Credits = new CreditsInput
            {
                NonrefundableCredits = baseline.Credits.NonrefundableCredits,
                RefundableCredits = baseline.Credits.RefundableCredits,
                PrecomputedChildTaxCredit = baseline.Credits.PrecomputedChildTaxCredit,
                ChildTaxCreditInput = newCtc,
                EducationCredits = baseline.Credits.EducationCredits,
                SaversCredit = baseline.Credits.SaversCredit
            },
            OtherTaxes = baseline.OtherTaxes,
            EstimatedTaxPayments = baseline.EstimatedTaxPayments,
            AdditionalExpectedWithholding = Math.Max(0m,
                baseline.AdditionalExpectedWithholding + VariantAdditionalExpectedWithholding),
            PriorYearSafeHarbor = baseline.PriorYearSafeHarbor,
            StateInputValues = baseline.StateInputValues
        };
    }

    private static WhatIfComparisonModel BuildComparison(AnnualTaxResult baseline, AnnualTaxResult variant)
    {
        var rows = new List<WhatIfRowModel>
        {
            new() { Label = "Total Income",      Baseline = baseline.TotalIncome,           Variant = variant.TotalIncome },
            new() { Label = "AGI",               Baseline = baseline.AdjustedGrossIncome,   Variant = variant.AdjustedGrossIncome },
            new() { Label = "Taxable Income",    Baseline = baseline.TaxableIncome,         Variant = variant.TaxableIncome },
            new() { Label = "Total Tax",         Baseline = baseline.TotalTax,              Variant = variant.TotalTax },
            new() { Label = "Total Payments",    Baseline = baseline.TotalPayments,         Variant = variant.TotalPayments },
            new() { Label = "Refund / (Owe)",    Baseline = baseline.RefundOrOwe,           Variant = variant.RefundOrOwe },
            new() { Label = "State Tax",         Baseline = baseline.StateTax?.StateIncomeTax ?? 0m,
                                                 Variant  = variant.StateTax?.StateIncomeTax  ?? 0m }
        };
        return new WhatIfComparisonModel
        {
            BaselineLabel = "Baseline",
            VariantLabel = "What-If",
            Rows = rows,
            HasBaseline = true,
            HasVariant = true
        };
    }
}
