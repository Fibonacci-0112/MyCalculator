namespace PaycheckCalc.Core.Models;

/// <summary>
/// Aggregated year-to-date totals computed from a user's saved paychecks.
/// Used by the dashboard landing page on both the Blazor and MAUI heads
/// to surface "real" YTD progress alongside the projection from
/// <see cref="AnnualProjection"/>.
/// </summary>
public sealed class YtdSummary
{
    /// <summary>Calendar year these totals cover.</summary>
    public int Year { get; init; }

    /// <summary>Number of saved paychecks that fell inside <see cref="Year"/>.</summary>
    public int PaycheckCount { get; init; }

    /// <summary>Sum of <see cref="PaycheckResult.GrossPay"/> across counted paychecks.</summary>
    public decimal TotalGross { get; init; }

    /// <summary>
    /// Sum of <see cref="PaycheckResult.TotalTaxes"/> across counted paychecks —
    /// federal + state + SDI + FICA + local income tax + local head tax. Uses
    /// the engine's computed total so the dashboard never drifts from the
    /// Results page.
    /// </summary>
    public decimal TotalTaxes { get; init; }

    /// <summary>Sum of <see cref="PaycheckResult.NetPay"/> across counted paychecks.</summary>
    public decimal TotalNet { get; init; }

    /// <summary>True when no saved paycheck fell inside <see cref="Year"/>.</summary>
    public bool IsEmpty => PaycheckCount == 0;
}
