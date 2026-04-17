namespace PaycheckCalc.Core.Models;

/// <summary>
/// Additional non-wage income items, most of which flow through Schedule 1
/// (Form 1040) Part I. Amounts are annual dollars.
/// Kept as a flat record for readability; the <c>Schedule1Calculator</c>
/// sums these into "Additional Income" feeding AGI.
/// </summary>
public sealed class OtherIncomeInput
{
    /// <summary>Form 1040 line 2b — taxable interest.</summary>
    public decimal TaxableInterest { get; init; }

    /// <summary>Form 1040 line 3b — ordinary dividends.</summary>
    public decimal OrdinaryDividends { get; init; }

    /// <summary>
    /// Form 1040 line 3a — qualified dividends. Included in ordinary dividends
    /// for AGI; tracked separately for future preferential-rate handling.
    /// </summary>
    public decimal QualifiedDividends { get; init; }

    /// <summary>Form 1040 line 7 — net capital gain or loss (Schedule D).</summary>
    public decimal CapitalGainOrLoss { get; init; }

    /// <summary>Schedule 1 line 7 — unemployment compensation.</summary>
    public decimal UnemploymentCompensation { get; init; }

    /// <summary>Schedule 1 line 1 — taxable refunds of state/local income taxes.</summary>
    public decimal TaxableStateLocalRefunds { get; init; }

    /// <summary>Form 1040 line 6b — taxable portion of Social Security benefits.</summary>
    public decimal TaxableSocialSecurity { get; init; }

    /// <summary>
    /// Schedule 1 line 8z — any other additional income not captured above
    /// (gambling winnings, prizes, jury duty, etc.). Free-form catch-all.
    /// </summary>
    public decimal OtherAdditionalIncome { get; init; }
}
