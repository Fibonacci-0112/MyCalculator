namespace PaycheckCalc.App.Models;

/// <summary>
/// Presentation model for the annual Form 1040 tax projection. Decouples
/// the UI layer from the Core <c>AnnualTaxResult</c> / <c>AnnualStateTaxResult</c>
/// types and adds formatting helpers used by the results page.
/// </summary>
public sealed class AnnualTaxResultModel
{
    public int TaxYear { get; init; }
    public string FilingStatusDisplay { get; init; } = "";

    // ── Income build-up ─────────────────────────────────────
    public decimal TotalW2Wages { get; init; }
    public decimal ScheduleCNetProfit { get; init; }
    public decimal AdditionalIncome { get; init; }
    public decimal TotalAdjustments { get; init; }
    public decimal TotalIncome { get; init; }
    public decimal AdjustedGrossIncome { get; init; }

    // ── Deductions ──────────────────────────────────────────
    public decimal StandardDeduction { get; init; }
    public decimal ItemizedDeductionsOverStandard { get; init; }
    public decimal QbiDeduction { get; init; }
    public decimal TaxableIncome { get; init; }

    // ── Tax ─────────────────────────────────────────────────
    public decimal IncomeTaxBeforeCredits { get; init; }
    public decimal NonrefundableCredits { get; init; }
    public decimal ChildTaxCredit { get; init; }
    public decimal EducationCreditsNonrefundable { get; init; }
    public decimal SaversCredit { get; init; }
    public decimal IncomeTaxAfterCredits { get; init; }

    // ── Other taxes ─────────────────────────────────────────
    public decimal SelfEmploymentTax { get; init; }
    public decimal NetInvestmentIncomeTax { get; init; }
    public decimal OtherSchedule2Taxes { get; init; }
    public decimal TotalTax { get; init; }

    // ── Payments ────────────────────────────────────────────
    public decimal FederalWithholdingFromW2s { get; init; }
    public decimal EstimatedTaxPayments { get; init; }
    public decimal ExcessSocialSecurityCredit { get; init; }
    public decimal RefundableCredits { get; init; }
    public decimal RefundableEducationCredit { get; init; }
    public decimal RefundableAdditionalChildTaxCredit { get; init; }
    public decimal TotalPayments { get; init; }

    // ── Final outcome ───────────────────────────────────────
    public decimal RefundOrOwe { get; init; }
    public decimal EffectiveTaxRate { get; init; }
    public decimal MarginalTaxRate { get; init; }

    // ── State ───────────────────────────────────────────────
    public string StateName { get; init; } = "";
    public bool IsNoIncomeTaxState { get; init; }
    public decimal StateWages { get; init; }
    public decimal StateIncomeTax { get; init; }
    public decimal StateDisabilityInsurance { get; init; }
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";
    public decimal StateTaxWithheld { get; init; }
    public decimal StateRefundOrOwe { get; init; }
    public string? StateDescription { get; init; }

    // ── Display helpers ─────────────────────────────────────
    public bool IsRefund => RefundOrOwe > 0m;
    public bool IsBalanceDue => RefundOrOwe < 0m;
    public decimal RefundOrOweAbsolute => Math.Abs(RefundOrOwe);
    public string RefundOrOweLabel => RefundOrOwe >= 0m
        ? "Expected Refund"
        : "Balance Due";

    public bool IsStateRefund => StateRefundOrOwe > 0m;
    public bool IsStateBalanceDue => StateRefundOrOwe < 0m;
    public decimal StateRefundOrOweAbsolute => Math.Abs(StateRefundOrOwe);
    public string StateRefundOrOweLabel => StateRefundOrOwe >= 0m
        ? "Expected State Refund"
        : "State Balance Due";

    public bool ShowStateSection => !IsNoIncomeTaxState && (StateIncomeTax > 0m || StateTaxWithheld > 0m);
    public bool ShowStateDisability => StateDisabilityInsurance > 0m;
    public bool ShowScheduleC => ScheduleCNetProfit != 0m;
    public bool ShowQbi => QbiDeduction > 0m;
    public bool ShowCtc => ChildTaxCredit > 0m;
    public bool ShowEducationCredits => EducationCreditsNonrefundable > 0m || RefundableEducationCredit > 0m;
    public bool ShowSaversCredit => SaversCredit > 0m;
    public bool ShowExcessSsCredit => ExcessSocialSecurityCredit > 0m;
    public bool ShowAdditionalCtc => RefundableAdditionalChildTaxCredit > 0m;

    public string EffectiveTaxRateFormatted => $"{EffectiveTaxRate:F2}%";
    public string MarginalTaxRateFormatted => $"{MarginalTaxRate * 100m:F1}%";
}
