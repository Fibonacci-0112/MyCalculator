namespace PaycheckCalc.App.Models;

/// <summary>
/// Presentation model for displaying self-employment tax results in the UI.
/// Decouples the view layer from the domain <c>SelfEmploymentResult</c> type.
/// </summary>
public sealed class SelfEmploymentResultModel
{
    // ── Schedule C ──────────────────────────────────────────
    public decimal GrossRevenue { get; init; }
    public decimal CostOfGoodsSold { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal NetProfit { get; init; }

    // ── W-2 FICA coordination ───────────────────────────────
    public decimal W2SocialSecurityWages { get; init; }
    public decimal W2MedicareWages { get; init; }

    // ── SE Tax ──────────────────────────────────────────────
    public decimal SeTaxableEarnings { get; init; }
    public decimal SocialSecurityTax { get; init; }
    public decimal MedicareTax { get; init; }
    public decimal AdditionalMedicareTax { get; init; }
    public decimal TotalSeTax { get; init; }
    public decimal DeductibleHalfOfSeTax { get; init; }

    // ── Income Tax ──────────────────────────────────────────
    public decimal OtherIncome { get; init; }
    public decimal AdjustedGrossIncome { get; init; }
    public decimal StandardDeduction { get; init; }
    public decimal QbiDeduction { get; init; }
    public decimal TaxableIncome { get; init; }
    public decimal FederalIncomeTax { get; init; }
    public string StateName { get; init; } = "";
    public decimal StateIncomeTax { get; init; }

    // ── Summary ─────────────────────────────────────────────
    public decimal TotalFederalTax { get; init; }
    public decimal TotalStateTax { get; init; }
    public decimal TotalTax { get; init; }
    public decimal EffectiveTaxRate { get; init; }
    public decimal EstimatedQuarterlyPayment { get; init; }
    public decimal OverUnderPayment { get; init; }

    // ── Display helpers ─────────────────────────────────────
    public bool ShowCogs => CostOfGoodsSold > 0;
    public bool ShowOtherIncome => OtherIncome > 0;
    public bool ShowW2Wages => W2SocialSecurityWages > 0 || W2MedicareWages > 0;
    public bool ShowAdditionalMedicare => AdditionalMedicareTax > 0;
    public bool ShowQbi => QbiDeduction > 0;
    public bool ShowStateTax => StateIncomeTax > 0;
    public bool IsOverpayment => OverUnderPayment > 0;
    public bool IsUnderpayment => OverUnderPayment < 0;
    public bool ShowOverUnder => OverUnderPayment != 0;
    public string OverUnderLabel => OverUnderPayment > 0 ? "Overpayment (Refund)" : "Underpayment (Balance Due)";
    public decimal OverUnderAbsolute => Math.Abs(OverUnderPayment);
    public string EffectiveTaxRateFormatted => $"{EffectiveTaxRate:F2}%";
}
