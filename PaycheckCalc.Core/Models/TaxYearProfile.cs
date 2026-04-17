using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.Core.Models;

/// <summary>
/// Placeholder inputs for future Form 8863 / 8880 / dependent care / CTC work
/// (Phase 3 of the plan). Present now so <see cref="TaxYearProfile"/> is stable
/// and <c>Form1040Calculator</c> can accept a credits bundle without churn when
/// the individual form calculators are wired in.
///
/// Until the dedicated credit calculators land, only pre-computed amounts on
/// this object are applied as lump-sum credits.
/// </summary>
public sealed class CreditsInput
{
    /// <summary>
    /// Pre-computed nonrefundable credits (e.g. from Form 8880 Saver's Credit
    /// once implemented). Applied against tax before refundable credits.
    /// </summary>
    public decimal NonrefundableCredits { get; init; }

    /// <summary>
    /// Pre-computed refundable credits (e.g. 40% refundable AOTC, EITC).
    /// Added to total payments on the Form 1040.
    /// </summary>
    public decimal RefundableCredits { get; init; }

    /// <summary>
    /// Child Tax Credit — simplified entry as a single dollar amount.
    /// The full CTC/ACTC split will move into a dedicated calculator.
    /// </summary>
    public decimal ChildTaxCredit { get; init; }
}

/// <summary>
/// Placeholder inputs for future Schedule 2 "other taxes" beyond what can be
/// derived from Schedule SE and FICA coordination. Present so the orchestrator
/// API is stable.
/// </summary>
public sealed class OtherTaxesInput
{
    /// <summary>
    /// Net Investment Income Tax (Form 8960) — entered as a pre-computed amount.
    /// Dedicated calculator will replace this in a future phase.
    /// </summary>
    public decimal NetInvestmentIncomeTax { get; init; }

    /// <summary>Any other Schedule 2 Part II taxes entered directly.</summary>
    public decimal OtherSchedule2Taxes { get; init; }
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
}
