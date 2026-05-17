using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// Per-circuit state for the Annual Tax Planner. Every annual page
/// (Projection, Jobs &amp; YTD, Other Income, Credits, Quarterly Estimates,
/// What-If, Results) reads and writes this single instance, so the
/// underlying Form 1040 profile stays in sync without per-page duplication.
///
/// Peer of <see cref="CalculatorSessionState"/> for the per-paycheck flow.
/// </summary>
public sealed class AnnualTaxSessionState
{
    public AnnualTaxSessionState()
    {
        W2Jobs.Add(new W2JobEntry { Name = "Employer 1" });
    }

    // ── Basics ──────────────────────────────────────────────
    public int TaxYear { get; set; } = 2026;
    public FederalFilingStatus FilingStatus { get; set; } =
        FederalFilingStatus.SingleOrMarriedSeparately;
    public int QualifyingChildren { get; set; }
    public decimal ItemizedDeductionsOverStandard { get; set; }
    public UsState ResidenceState { get; set; } = UsState.TX;

    // ── W-2 jobs ────────────────────────────────────────────
    public List<W2JobEntry> W2Jobs { get; } = new();

    // ── Schedule 1: other income ────────────────────────────
    public decimal TaxableInterest { get; set; }
    public decimal OrdinaryDividends { get; set; }
    public decimal QualifiedDividends { get; set; }
    public decimal CapitalGainOrLoss { get; set; }
    public decimal UnemploymentCompensation { get; set; }
    public decimal TaxableSocialSecurity { get; set; }
    public decimal TaxableStateLocalRefunds { get; set; }
    public decimal OtherAdditionalIncome { get; set; }

    // ── Schedule 1: adjustments ─────────────────────────────
    public decimal StudentLoanInterest { get; set; }
    public decimal HsaDeduction { get; set; }
    public decimal TraditionalIraDeduction { get; set; }
    public decimal EducatorExpenses { get; set; }
    public decimal SelfEmployedHealthInsurance { get; set; }
    public decimal SelfEmployedRetirement { get; set; }
    public decimal OtherAdjustments { get; set; }

    // ── Credits (legacy lump sums) ──────────────────────────
    public decimal NonrefundableCredits { get; set; }
    public decimal RefundableCredits { get; set; }
    public decimal PrecomputedChildTaxCredit { get; set; }

    // ── CTC structured input ────────────────────────────────
    public bool UseStructuredChildTaxCredit { get; set; }
    public int CtcQualifyingChildren { get; set; }
    public int CtcOtherDependents { get; set; }
    public decimal CtcEarnedIncome { get; set; }

    // ── Education credits (Form 8863) ───────────────────────
    public bool UseStructuredEducationCredits { get; set; }
    public List<EducationStudentEntry> EducationStudents { get; } = new();
    public decimal EducationModifiedAgiOverride { get; set; }

    // ── Saver's credit (Form 8880) ──────────────────────────
    public bool UseStructuredSaversCredit { get; set; }
    public decimal SaversTaxpayerContributions { get; set; }
    public decimal SaversSpouseContributions { get; set; }

    // ── NIIT (Form 8960) ────────────────────────────────────
    public bool UseStructuredNiit { get; set; }
    public decimal NiitNetInvestmentIncome { get; set; }
    public decimal NiitModifiedAgiOverride { get; set; }

    // ── Other taxes / payments ──────────────────────────────
    public decimal NetInvestmentIncomeTax { get; set; }
    public decimal OtherSchedule2Taxes { get; set; }
    public decimal EstimatedTaxPayments { get; set; }
    public decimal AdditionalExpectedWithholding { get; set; }

    // ── Prior-year 1040-ES safe harbor ──────────────────────
    public bool UsePriorYearSafeHarbor { get; set; }
    public decimal PriorYearTotalTax { get; set; }
    public decimal PriorYearAdjustedGrossIncome { get; set; }
    public bool PriorYearWasFullYear { get; set; } = true;

