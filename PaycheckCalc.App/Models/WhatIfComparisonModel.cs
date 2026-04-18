namespace PaycheckCalc.App.Models;

/// <summary>
/// One row in the What-If side-by-side comparison grid. Each row pairs a
/// labelled metric with the baseline and variant values and a pre-computed
/// delta string so XAML can bind without any converter math.
/// </summary>
public sealed class WhatIfRowModel
{
    public required string Label { get; init; }
    public required decimal Baseline { get; init; }
    public required decimal Variant { get; init; }

    public decimal Delta => Variant - Baseline;
    public bool IsImprovement => Delta > 0m;   // interpretation is per-row
    public bool IsWorse => Delta < 0m;

    /// <summary>Delta with sign, e.g. "+$1,234.56" or "-$800.00".</summary>
    public string DeltaFormatted
    {
        get
        {
            var sign = Delta >= 0 ? "+" : "-";
            var abs = Math.Abs(Delta);
            return $"{sign}{abs:C}";
        }
    }
}

/// <summary>
/// Presentation-ready What-If comparison. Callers populate
/// <see cref="Rows"/> with the labels they care about (refund, total tax,
/// AGI, etc.); the page binds directly to this shape with no converter
/// math.
/// </summary>
public sealed class WhatIfComparisonModel
{
    public required string BaselineLabel { get; init; } = "Baseline";
    public required string VariantLabel { get; init; } = "Variant";
    public IReadOnlyList<WhatIfRowModel> Rows { get; init; } = Array.Empty<WhatIfRowModel>();

    public bool HasBaseline { get; init; }
    public bool HasVariant { get; init; }
}
