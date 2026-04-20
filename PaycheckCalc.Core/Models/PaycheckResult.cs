using PaycheckCalc.Core.Tax.Local;

namespace PaycheckCalc.Core.Models;

public sealed class PaycheckResult
{
    public decimal GrossPay { get; init; }
    public decimal PreTaxDeductions { get; init; }
    public decimal PostTaxDeductions { get; init; }

    public UsState State { get; init; }
    public decimal StateTaxableWages { get; init; }
    public decimal StateWithholding { get; init; }
    public decimal StateDisabilityInsurance { get; init; }
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    public decimal SocialSecurityWithholding { get; init; }
    public decimal MedicareWithholding { get; init; }
    public decimal AdditionalMedicareWithholding { get; init; }

    public decimal FederalTaxableIncome { get; init; }
    public decimal FederalWithholding { get; init; }

    /// <summary>Wages subject to local income tax after locality-specific deductions.</summary>
    public decimal LocalTaxableWages { get; init; }

    /// <summary>Local income-tax withholding for the pay period (sum of all registered localities).</summary>
    public decimal LocalWithholding { get; init; }

    /// <summary>
    /// Flat per-pay-period locality charges that are not percentage-based income tax
    /// (e.g. PA Local Services Tax). Summed across all localities.
    /// </summary>
    public decimal LocalHeadTax { get; init; }

    /// <summary>Display label for the <see cref="LocalHeadTax"/> line item.</summary>
    public string LocalHeadTaxLabel { get; init; } = "Local Services Tax";

    /// <summary>
    /// Human-readable label describing the locality, e.g. "Philadelphia (PA EIT) + LST"
    /// or "NYC". Empty when no locality applies.
    /// </summary>
    public string LocalityLabel { get; init; } = string.Empty;

    /// <summary>
    /// Per-locality breakdown the UI may display. Empty when no locality applies.
    /// </summary>
    public IReadOnlyList<LocalWithholdingLine> LocalBreakdown { get; init; } = Array.Empty<LocalWithholdingLine>();

    public decimal TotalTaxes => StateWithholding + StateDisabilityInsurance
                                + SocialSecurityWithholding + MedicareWithholding + AdditionalMedicareWithholding
                                + FederalWithholding
                                + LocalWithholding + LocalHeadTax;
    public decimal NetPay { get; init; }

    // ── Drill-down explanations ────────────────────────────────
    // UI layers should surface these through ResultCardModel rather than
    // read raw tax-logic details. Any of these may be null if the calculation
    // path chose not to produce an explanation (e.g., zero-dollar line item).

    /// <summary>Explanation of how <see cref="FederalWithholding"/> was computed.</summary>
    public LineItemExplanation? FederalExplanation { get; init; }

    /// <summary>Explanation of how <see cref="SocialSecurityWithholding"/> was computed.</summary>
    public LineItemExplanation? SocialSecurityExplanation { get; init; }

    /// <summary>Explanation of how <see cref="MedicareWithholding"/> was computed.</summary>
    public LineItemExplanation? MedicareExplanation { get; init; }

    /// <summary>
    /// Explanation of how <see cref="AdditionalMedicareWithholding"/> was computed.
    /// <c>null</c> when Additional Medicare withholding is not triggered.
    /// </summary>
    public LineItemExplanation? AdditionalMedicareExplanation { get; init; }

    /// <summary>Explanation of how <see cref="StateWithholding"/> was computed.</summary>
    public LineItemExplanation? StateExplanation { get; init; }
}

/// <summary>One entry in <see cref="PaycheckResult.LocalBreakdown"/>.</summary>
public sealed record LocalWithholdingLine(
    string LocalityCode,
    string LocalityName,
    decimal TaxableWages,
    decimal Withholding,
    decimal HeadTax,
    string HeadTaxLabel,
    string? Description);