    // ── Latest computed result + scenario tracking ──────────
    public AnnualTaxResult? LastResult { get; set; }
    public Guid? LoadedScenarioId { get; set; }
    public string LoadedScenarioName { get; set; } = "";

    // ── What-If baseline (lives on session so it survives nav) ──
    public TaxYearProfile? WhatIfBaselineProfile { get; set; }
    public AnnualTaxResult? WhatIfBaselineResult { get; set; }

    /// <summary>
    /// Compose a Core <see cref="TaxYearProfile"/> from the current session
    /// values, ready for <c>Form1040Calculator.Calculate</c>. Direct port of
    /// <c>PaycheckCalc.App.Mappers.AnnualTaxInputMapper.Map</c>; do not clamp
    /// CapitalGainOrLoss, OtherAdditionalIncome, or OtherAdjustments to ≥0.
    /// </summary>
    public TaxYearProfile BuildProfile()
    {
        var jobs = W2Jobs.Select(j => new W2JobInput
        {
            Name = j.Name,
            Holder = j.IsSpouse ? W2JobHolder.Spouse : W2JobHolder.Taxpayer,
            WagesBox1 = Math.Max(0m, j.WagesBox1),
            FederalWithholdingBox2 = Math.Max(0m, j.FederalWithholdingBox2),
            SocialSecurityWagesBox3 = Math.Max(0m, j.SocialSecurityWagesBox3),
            SocialSecurityTaxBox4 = Math.Max(0m, j.SocialSecurityTaxBox4),
            MedicareWagesBox5 = Math.Max(0m, j.MedicareWagesBox5),
            MedicareTaxBox6 = Math.Max(0m, j.MedicareTaxBox6),
            StateWagesBox16 = Math.Max(0m, j.StateWagesBox16),
            StateWithholdingBox17 = Math.Max(0m, j.StateWithholdingBox17)
        }).ToList();

        var ctc = UseStructuredChildTaxCredit
            ? new ChildTaxCreditInput
            {
                QualifyingChildren = Math.Max(0, CtcQualifyingChildren),
                OtherDependents = Math.Max(0, CtcOtherDependents),
                EarnedIncome = Math.Max(0m, CtcEarnedIncome)
            }
            : null;

        var education = UseStructuredEducationCredits
            ? new EducationCreditsInput
            {
                Students = EducationStudents
                    .Select(st => new EducationStudentInput
                    {
                        Name = st.Name,
                        QualifiedExpenses = Math.Max(0m, st.QualifiedExpenses),
                        ClaimAmericanOpportunityCredit = st.ClaimAmericanOpportunityCredit,
                        ClaimLifetimeLearningCredit = st.ClaimLifetimeLearningCredit
                    })
                    .ToList(),
                ModifiedAgiOverride = EducationModifiedAgiOverride > 0m
                    ? EducationModifiedAgiOverride
                    : null
            }
            : null;

        var savers = UseStructuredSaversCredit
            ? new SaversCreditInput
            {
                TaxpayerContributions = Math.Max(0m, SaversTaxpayerContributions),
                SpouseContributions = Math.Max(0m, SaversSpouseContributions)
            }
            : null;

        var niit = UseStructuredNiit
            ? new NetInvestmentIncomeInput
            {
                NetInvestmentIncome = Math.Max(0m, NiitNetInvestmentIncome),
                ModifiedAgiOverride = NiitModifiedAgiOverride > 0m
                    ? NiitModifiedAgiOverride
                    : null
            }
            : null;

        // Must emit null (not a zeroed instance) when toggle is off, otherwise
        // Form 1040-ES picks the prior-year safe harbor with all zeroes.
        var priorYear = UsePriorYearSafeHarbor
            ? new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = Math.Max(0m, PriorYearTotalTax),
                PriorYearAdjustedGrossIncome = Math.Max(0m, PriorYearAdjustedGrossIncome),
                PriorYearWasFullYear = PriorYearWasFullYear
            }
            : null;

