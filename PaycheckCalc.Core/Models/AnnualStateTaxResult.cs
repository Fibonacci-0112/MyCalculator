namespace PaycheckCalc.Core.Models;

/// <summary>
/// Result of the annual state/local income tax projection produced by
/// <c>AnnualStateTaxCalculator</c>. All amounts are annual dollars rounded
/// to cents.
///
/// This is an estimation layer built on top of the existing per-paycheck
/// state withholding engine (<c>StateCalculatorRegistry</c>): the calculator
/// runs each state's registered <c>IStateWithholdingCalculator</c> once at
/// <see cref="PayFrequency.Annual"/> frequency against an annualized
/// state-wages base so that a single year-end number falls out. It is a
/// projection, not an authoritative state return.
/// </summary>
public sealed class AnnualStateTaxResult
{
    /// <summary>The state of residence this projection applies to.</summary>
    public UsState State { get; init; }

    /// <summary>True when the residence state has no individual income tax.</summary>
    public bool IsNoIncomeTaxState { get; init; }

    /// <summary>
    /// Gross state wages used as the annualized input (typically sum of
    /// W-2 Box 16, falling back to Box 1 when Box 16 is not supplied).
    /// </summary>
    public decimal StateWages { get; init; }

    /// <summary>
    /// Estimated annual state income tax liability. This is the output of
    /// the state calculator run at annual frequency — i.e. the annual tax,
    /// not a per-period amount.
    /// </summary>
    public decimal StateIncomeTax { get; init; }

    /// <summary>
    /// Annual state disability / paid-family-leave insurance (e.g., CA SDI,
    /// CT FLI). Reported separately from the income tax because it is
    /// typically not refundable against state tax.
    /// </summary>
    public decimal StateDisabilityInsurance { get; init; }

    /// <summary>Display label for the disability/PFL line item.</summary>
    public string StateDisabilityInsuranceLabel { get; init; } = "State Disability Insurance";

    /// <summary>Sum of W-2 Box 17 state income tax withheld across all jobs.</summary>
    public decimal StateTaxWithheld { get; init; }

    /// <summary>
    /// Positive = state refund expected (overpaid),
    /// negative = state balance due (owe).
    /// Mirrors the federal <see cref="AnnualTaxResult.RefundOrOwe"/>
    /// convention.
    /// </summary>
    public decimal StateRefundOrOwe { get; init; }

    /// <summary>Optional descriptive note from the underlying state calculator.</summary>
    public string? Description { get; init; }

    /// <summary>A zero-valued result for states with no income tax or no wages.</summary>
    public static AnnualStateTaxResult Zero(UsState state, bool noIncomeTax = false) => new()
    {
        State = state,
        IsNoIncomeTaxState = noIncomeTax,
        Description = noIncomeTax ? "No state income tax" : null
    };
}
