using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Models;

/// <summary>
/// Single installment row on the Quarterly Estimates page.
/// </summary>
public sealed class QuarterlyEstimateRowModel
{
    public required string Period { get; init; }
    public required DateOnly DueDate { get; init; }
    public required decimal Amount { get; init; }
    public required decimal CumulativeAmount { get; init; }

    /// <summary>Localized, short-form due date for XAML binding (e.g. "Apr 15, 2026").</summary>
    public string DueDateDisplay => DueDate.ToString("MMM d, yyyy");
}

/// <summary>
/// Presentation-ready 1040-ES result for the Quarterly Estimates page.
/// Wraps the domain <see cref="QuarterlyEstimatesResult"/> with UI helpers
/// (basis display string, Q1–Q4 rows with formatted dates).
/// </summary>
public sealed class QuarterlyEstimatesCardModel
{
    public int TaxYear { get; init; }
    public decimal CurrentYearProjectedTax { get; init; }
    public decimal PriorYearTotalTax { get; init; }
    public decimal ExpectedWithholding { get; init; }
    public decimal RequiredAnnualPayment { get; init; }
    public SafeHarborBasis SafeHarborBasis { get; init; }
    public string SafeHarborBasisDisplay { get; init; } = "";
    public decimal TotalEstimatedPayments { get; init; }
    public bool EstimatesRequired { get; init; }
    public IReadOnlyList<QuarterlyEstimateRowModel> Installments { get; init; }
        = Array.Empty<QuarterlyEstimateRowModel>();

    public string StatusLabel => EstimatesRequired
        ? "Quarterly estimated payments required"
        : "Expected withholding already covers the safe harbor";

    /// <summary>True when the worksheet picked a prior-year safe harbor.</summary>
    public bool UsedPriorYearSafeHarbor =>
        SafeHarborBasis != Core.Models.SafeHarborBasis.NinetyPercentOfCurrentYear;
}