        return new TaxYearProfile
        {
            TaxYear = TaxYear,
            FilingStatus = FilingStatus,
            QualifyingChildren = Math.Max(0, QualifyingChildren),
            ResidenceState = ResidenceState,
            W2Jobs = jobs,
            ItemizedDeductionsOverStandard = Math.Max(0m, ItemizedDeductionsOverStandard),
            OtherIncome = new OtherIncomeInput
            {
                TaxableInterest = Math.Max(0m, TaxableInterest),
                OrdinaryDividends = Math.Max(0m, OrdinaryDividends),
                QualifiedDividends = Math.Max(0m, QualifiedDividends),
                CapitalGainOrLoss = CapitalGainOrLoss,
                UnemploymentCompensation = Math.Max(0m, UnemploymentCompensation),
                TaxableStateLocalRefunds = Math.Max(0m, TaxableStateLocalRefunds),
                TaxableSocialSecurity = Math.Max(0m, TaxableSocialSecurity),
                OtherAdditionalIncome = OtherAdditionalIncome
            },
            Adjustments = new AdjustmentsInput
            {
                StudentLoanInterest = Math.Max(0m, StudentLoanInterest),
                HsaDeduction = Math.Max(0m, HsaDeduction),
                EducatorExpenses = Math.Max(0m, EducatorExpenses),
                SelfEmployedHealthInsurance = Math.Max(0m, SelfEmployedHealthInsurance),
                SelfEmployedRetirement = Math.Max(0m, SelfEmployedRetirement),
                TraditionalIraDeduction = Math.Max(0m, TraditionalIraDeduction),
                OtherAdjustments = OtherAdjustments
            },
            Credits = new CreditsInput
            {
                NonrefundableCredits = Math.Max(0m, NonrefundableCredits),
                RefundableCredits = Math.Max(0m, RefundableCredits),
                PrecomputedChildTaxCredit = Math.Max(0m, PrecomputedChildTaxCredit),
                ChildTaxCreditInput = ctc,
                EducationCredits = education,
                SaversCredit = savers
            },
            OtherTaxes = new OtherTaxesInput
            {
                NetInvestmentIncomeTax = Math.Max(0m, NetInvestmentIncomeTax),
                OtherSchedule2Taxes = Math.Max(0m, OtherSchedule2Taxes),
                NetInvestmentIncome = niit
            },
            EstimatedTaxPayments = Math.Max(0m, EstimatedTaxPayments),
            AdditionalExpectedWithholding = Math.Max(0m, AdditionalExpectedWithholding),
            PriorYearSafeHarbor = priorYear
        };
    }

    /// <summary>
    /// Rehydrate this session from a previously saved scenario. Inverse of
    /// <see cref="BuildProfile"/>; the result is cleared and callers should
    /// re-run <c>Form1040Calculator</c> to refresh it. Loaded-scenario
    /// identity is captured so subsequent Saves overwrite in place.
    /// </summary>
    public void LoadFromScenario(SavedAnnualScenario scenario)
    {
        var p = scenario.Profile;

        TaxYear = p.TaxYear;
        FilingStatus = p.FilingStatus;
        QualifyingChildren = p.QualifyingChildren;
        ItemizedDeductionsOverStandard = p.ItemizedDeductionsOverStandard;
        ResidenceState = p.ResidenceState;

        W2Jobs.Clear();
        foreach (var j in p.W2Jobs)
        {
            W2Jobs.Add(new W2JobEntry
            {
                Name = j.Name,
                IsSpouse = j.Holder == W2JobHolder.Spouse,
                WagesBox1 = j.WagesBox1,
                FederalWithholdingBox2 = j.FederalWithholdingBox2,
                SocialSecurityWagesBox3 = j.SocialSecurityWagesBox3,
                SocialSecurityTaxBox4 = j.SocialSecurityTaxBox4,
                MedicareWagesBox5 = j.MedicareWagesBox5,
                MedicareTaxBox6 = j.MedicareTaxBox6,
                StateWagesBox16 = j.StateWagesBox16,
                StateWithholdingBox17 = j.StateWithholdingBox17
            });
        }

        TaxableInterest = p.OtherIncome.TaxableInterest;
        OrdinaryDividends = p.OtherIncome.OrdinaryDividends;
        QualifiedDividends = p.OtherIncome.QualifiedDividends;
        CapitalGainOrLoss = p.OtherIncome.CapitalGainOrLoss;
        UnemploymentCompensation = p.OtherIncome.UnemploymentCompensation;
        TaxableStateLocalRefunds = p.OtherIncome.TaxableStateLocalRefunds;
        TaxableSocialSecurity = p.OtherIncome.TaxableSocialSecurity;
        OtherAdditionalIncome = p.OtherIncome.OtherAdditionalIncome;

        StudentLoanInterest = p.Adjustments.StudentLoanInterest;
        HsaDeduction = p.Adjustments.HsaDeduction;
        EducatorExpenses = p.Adjustments.EducatorExpenses;
        SelfEmployedHealthInsurance = p.Adjustments.SelfEmployedHealthInsurance;
        SelfEmployedRetirement = p.Adjustments.SelfEmployedRetirement;
        TraditionalIraDeduction = p.Adjustments.TraditionalIraDeduction;
        OtherAdjustments = p.Adjustments.OtherAdjustments;

        NonrefundableCredits = p.Credits.NonrefundableCredits;
        RefundableCredits = p.Credits.RefundableCredits;
        PrecomputedChildTaxCredit = p.Credits.PrecomputedChildTaxCredit;

        if (p.Credits.ChildTaxCreditInput is { } ctc)
        {
            UseStructuredChildTaxCredit = true;
            CtcQualifyingChildren = ctc.QualifyingChildren;
            CtcOtherDependents = ctc.OtherDependents;
            CtcEarnedIncome = ctc.EarnedIncome;
        }
        else
        {
            UseStructuredChildTaxCredit = false;
            CtcQualifyingChildren = 0;
            CtcOtherDependents = 0;
            CtcEarnedIncome = 0m;
        }

        EducationStudents.Clear();
        if (p.Credits.EducationCredits is { } edu)
        {
            UseStructuredEducationCredits = true;
            EducationModifiedAgiOverride = edu.ModifiedAgiOverride ?? 0m;
            foreach (var st in edu.Students)
            {
                EducationStudents.Add(new EducationStudentEntry
                {
                    Name = st.Name,
                    QualifiedExpenses = st.QualifiedExpenses,
                    ClaimAmericanOpportunityCredit = st.ClaimAmericanOpportunityCredit,
                    ClaimLifetimeLearningCredit = st.ClaimLifetimeLearningCredit
                });
            }
        }
        else
        {
            UseStructuredEducationCredits = false;
            EducationModifiedAgiOverride = 0m;
        }

        if (p.Credits.SaversCredit is { } sv)
        {
            UseStructuredSaversCredit = true;
            SaversTaxpayerContributions = sv.TaxpayerContributions;
            SaversSpouseContributions = sv.SpouseContributions;
        }
        else
        {
            UseStructuredSaversCredit = false;
            SaversTaxpayerContributions = 0m;
            SaversSpouseContributions = 0m;
        }

        NetInvestmentIncomeTax = p.OtherTaxes.NetInvestmentIncomeTax;
        OtherSchedule2Taxes = p.OtherTaxes.OtherSchedule2Taxes;
        if (p.OtherTaxes.NetInvestmentIncome is { } niit)
        {
            UseStructuredNiit = true;
            NiitNetInvestmentIncome = niit.NetInvestmentIncome;
            NiitModifiedAgiOverride = niit.ModifiedAgiOverride ?? 0m;
        }
        else
        {
            UseStructuredNiit = false;
            NiitNetInvestmentIncome = 0m;
            NiitModifiedAgiOverride = 0m;
        }

        EstimatedTaxPayments = p.EstimatedTaxPayments;
        AdditionalExpectedWithholding = p.AdditionalExpectedWithholding;

        if (p.PriorYearSafeHarbor is { } py)
        {
            UsePriorYearSafeHarbor = true;
            PriorYearTotalTax = py.PriorYearTotalTax;
            PriorYearAdjustedGrossIncome = py.PriorYearAdjustedGrossIncome;
            PriorYearWasFullYear = py.PriorYearWasFullYear;
        }
        else
        {
            UsePriorYearSafeHarbor = false;
            PriorYearTotalTax = 0m;
            PriorYearAdjustedGrossIncome = 0m;
            PriorYearWasFullYear = true;
        }

        LastResult = null;
        LoadedScenarioId = scenario.Id;
        LoadedScenarioName = scenario.Name;
        WhatIfBaselineProfile = null;
        WhatIfBaselineResult = null;
    }

    /// <summary>
    /// Reset every field to its default. Used by "New scenario" on the
    /// Results page.
    /// </summary>
    public void Reset()
    {
        TaxYear = 2026;
        FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately;
        QualifyingChildren = 0;
        ItemizedDeductionsOverStandard = 0m;
        ResidenceState = UsState.TX;

        W2Jobs.Clear();
        W2Jobs.Add(new W2JobEntry { Name = "Employer 1" });

        TaxableInterest = OrdinaryDividends = QualifiedDividends = 0m;
        CapitalGainOrLoss = UnemploymentCompensation = TaxableSocialSecurity = 0m;
        TaxableStateLocalRefunds = OtherAdditionalIncome = 0m;

        StudentLoanInterest = HsaDeduction = TraditionalIraDeduction = 0m;
        EducatorExpenses = SelfEmployedHealthInsurance = SelfEmployedRetirement = 0m;
        OtherAdjustments = 0m;

        NonrefundableCredits = RefundableCredits = PrecomputedChildTaxCredit = 0m;

        UseStructuredChildTaxCredit = false;
        CtcQualifyingChildren = CtcOtherDependents = 0;
        CtcEarnedIncome = 0m;

        UseStructuredEducationCredits = false;
        EducationStudents.Clear();
        EducationModifiedAgiOverride = 0m;

        UseStructuredSaversCredit = false;
        SaversTaxpayerContributions = SaversSpouseContributions = 0m;

        UseStructuredNiit = false;
        NiitNetInvestmentIncome = NiitModifiedAgiOverride = 0m;

        NetInvestmentIncomeTax = OtherSchedule2Taxes = 0m;
        EstimatedTaxPayments = AdditionalExpectedWithholding = 0m;

        UsePriorYearSafeHarbor = false;
        PriorYearTotalTax = PriorYearAdjustedGrossIncome = 0m;
        PriorYearWasFullYear = true;

        LastResult = null;
        LoadedScenarioId = null;
        LoadedScenarioName = "";
        WhatIfBaselineProfile = null;
        WhatIfBaselineResult = null;
    }
}

/// <summary>
/// UI row for a single W-2 job on the Jobs &amp; YTD tab. Converted to the
/// immutable <see cref="W2JobInput"/> domain model when building the profile.
/// </summary>
public sealed class W2JobEntry
{
    public string Name { get; set; } = "";
    public bool IsSpouse { get; set; }
    public decimal WagesBox1 { get; set; }
    public decimal FederalWithholdingBox2 { get; set; }
    public decimal SocialSecurityWagesBox3 { get; set; }
    public decimal SocialSecurityTaxBox4 { get; set; }
    public decimal MedicareWagesBox5 { get; set; }
    public decimal MedicareTaxBox6 { get; set; }
    public decimal StateWagesBox16 { get; set; }
    public decimal StateWithholdingBox17 { get; set; }
}

/// <summary>
/// UI row for a single student on Form 8863. Converted to the immutable
/// <see cref="EducationStudentInput"/> when building the profile.
/// </summary>
public sealed class EducationStudentEntry
{
    public string Name { get; set; } = "";
    public decimal QualifiedExpenses { get; set; }
    public bool ClaimAmericanOpportunityCredit { get; set; }
    public bool ClaimLifetimeLearningCredit { get; set; }
}
