namespace PaycheckCalc.Core.Models;

/// <summary>
/// Basis for the <see cref="QuarterlyEstimatesResult.RequiredAnnualPayment"/>.
/// The IRS 1040-ES worksheet chooses the smaller of 90% of current-year tax
/// and 100% (or 110% for higher-income taxpayers) of prior-year tax. When the
/// taxpayer did not supply prior-year information, the calculator falls back
/// to the 90% current-year rule alone.
/// </summary>
public enum SafeHarborBasis
{
    /// <summary>90% of the current-year projected total tax.</summary>
    NinetyPercentOfCurrentYear,

    /// <summary>100% of the prior-year total tax.</summary>
    OneHundredPercentOfPriorYear,

    /// <summary>110% of the prior-year total tax (high-income rule).</summary>
    OneHundredTenPercentOfPriorYear
}

/// <summary>
/// Prior-year figures the 1040-ES worksheet needs to pick a safe harbor. All
/// properties are optional (default 0); when the prior-year total tax is 0
/// the calculator simply uses 90% of the current year's tax.
/// </summary>
public sealed class PriorYearSafeHarborInput
{
    /// <summary>Prior-year Form 1040 line 24 total tax (after credits).</summary>
    public decimal PriorYearTotalTax { get; init; }

    /// <summary>
    /// Prior-year AGI. Only used to decide whether the 110% higher-income
    /// safe harbor applies (AGI &gt; $150,000, or &gt; $75,000 MFS).
    /// </summary>
    public decimal PriorYearAdjustedGrossIncome { get; init; }

    /// <summary>
    /// Whether the prior-year return covered a full 12-month tax year. The
    /// prior-year safe harbor is only available for full-year returns; if
    /// false, the calculator ignores prior-year info and uses 90% of CY.
    /// </summary>
    public bool PriorYearWasFullYear { get; init; } = true;
}

/// <summary>
/// A single 1040-ES installment.
/// </summary>
public sealed class QuarterlyEstimatePayment
{
    /// <summary>Label, e.g. "Q1".</summary>
    public required string Period { get; init; }

    /// <summary>Due date for the installment.</summary>
    public required DateOnly DueDate { get; init; }

    /// <summary>Dollar amount due this installment.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Running cumulative total through this installment.</summary>
    public required decimal CumulativeAmount { get; init; }
}

/// <summary>
/// Form 1040-ES quarterly-estimated-tax worksheet output. Positive
/// <see cref="TotalEstimatedPayments"/> means the taxpayer should send
/// quarterly payments of the listed amounts to avoid an underpayment
/// penalty; zero means expected withholding already satisfies the safe
/// harbor and no estimated payments are required.
/// </summary>
public sealed class QuarterlyEstimatesResult
{
    /// <summary>Tax year the installments apply to.</summary>
    public int TaxYear { get; init; }

    /// <summary>Current-year projected total tax (Form 1040 line 24).</summary>
    public decimal CurrentYearProjectedTax { get; init; }

    /// <summary>Prior-year total tax used for the safe harbor (0 when unavailable).</summary>
    public decimal PriorYearTotalTax { get; init; }

    /// <summary>Expected W-2/1099 federal withholding credited against the estimate.</summary>
    public decimal ExpectedWithholding { get; init; }

    /// <summary>Smallest safe-harbor amount for the full year.</summary>
    public decimal RequiredAnnualPayment { get; init; }

    /// <summary>Which safe harbor produced <see cref="RequiredAnnualPayment"/>.</summary>
    public SafeHarborBasis SafeHarborBasis { get; init; }

    /// <summary>
    /// Required annual payment minus expected withholding, floored at zero.
    /// This is the amount the taxpayer must send in via 1040-ES installments.
    /// </summary>
    public decimal TotalEstimatedPayments { get; init; }

    /// <summary>
    /// True when expected withholding already covers the safe harbor — i.e.
    /// <see cref="TotalEstimatedPayments"/> is zero.
    /// </summary>
    public bool EstimatesRequired => TotalEstimatedPayments > 0m;

    /// <summary>The four quarterly installments.</summary>
    public IReadOnlyList<QuarterlyEstimatePayment> Installments { get; init; } =
        Array.Empty<QuarterlyEstimatePayment>();

    public static readonly QuarterlyEstimatesResult Zero = new();
}
