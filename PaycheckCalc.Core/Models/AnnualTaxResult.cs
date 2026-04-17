using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.Core.Models;

/// <summary>
/// Result of the annual Form 1040 engine. Presents the canonical refund/owe
/// output along with every intermediate figure needed to explain how it was
/// derived. All amounts are annual dollars rounded to cents unless noted.
/// </summary>
public sealed class AnnualTaxResult
{
    public int TaxYear { get; init; }
    public FederalFilingStatus FilingStatus { get; init; }

    // ── Income build-up ─────────────────────────────────────
    /// <summary>Sum of all W-2 Box 1 wages across jobs.</summary>
    public decimal TotalW2Wages { get; init; }

    /// <summary>Schedule C Line 31 net profit (0 when no SE).</summary>
    public decimal ScheduleCNetProfit { get; init; }

    /// <summary>Schedule 1 Part I total "Additional Income".</summary>
    public decimal AdditionalIncome { get; init; }

    /// <summary>Schedule 1 Part II total "Adjustments to Income" (includes deductible half of SE tax).</summary>
    public decimal TotalAdjustments { get; init; }

    /// <summary>Form 1040 line 9 — total income.</summary>
    public decimal TotalIncome { get; init; }

    /// <summary>Form 1040 line 11 — Adjusted Gross Income.</summary>
    public decimal AdjustedGrossIncome { get; init; }

    // ── Deductions ──────────────────────────────────────────
    public decimal StandardDeduction { get; init; }

    /// <summary>Amount by which itemized exceeds standard (0 if taking std).</summary>
    public decimal ItemizedDeductionsOverStandard { get; init; }

    /// <summary>Form 8995/8995-A QBI deduction.</summary>
    public decimal QbiDeduction { get; init; }

    /// <summary>Form 1040 line 15 — taxable income.</summary>
    public decimal TaxableIncome { get; init; }

    // ── Tax ─────────────────────────────────────────────────
    /// <summary>Form 1040 line 16 — income tax from brackets (before credits).</summary>
    public decimal IncomeTaxBeforeCredits { get; init; }

    /// <summary>Schedule 3 — total nonrefundable credits applied.</summary>
    public decimal NonrefundableCredits { get; init; }

    /// <summary>Child Tax Credit applied (included within nonrefundable credits total).</summary>
    public decimal ChildTaxCredit { get; init; }

    /// <summary>
    /// Nonrefundable education credits applied from Form 8863
    /// (AOTC 60% + LLC). Included within <see cref="NonrefundableCredits"/>.
    /// </summary>
    public decimal EducationCreditsNonrefundable { get; init; }

    /// <summary>
    /// Saver's Credit (Form 8880) applied. Included within
    /// <see cref="NonrefundableCredits"/>.
    /// </summary>
    public decimal SaversCredit { get; init; }

    /// <summary>Form 1040 line 22 — income tax after nonrefundable credits.</summary>
    public decimal IncomeTaxAfterCredits { get; init; }

    // ── Other taxes (Schedule 2) ────────────────────────────
    /// <summary>Schedule SE — self-employment tax.</summary>
    public decimal SelfEmploymentTax { get; init; }

    /// <summary>Net Investment Income Tax (Form 8960 passthrough).</summary>
    public decimal NetInvestmentIncomeTax { get; init; }

    /// <summary>Any other Schedule 2 Part II taxes passed through as lump sum.</summary>
    public decimal OtherSchedule2Taxes { get; init; }

    /// <summary>Form 1040 line 24 — total tax.</summary>
    public decimal TotalTax { get; init; }

    // ── Payments (Form 1040 lines 25a/25c/26) ───────────────
    /// <summary>Sum of W-2 Box 2 federal income tax withheld.</summary>
    public decimal FederalWithholdingFromW2s { get; init; }

    /// <summary>Total Form 1040-ES estimated tax payments.</summary>
    public decimal EstimatedTaxPayments { get; init; }

    /// <summary>
    /// Excess Social Security tax credit (Schedule 3 line 11) — arises when
    /// multiple employers cumulatively withheld SS tax on wages above the
    /// annual SS wage base. Applied as a payment.
    /// </summary>
    public decimal ExcessSocialSecurityCredit { get; init; }

    /// <summary>Refundable credits from <see cref="CreditsInput"/> (AOTC 40%, EITC, etc.).</summary>
    public decimal RefundableCredits { get; init; }

    /// <summary>
    /// Refundable portion of the American Opportunity Tax Credit (40% of AOTC
    /// before cap). Included within <see cref="RefundableCredits"/>.
    /// </summary>
    public decimal RefundableEducationCredit { get; init; }

    /// <summary>
    /// Refundable Additional Child Tax Credit (ACTC) per OBBBA $1,700 cap.
    /// Included within <see cref="RefundableCredits"/>.
    /// </summary>
    public decimal RefundableAdditionalChildTaxCredit { get; init; }

    /// <summary>Form 1040 line 33 — total payments.</summary>
    public decimal TotalPayments { get; init; }

    // ── Final outcome ───────────────────────────────────────
    /// <summary>
    /// Positive = refund expected (overpaid),
    /// negative = balance due (owe).
    /// Mirrors the convention used on <see cref="AnnualProjection"/>.
    /// </summary>
    public decimal RefundOrOwe { get; init; }

    /// <summary>TotalTax / TotalIncome when TotalIncome &gt; 0; else 0. In percent (0–100).</summary>
    public decimal EffectiveTaxRate { get; init; }

    /// <summary>Marginal federal bracket rate for taxable income (0–1 decimal, e.g. 0.22m).</summary>
    public decimal MarginalTaxRate { get; init; }

    // ── Annual state tax projection ─────────────────────────
    /// <summary>
    /// Annual state/local income tax projection for the residence state,
    /// produced by <c>AnnualStateTaxCalculator</c> when wired in. Null when
    /// the orchestrator is run without a state calculator (back-compat).
    /// </summary>
    public AnnualStateTaxResult? StateTax { get; init; }
}
