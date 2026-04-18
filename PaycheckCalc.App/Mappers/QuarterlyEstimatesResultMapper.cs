using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps a domain <see cref="QuarterlyEstimatesResult"/> to a
/// presentation-ready <see cref="QuarterlyEstimatesCardModel"/> with
/// formatted per-quarter rows.
/// </summary>
public static class QuarterlyEstimatesResultMapper
{
    public static QuarterlyEstimatesCardModel Map(QuarterlyEstimatesResult r)
    {
        var rows = r.Installments
            .Select(i => new QuarterlyEstimateRowModel
            {
                Period = i.Period,
                DueDate = i.DueDate,
                Amount = i.Amount,
                CumulativeAmount = i.CumulativeAmount
            })
            .ToList();

        return new QuarterlyEstimatesCardModel
        {
            TaxYear = r.TaxYear,
            CurrentYearProjectedTax = r.CurrentYearProjectedTax,
            PriorYearTotalTax = r.PriorYearTotalTax,
            ExpectedWithholding = r.ExpectedWithholding,
            RequiredAnnualPayment = r.RequiredAnnualPayment,
            SafeHarborBasis = r.SafeHarborBasis,
            SafeHarborBasisDisplay = FormatBasis(r.SafeHarborBasis),
            TotalEstimatedPayments = r.TotalEstimatedPayments,
            EstimatesRequired = r.EstimatesRequired,
            Installments = rows
        };
    }

    private static string FormatBasis(SafeHarborBasis basis) => basis switch
    {
        SafeHarborBasis.NinetyPercentOfCurrentYear => "90% of current-year tax",
        SafeHarborBasis.OneHundredPercentOfPriorYear => "100% of prior-year tax",
        SafeHarborBasis.OneHundredTenPercentOfPriorYear => "110% of prior-year tax (high-income)",
        _ => basis.ToString()
    };
}
