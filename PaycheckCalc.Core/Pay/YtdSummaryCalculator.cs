using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Pay;

/// <summary>
/// Aggregates a list of <see cref="SavedPaycheck"/> records into a single
/// <see cref="YtdSummary"/> covering one calendar year. Stateless and safe
/// to register as a singleton.
/// </summary>
/// <remarks>
/// Filtering is by <see cref="SavedPaycheck.UpdatedAt"/>.Year so that renames
/// or edits performed in a new tax year surface in the dashboard for that
/// year. The dashboard's "YTD actual" tile is informational; the engine's
/// <see cref="AnnualProjectionCalculator"/> remains the source of truth for
/// year-end projections.
/// </remarks>
public sealed class YtdSummaryCalculator
{
    public YtdSummary Calculate(IEnumerable<SavedPaycheck> paychecks, int year)
    {
        ArgumentNullException.ThrowIfNull(paychecks);

        int count = 0;
        decimal gross = 0m;
        decimal taxes = 0m;
        decimal net = 0m;

        foreach (var p in paychecks)
        {
            if (p.UpdatedAt.Year != year) continue;

            count++;
            gross += p.Result.GrossPay;
            taxes += p.Result.TotalTaxes;
            net   += p.Result.NetPay;
        }

        return new YtdSummary
        {
            Year          = year,
            PaycheckCount = count,
            TotalGross    = R(gross),
            TotalTaxes    = R(taxes),
            TotalNet      = R(net)
        };
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
