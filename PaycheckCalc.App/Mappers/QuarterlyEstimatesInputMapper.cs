using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Bundles the inputs <c>Form1040ESCalculator.Calculate</c> needs, sourced
/// from the shared <see cref="AnnualTaxSession"/>. Mappers stay pure — no
/// calls into any calculator — and return primitives the view-model can
/// feed directly to the calculator.
/// </summary>
public readonly record struct QuarterlyEstimatesMapped(
    int TaxYear,
    FederalFilingStatus FilingStatus,
    decimal CurrentYearProjectedTax,
    decimal ExpectedWithholding,
    PriorYearSafeHarborInput? PriorYear);

public static class QuarterlyEstimatesInputMapper
{
    /// <summary>
    /// Builds the inputs for <c>Form1040ESCalculator.Calculate</c>.
    /// If the session has a cached 1040 result the projected tax defaults
    /// from <c>TotalTax</c> and expected withholding from
    /// <c>FederalWithholdingFromW2s</c>; both are overridable by explicit
    /// page-level entries supplied via <paramref name="overrides"/>.
    /// </summary>
    public static QuarterlyEstimatesMapped Map(
        AnnualTaxSession s,
        QuarterlyEstimatesOverrides overrides)
    {
        var projectedTax = overrides.ProjectedTotalTax
            ?? s.ResultModel?.TotalTax
            ?? 0m;

        var withholdingFromW2 = s.ResultModel?.FederalWithholdingFromW2s
            ?? JobsAndYtdMapper.Summarize(s.W2Jobs).TotalFederalWithholding;

        var expectedWithholding = overrides.ExpectedWithholding
            ?? (withholdingFromW2 + Math.Max(0m, s.AdditionalExpectedWithholding));

        PriorYearSafeHarborInput? prior = s.UsePriorYearSafeHarbor
            ? new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = Math.Max(0m, s.PriorYearTotalTax),
                PriorYearAdjustedGrossIncome = Math.Max(0m, s.PriorYearAdjustedGrossIncome),
                PriorYearWasFullYear = s.PriorYearWasFullYear
            }
            : null;

        return new QuarterlyEstimatesMapped(
            s.TaxYear,
            s.FilingStatus,
            Math.Max(0m, projectedTax),
            Math.Max(0m, expectedWithholding),
            prior);
    }
}

/// <summary>
/// Page-level overrides entered by the user on the Quarterly Estimates
/// page. Nulls mean "use the session default" — the session's cached
/// 1040 result or summed W-2 withholding.
/// </summary>
public readonly record struct QuarterlyEstimatesOverrides(
    decimal? ProjectedTotalTax,
    decimal? ExpectedWithholding);
