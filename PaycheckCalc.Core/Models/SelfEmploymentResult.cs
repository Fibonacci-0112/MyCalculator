namespace PaycheckCalc.Core.Models;

/// <summary>
/// Domain result for a self-employment / contractor tax estimation.
/// Contains the complete breakdown from Schedule C through SE tax,
/// QBI deduction, federal/state income tax, and quarterly estimates.
/// </summary>
public sealed class SelfEmploymentResult
{
    // ── Schedule C ──────────────────────────────────────────
    public decimal GrossRevenue { get; init; }
    public decimal CostOfGoodsSold { get; init; }
    public decimal TotalExpenses { get; init; }

    /// <summary>Schedule C Line 31: net profit (or loss).</summary>
    public decimal NetProfit { get; init; }

    // ── W-2 FICA coordination ──────────────────────────────
    /// <summary>W-2 Social Security wages used for SS wage base coordination.</summary>
    public decimal W2SocialSecurityWages { get; init; }

    /// <summary>W-2 Medicare wages used for Additional Medicare threshold coordination.</summary>
    public decimal W2MedicareWages { get; init; }

    // ── Self-Employment Tax (Schedule SE) ───────────────────
    /// <summary>Net profit × 0.9235 — the taxable base for SE tax.</summary>
    public decimal SeTaxableEarnings { get; init; }

    public decimal SocialSecurityTax { get; init; }
    public decimal MedicareTax { get; init; }
    public decimal AdditionalMedicareTax { get; init; }

    /// <summary>Total SE tax = SS + Medicare + Additional Medicare.</summary>
    public decimal TotalSeTax { get; init; }

    /// <summary>50% of total SE tax — above-the-line deduction on Form 1040.</summary>
    public decimal DeductibleHalfOfSeTax { get; init; }

    // ── Income Tax ──────────────────────────────────────────
    public decimal OtherIncome { get; init; }
    public decimal AdjustedGrossIncome { get; init; }
    public decimal StandardDeduction { get; init; }

    /// <summary>Section 199A qualified business income deduction.</summary>
    public decimal QbiDeduction { get; init; }

    public decimal TaxableIncome { get; init; }
    public decimal FederalIncomeTax { get; init; }

    public UsState State { get; init; }
    public decimal StateIncomeTax { get; init; }

    // ── Summary ─────────────────────────────────────────────
    /// <summary>Federal income tax + total SE tax.</summary>
    public decimal TotalFederalTax { get; init; }

    public decimal TotalStateTax { get; init; }

    /// <summary>All taxes: federal income + SE + state.</summary>
    public decimal TotalTax { get; init; }

    /// <summary>TotalTax / (GrossRevenue + OtherIncome) when income > 0.</summary>
    public decimal EffectiveTaxRate { get; init; }

    /// <summary>TotalTax / 4 — suggested quarterly estimated payment.</summary>
    public decimal EstimatedQuarterlyPayment { get; init; }

    /// <summary>
    /// Positive = overpaid (refund expected),
    /// negative = underpaid (balance due).
    /// </summary>
    public decimal OverUnderPayment { get; init; }
}
