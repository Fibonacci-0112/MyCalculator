namespace PaycheckCalc.Core.Models;

/// <summary>
/// Which channels the <c>WithholdingSuggestionCalculator</c> is allowed to
/// use when solving for the target refund/owe outcome.
///
/// The suggestion engine translates the adjustment needed to reach the
/// taxpayer's target into one or more of:
/// <list type="bullet">
///   <item>Extra per-paycheck federal withholding (W-4 Step 4c)</item>
///   <item>Extra per-paycheck state withholding</item>
///   <item>A remaining Form 1040-ES quarterly estimated-tax payment</item>
/// </list>
/// The chosen mode controls which fields of
/// <see cref="WithholdingSuggestionResult"/> are populated.
/// </summary>
public enum SuggestionAllocation
{
    /// <summary>Put the entire adjustment into extra federal withholding (W-4 4c).</summary>
    FederalWithholdingOnly,

    /// <summary>Put the entire adjustment into extra state withholding.</summary>
    StateWithholdingOnly,

    /// <summary>Put the entire adjustment into 1040-ES quarterly estimates.</summary>
    EstimatedPaymentsOnly,

    /// <summary>
    /// Prefer extra federal withholding if any pay periods remain; fall back
    /// to 1040-ES only when no pay periods remain. This is the default mode
    /// and matches IRS guidance that extra W-4 4c withholding is the simpler
    /// correction method when feasible.
    /// </summary>
    FederalFirstThenEstimates,

    /// <summary>
    /// Split the adjustment across federal + state + 1040-ES proportionally
    /// to each channel's <c>Weight</c> on the input. Channels whose weight
    /// is 0 or which have no remaining capacity (e.g. zero remaining pay
    /// periods) are skipped and their share is redistributed.
    /// </summary>
    Split
}

/// <summary>
/// What outcome the taxpayer is aiming for at year-end, encoded as a signed
/// <c>TargetRefundOrOwe</c> using the same convention as
/// <see cref="AnnualTaxResult.RefundOrOwe"/>:
/// <list type="bullet">
///   <item><c>+X</c> — target a refund of $X (e.g. <c>500m</c> → $500 refund)</item>
///   <item><c>0</c>  — target an exact break-even ($0 refund / $0 owed)</item>
///   <item><c>-X</c> — target owing $X at filing (e.g. <c>-300m</c> → "owe no more than $300")</item>
/// </list>
/// The "owe no more than $X" UX question is served by setting
/// <see cref="TargetRefundOrOwe"/> to <c>-X</c>; the engine hits that amount
/// exactly, which satisfies the &quot;no more than&quot; constraint.
/// </summary>
public sealed class WithholdingSuggestionTarget
{
    /// <summary>Signed desired refund/owe amount. See class summary for sign convention.</summary>
    public decimal TargetRefundOrOwe { get; init; }

    /// <summary>Convenience constructor for a $0 break-even target.</summary>
    public static WithholdingSuggestionTarget ZeroBalance() =>
        new() { TargetRefundOrOwe = 0m };

    /// <summary>Convenience constructor for a target refund of <paramref name="amount"/>.</summary>
    public static WithholdingSuggestionTarget Refund(decimal amount) =>
        new() { TargetRefundOrOwe = Math.Abs(amount) };

    /// <summary>
    /// Convenience constructor for a target of owing at most <paramref name="amount"/>
    /// (positive dollars). Stored as <c>-amount</c> internally.
    /// </summary>
    public static WithholdingSuggestionTarget OweNoMoreThan(decimal amount) =>
        new() { TargetRefundOrOwe = -Math.Abs(amount) };
}

/// <summary>
/// Input to <c>WithholdingSuggestionCalculator</c>.
///
/// The calculator is deliberately ignorant of where the projected figures
/// came from: callers can drive it from the per-paycheck
/// <see cref="AnnualProjection"/>, from the annual <see cref="AnnualTaxResult"/>,
/// or from their own externally-computed projection. All that matters is a
/// consistent sign convention — positive = refund, negative = owe.
/// </summary>
public sealed class WithholdingSuggestionInput
{
    /// <summary>
    /// The currently-projected federal refund/owe assuming NO additional
    /// withholding or estimated payments beyond what is already scheduled.
    /// Positive = refund expected, negative = balance due. Typically sourced
    /// from <see cref="AnnualTaxResult.RefundOrOwe"/> or derived from
    /// <see cref="AnnualProjection.OverUnderWithholding"/>.
    /// </summary>
    public decimal CurrentProjectedRefundOrOwe { get; init; }

    /// <summary>
    /// Currently-projected state refund/owe (same sign convention). Only
    /// meaningful when <see cref="Allocation"/> routes part of the
    /// adjustment through extra state withholding. Defaults to 0.
    /// </summary>
    public decimal CurrentProjectedStateRefundOrOwe { get; init; }

    /// <summary>Number of pay periods remaining in the year for federal withholding purposes.</summary>
    public int RemainingFederalPayPeriods { get; init; }

    /// <summary>
    /// Number of pay periods remaining in the year for state withholding. Typically
    /// equal to <see cref="RemainingFederalPayPeriods"/>; separate to support taxpayers
    /// with state-only side jobs or different pay schedules.
    /// </summary>
    public int RemainingStatePayPeriods { get; init; }

    /// <summary>
    /// Number of 1040-ES quarterly installments still available to absorb any
    /// remaining adjustment (e.g. 4 early in the year, 1 after Sep 15). Valid
    /// range is 0..4. Defaults to 0 — the caller must explicitly opt in to
    /// estimate payments.
    /// </summary>
    public int RemainingEstimatedPaymentPeriods { get; init; }

    /// <summary>The target outcome — see <see cref="WithholdingSuggestionTarget"/>.</summary>
    public WithholdingSuggestionTarget Target { get; init; } = WithholdingSuggestionTarget.ZeroBalance();

    /// <summary>Which channels the engine is allowed to use. Defaults to federal-first.</summary>
    public SuggestionAllocation Allocation { get; init; } = SuggestionAllocation.FederalFirstThenEstimates;

    /// <summary>
    /// Weight on the federal channel when <see cref="Allocation"/> is
    /// <see cref="SuggestionAllocation.Split"/>. Ignored otherwise. Default 1.
    /// </summary>
    public decimal FederalWeight { get; init; } = 1m;

    /// <summary>
    /// Weight on the state channel when <see cref="Allocation"/> is
    /// <see cref="SuggestionAllocation.Split"/>. Ignored otherwise. Default 0.
    /// </summary>
    public decimal StateWeight { get; init; }

    /// <summary>
    /// Weight on the 1040-ES channel when <see cref="Allocation"/> is
    /// <see cref="SuggestionAllocation.Split"/>. Ignored otherwise. Default 0.
    /// </summary>
    public decimal EstimatedPaymentWeight { get; init; }
}
