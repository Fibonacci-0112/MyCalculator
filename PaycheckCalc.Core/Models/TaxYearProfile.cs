using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Models;

/// <summary>
/// Container for Schedule 3 credits. Callers may supply either structured
/// inputs (<see cref="ChildTaxCredit"/> / <see cref="EducationCredits"/> /
/// <see cref="SaversCredit"/>) that are computed by the dedicated
/// calculators, or the legacy pre-computed lump sums
/// (<see cref="NonrefundableCredits"/>, <see cref="RefundableCredits"/>,
/// <see cref="PrecomputedChildTaxCredit"/>) for back-compat, or both —
/// the engine adds them together.
/// </summary>
public sealed class CreditsInput
{
    /// <summary>
    /// Pre-computed nonrefundable credits (e.g. from external tools). Added
    /// to the engine's dedicated-calculator results before capping at the
    /// income tax.
    /// </summary>
    public decimal NonrefundableCredits { get; init; }

    /// <summary>
    /// Pre-computed refundable credits (e.g. EITC). Added to the engine's
    /// dedicated-calculator refundable totals.
    /// </summary>
    public decimal RefundableCredits { get; init; }

    /// <summary>
    /// Pre-computed Child Tax Credit (legacy lump-sum entry). When a
    /// <see cref="ChildTaxCredit"/> input is also supplied the two amounts
    /// are combined — prefer the structured input for new code.
    /// </summary>
    public decimal PrecomputedChildTaxCredit { get; init; }

    /// <summary>
    /// Back-compat alias for <see cref="PrecomputedChildTaxCredit"/>. Existing
    /// callers that set <c>Credits.ChildTaxCredit = …</c> continue to work.
    /// </summary>
    public decimal ChildTaxCredit
    {
        get => PrecomputedChildTaxCredit;
        init => PrecomputedChildTaxCredit = value;
    }

    /// <summary>
    /// Structured Child Tax Credit / ODC input. When non-null the engine
    /// runs <c>ChildTaxCreditCalculator</c> against it.
    /// </summary>
    public ChildTaxCreditInput? ChildTaxCreditInput { get; init; }

    /// <summary>
    /// Structured education credits input (Form 8863). When non-null the
    /// engine runs <c>Form8863EducationCreditsCalculator</c>.
    /// </summary>
    public EducationCreditsInput? EducationCredits { get; init; }

    /// <summary>
    /// Structured Saver's Credit input (Form 8880). When non-null the
    /// engine runs <c>Form8880SaversCreditCalculator</c>.
    /// </summary>
    public SaversCreditInput? SaversCredit { get; init; }
}

/// <summary>
/// Schedule 2 "other taxes" beyond what Schedule SE and FICA coordination
/// cover. Callers may supply either a structured
/// <see cref="NetInvestmentIncomeInput"/> (which drives
/// <c>Form8960NiitCalculator</c>) or the legacy pre-computed
/// <see cref="NetInvestmentIncomeTax"/> lump sum — the engine adds them
/// together.
/// </summary>
public sealed class OtherTaxesInput
{
    /// <summary>
    /// Legacy pre-computed NIIT amount. When a
    /// <see cref="NetInvestmentIncome"/> input is also supplied the two
    /// amounts are combined.
    /// </summary>
    public decimal NetInvestmentIncomeTax { get; init; }

    /// <summary>Any other Schedule 2 Part II taxes entered directly.</summary>
    public decimal OtherSchedule2Taxes { get; init; }

    /// <summary>
    /// Structured Net Investment Income Tax input (Form 8960). When non-null
    /// the engine runs <c>Form8960NiitCalculator</c>.
    /// </summary>
    public NetInvestmentIncomeInput? NetInvestmentIncome { get; init; }
}

/// <summary>
/// Year-level taxpayer profile aggregating everything the annual Form 1040
/// engine needs: filing info, one or more W-2 jobs, optional self-employment,
/// additional income, adjustments, credits, payments already made, and state
/// of residence.
///
/// This model is Phase 1 of the financial-decision-tool plan and is consumed
/// by <c>Form1040Calculator</c>. It is intentionally UI-agnostic.
/// </summary>
public sealed class TaxYearProfile
{
    /// <summary>Tax year this profile describes (e.g. 2026).</summary>
    public int TaxYear { get; init; } = 2026;

    /// <summary>Federal filing status. Drives bracket and std-deduction selection.</summary>
    public FederalFilingStatus FilingStatus { get; init; } = FederalFilingStatus.SingleOrMarriedSeparately;

    /// <summary>
    /// Number of qualifying children for CTC purposes. Not used for
    /// dependent head-counting elsewhere in the engine.
    /// </summary>
    public int QualifyingChildren { get; init; }

    /// <summary>State of residence for state-tax estimation.</summary>
    public UsState ResidenceState { get; init; } = UsState.TX;

    /// <summary>W-2 jobs. Empty list is valid (e.g. purely self-employed).</summary>
    public IReadOnlyList<W2JobInput> W2Jobs { get; init; } = Array.Empty<W2JobInput>();

    /// <summary>
    /// Optional self-employment input. When non-null, the engine runs the
    /// existing <c>SelfEmploymentCalculator</c>/<c>SelfEmploymentTaxCalculator</c>
    /// pipeline to derive Schedule C net profit, SE tax, and its deductible half.
    /// </summary>
    public SelfEmploymentInput? SelfEmployment { get; init; }

    /// <summary>Non-wage income (interest, dividends, cap gains, etc.).</summary>
    public OtherIncomeInput OtherIncome { get; init; } = new();

    /// <summary>Above-the-line adjustments to income (Schedule 1 Part II).</summary>
    public AdjustmentsInput Adjustments { get; init; } = new();

    /// <summary>
    /// Amount by which itemized deductions exceed the standard deduction.
    /// Zero means the taxpayer takes the standard deduction.
    /// Matches the convention already used on <see cref="SelfEmploymentInput"/>.
    /// </summary>
    public decimal ItemizedDeductionsOverStandard { get; init; }

    /// <summary>Credits inputs (currently accepts pre-computed amounts).</summary>
    public CreditsInput Credits { get; init; } = new();

    /// <summary>Other Schedule 2 taxes (NIIT, etc.) beyond SE tax.</summary>
    public OtherTaxesInput OtherTaxes { get; init; } = new();

    /// <summary>
    /// Total Form 1040-ES estimated tax payments already made for the year.
    /// Added to total payments alongside W-2 federal withholding.
    /// </summary>
    public decimal EstimatedTaxPayments { get; init; }

    /// <summary>
    /// Optional prior-year figures enabling the 100% / 110% prior-year safe
    /// harbor on the 1040-ES worksheet. When null, the annual engine falls
    /// back to the 90%-of-current-year safe harbor for quarterly estimates.
    /// </summary>
    public PriorYearSafeHarborInput? PriorYearSafeHarbor { get; init; }

    /// <summary>
    /// Expected additional federal withholding beyond W-2 Box 2 (e.g. 1099-R
    /// voluntary withholding). Combined with W-2 withholding when computing
    /// whether estimates are required. Defaults to 0.
    /// </summary>
    public decimal AdditionalExpectedWithholding { get; init; }

    /// <summary>
    /// Optional state-specific input values (e.g., Alabama dependents, IL-W-4
    /// allowances) consumed by the registered <see cref="IStateWithholdingCalculator"/>
    /// for <see cref="ResidenceState"/> when the annual state-tax projection
    /// runs. Null / empty is accepted — calculators fall back to their own
    /// defaults.
    /// </summary>
    public StateInputValues? StateInputValues { get; init; }
}
