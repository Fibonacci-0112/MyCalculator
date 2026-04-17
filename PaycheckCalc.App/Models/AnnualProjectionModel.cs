namespace PaycheckCalc.App.Models;

/// <summary>
/// Presentation model for displaying annual paycheck projections in the UI.
/// Decouples the view layer from the domain <c>AnnualProjection</c> type,
/// adding display-specific computed properties the XAML can bind to directly.
/// </summary>
public sealed class AnnualProjectionModel
{
    // ── Pay period info ─────────────────────────────────────
    public int PayPeriodsPerYear { get; init; }
    public int CurrentPaycheckNumber { get; init; }
    public int RemainingPaychecks { get; init; }

    // ── Annualized amounts ──────────────────────────────────
    public decimal AnnualizedGrossPay { get; init; }
    public decimal AnnualizedPreTaxDeductions { get; init; }
    public decimal AnnualizedPostTaxDeductions { get; init; }
    public decimal AnnualizedFederalTaxableWages { get; init; }
    public decimal AnnualizedFicaTaxableWages { get; init; }
    public decimal AnnualizedStateTaxableWages { get; init; }
    public decimal AnnualizedFederalWithholding { get; init; }
    public decimal AnnualizedStateWithholding { get; init; }
    public decimal AnnualizedFica { get; init; }
    public decimal AnnualizedNetPay { get; init; }

    // ── Projected YTD ───────────────────────────────────────
    public decimal ProjectedYtdGrossPay { get; init; }
    public decimal ProjectedYtdFederalWithholding { get; init; }
    public decimal ProjectedYtdStateWithholding { get; init; }
    public decimal ProjectedYtdFica { get; init; }
    public decimal ProjectedYtdNetPay { get; init; }

    // ── Under/over withholding estimate ─────────────────────
    public decimal EstimatedAnnualFederalLiability { get; init; }
    public decimal EstimatedAnnualFicaLiability { get; init; }
    public decimal AnnualizedTotalWithholding { get; init; }
    public decimal EstimatedTotalLiability { get; init; }
    public decimal OverUnderWithholding { get; init; }

    // ── Display helpers (UI-only concerns) ──────────────────
    /// <summary>True when over-withholding (likely refund).</summary>
    public bool IsOverWithholding => OverUnderWithholding > 0;

    /// <summary>True when under-withholding (likely owe).</summary>
    public bool IsUnderWithholding => OverUnderWithholding < 0;

    /// <summary>Absolute value of the over/under amount for display.</summary>
    public decimal OverUnderAmount => Math.Abs(OverUnderWithholding);

    /// <summary>Display label: "Estimated Refund" or "Estimated Amount Owed".</summary>
    public string OverUnderLabel => OverUnderWithholding >= 0
        ? "Estimated Refund"
        : "Estimated Amount Owed";
}
