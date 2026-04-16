using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Models;

/// <summary>
/// Domain input for self-employment / contractor tax estimation.
/// Captures Schedule C income, filing details, and state information
/// needed to compute SE tax, federal income tax, QBI deduction, and state tax.
/// </summary>
public sealed class SelfEmploymentInput
{
    // ── Schedule C (simplified) ─────────────────────────────
    /// <summary>Schedule C Line 1: gross receipts / revenue.</summary>
    public decimal GrossRevenue { get; init; }

    /// <summary>Schedule C Line 4: cost of goods sold (optional).</summary>
    public decimal CostOfGoodsSold { get; init; }

    /// <summary>Schedule C Line 28: total business expenses.</summary>
    public decimal TotalBusinessExpenses { get; init; }

    // ── Other income ────────────────────────────────────────
    /// <summary>
    /// Non-SE income (W-2 wages, interest, dividends, etc.) used to
    /// place the taxpayer in the correct bracket for federal/state tax.
    /// </summary>
    public decimal OtherIncome { get; init; }

    // ── W-2 FICA coordination ───────────────────────────────
    /// <summary>
    /// W-2 Social Security wages (Box 3) already subject to employer
    /// withholding. Used to coordinate the SS wage base cap so that SE
    /// Social Security tax is only assessed on the remaining room under
    /// the annual wage base ($184,500 for 2026).
    /// </summary>
    public decimal W2SocialSecurityWages { get; init; }

    /// <summary>
    /// W-2 Medicare wages (Box 5) already subject to employer withholding.
    /// Used to coordinate the Additional Medicare Tax threshold ($200,000
    /// for most filers) so that the 0.9% surtax applies only to combined
    /// wages + SE earnings above that threshold.
    /// </summary>
    public decimal W2MedicareWages { get; init; }

    // ── Federal filing info ─────────────────────────────────
    public FederalFilingStatus FilingStatus { get; init; } = FederalFilingStatus.SingleOrMarriedSeparately;

    // ── State ───────────────────────────────────────────────
    public UsState State { get; init; } = UsState.TX;

    /// <summary>
    /// Dynamic state-specific input values populated by the UI from the
    /// calculator's <see cref="IStateWithholdingCalculator.GetInputSchema"/>.
    /// </summary>
    public StateInputValues? StateInputValues { get; init; }

    // ── Deductions ──────────────────────────────────────────
    /// <summary>
    /// Amount by which itemized deductions exceed the standard deduction.
    /// Zero means the taxpayer takes the standard deduction.
    /// </summary>
    public decimal ItemizedDeductionsOverStandard { get; init; }

    // ── QBI (Form 8995 / 8995-A) ────────────────────────────
    /// <summary>
    /// True when the business is a Specified Service Trade or Business (SSTB)
    /// subject to QBI phase-out rules above the income threshold.
    /// </summary>
    public bool IsSpecifiedServiceBusiness { get; init; }

    /// <summary>
    /// W-2 wages paid by the qualified business. Used in the W-2/UBIA
    /// limitation for the full Form 8995-A calculation.
    /// </summary>
    public decimal QualifiedBusinessW2Wages { get; init; }

    /// <summary>
    /// Unadjusted basis immediately after acquisition (UBIA) of qualified
    /// property held by the business. Used in the W-2/UBIA limitation.
    /// </summary>
    public decimal QualifiedPropertyUbia { get; init; }

    // ── Estimated payments ──────────────────────────────────
    /// <summary>
    /// Total estimated tax payments already made this year (Form 1040-ES).
    /// Used to compute over/under payment.
    /// </summary>
    public decimal EstimatedTaxPayments { get; init; }
}
