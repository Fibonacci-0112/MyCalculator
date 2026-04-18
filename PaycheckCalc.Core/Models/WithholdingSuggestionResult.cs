namespace PaycheckCalc.Core.Models;

/// <summary>
/// Output of <c>WithholdingSuggestionCalculator</c>. Each channel is zero
/// unless the chosen <see cref="SuggestionAllocation"/> routed some of the
/// required adjustment through it.
///
/// Sign/convention notes:
/// <list type="bullet">
///   <item>Positive dollar amounts mean &quot;take this additional action&quot;
///     (withhold extra, send estimate).</item>
///   <item>A negative <see cref="NeededAdjustment"/> means the taxpayer is
///     currently projected to over-pay relative to their target. In that
///     case the engine cannot solve with extra withholding or 1040-ES
///     (W-4 Step 4c only adds), so the returned per-paycheck extras are $0
///     and <see cref="IsReducingBaseWithholdingAdvised"/> is true.</item>
/// </list>
/// </summary>
public sealed class WithholdingSuggestionResult
{
    // ── Echo of the scenario ────────────────────────────────

    /// <summary>Target refund/owe the caller asked to hit (signed).</summary>
    public decimal TargetRefundOrOwe { get; init; }

    /// <summary>Current projected federal refund/owe before applying this suggestion.</summary>
    public decimal CurrentProjectedRefundOrOwe { get; init; }

    /// <summary>
    /// Total dollar adjustment required to move the projection to the target.
    /// Equals <c>TargetRefundOrOwe − CurrentProjectedRefundOrOwe</c> flipped
    /// so that positive means &quot;increase payments by this much&quot;.
    /// </summary>
    public decimal NeededAdjustment { get; init; }

    /// <summary>Allocation mode that was applied.</summary>
    public SuggestionAllocation Allocation { get; init; }

    // ── Per-channel suggestions ─────────────────────────────

    /// <summary>
    /// Extra per-paycheck federal withholding to enter on W-4 Step 4c.
    /// Rounded to cents (taxpayers can enter dollars-and-cents on W-4).
    /// Always &gt;= 0 — the engine never recommends negative extra WH.
    /// </summary>
    public decimal ExtraPerPaycheckFederalWithholding { get; init; }

    /// <summary>
    /// Total dollars the extra federal WH suggestion will add across the
    /// remaining pay periods (per-paycheck × RemainingFederalPayPeriods).
    /// </summary>
    public decimal TotalExtraFederalWithholding { get; init; }

    /// <summary>
    /// Extra per-paycheck state withholding. State-specific forms differ
    /// (e.g., IT-2104, DE W-4, etc.) so the engine reports a generic
    /// dollar amount and leaves form routing to the UI layer.
    /// </summary>
    public decimal ExtraPerPaycheckStateWithholding { get; init; }

    /// <summary>
    /// Total extra state withholding across the remaining state pay periods.
    /// </summary>
    public decimal TotalExtraStateWithholding { get; init; }

    /// <summary>
    /// Suggested amount to send per remaining 1040-ES quarterly installment
    /// (for SE income or any shortfall that cannot be absorbed by extra WH).
    /// Zero when no estimates are needed/allowed for this scenario.
    /// </summary>
    public decimal SuggestedQuarterlyEstimatedPayment { get; init; }

    /// <summary>
    /// Total 1040-ES dollars across the remaining quarters (per-quarter ×
    /// RemainingEstimatedPaymentPeriods).
    /// </summary>
    public decimal TotalSuggestedEstimatedPayments { get; init; }

    // ── Diagnostics ─────────────────────────────────────────

    /// <summary>
    /// True when the suggested actions, when applied, are expected to hit
    /// the target exactly (within rounding). False when the engine could
    /// not fully solve — e.g. zero remaining pay periods with federal-only
    /// allocation, or a negative needed adjustment (already over-paying).
    /// </summary>
    public bool IsFeasible { get; init; }

    /// <summary>
    /// True when the current projection already over-pays relative to the
    /// target (i.e. <see cref="NeededAdjustment"/> is negative). W-4 Step
    /// 4c cannot subtract; the taxpayer should reduce base withholding
    /// via W-4 Step 3 dependents or a different filing status/allowances.
    /// </summary>
    public bool IsReducingBaseWithholdingAdvised { get; init; }

    /// <summary>
    /// Residual dollar amount the suggestion could NOT absorb. Zero when
    /// <see cref="IsFeasible"/> is true. Positive means more action is
    /// still needed beyond what the selected channels could handle.
    /// </summary>
    public decimal UnallocatedAdjustment { get; init; }

    /// <summary>
    /// Human-readable diagnostic notes (e.g. &quot;no remaining pay periods&quot;,
    /// &quot;already over-withheld&quot;). Useful for UI tooltips and CSV/PDF
    /// export. Never null; empty when everything cleanly solved.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
