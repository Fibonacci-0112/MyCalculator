namespace PaycheckCalc.Core.Tax.Local;

/// <summary>
/// Normalized result returned by every <see cref="ILocalWithholdingCalculator"/>.
/// Local taxes are <b>additive</b>: they do not reduce federal or state taxable wages.
/// </summary>
public sealed class LocalWithholdingResult
{
    /// <summary>Human-readable locality name shown in the UI (e.g. "Philadelphia", "NYC").</summary>
    public string LocalityName { get; init; } = string.Empty;

    /// <summary>Wages subject to this locality's income tax after its own deductions (if any).</summary>
    public decimal TaxableWages { get; init; }

    /// <summary>Local income-tax withholding (EIT, NYC, RITA/CCA, MD county surtax) for the pay period.</summary>
    public decimal Withholding { get; init; }

    /// <summary>
    /// Flat per-pay-period charges that are not percentage-based income tax
    /// (e.g. PA Local Services Tax $52/year cap, municipal head tax).
    /// Zero when the locality does not impose one.
    /// </summary>
    public decimal HeadTax { get; init; }

    /// <summary>
    /// Display label for the <see cref="HeadTax"/> line item.
    /// Defaults to "Local Services Tax" which matches the most common usage.
    /// </summary>
    public string HeadTaxLabel { get; init; } = "Local Services Tax";

    /// <summary>Optional human-readable note (e.g. "Act 32 resident rule applied", "Exempt — no tax due").</summary>
    public string? Description { get; init; }

    /// <summary>Empty (no-op) result for localities that do not tax.</summary>
    public static LocalWithholdingResult None { get; } = new();
}
