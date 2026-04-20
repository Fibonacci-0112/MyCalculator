namespace PaycheckCalc.App.Models;

/// <summary>
/// Presentation model for displaying paycheck results in the UI.
/// Decouples the view layer from the domain <c>PaycheckResult</c> type,
/// adding display-specific computed properties the XAML can bind to directly.
/// </summary>
public sealed class ResultCardModel
{
    // ── Income ──────────────────────────────────────────────
    public decimal GrossPay { get; init; }
    public decimal FederalTaxableIncome { get; init; }
    public decimal StateTaxableWages { get; init; }

    // ── Tax withholdings ────────────────────────────────────
    public decimal FederalWithholding { get; init; }
    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }

    // ── Deductions ──────────────────────────────────────────
    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }

    // ── Totals ──────────────────────────────────────────────
    public decimal TotalTaxes { get; init; }
    public decimal NetPay { get; init; }

    // ── Display helpers (UI-only concerns) ──────────────────
    /// <summary>True when state disability insurance is non-zero and should be shown.</summary>
    public bool ShowStateDisabilityInsurance => StateDisabilityInsurance > 0;

    /// <summary>
    /// Display label for the disability-insurance line item.
    /// Varies by state (e.g., "State Disability Insurance" for CA, "Family Leave Insurance" for CT).
    /// </summary>
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    /// <summary>Human-readable state name for display (e.g., "California").</summary>
    public string StateName { get; init; } = "";

    // ── Drill-down explanations (UI "info" affordances) ─────
    // Surfaced from domain LineItemExplanation via the mapper. All may be null
    // if the calculation produced no explanation (e.g., zero-dollar line).

    public LineItemExplanationModel? FederalExplanation { get; init; }
    public LineItemExplanationModel? SocialSecurityExplanation { get; init; }
    public LineItemExplanationModel? MedicareExplanation { get; init; }
    public LineItemExplanationModel? AdditionalMedicareExplanation { get; init; }
    public LineItemExplanationModel? StateExplanation { get; init; }
}
