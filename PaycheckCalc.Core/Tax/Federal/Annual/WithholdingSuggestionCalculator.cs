using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Phase 7 — Withholding suggestion engine.
///
/// Given a projected year-end refund/owe position and a target outcome
/// (e.g. &quot;$0 refund&quot;, &quot;$500 refund&quot;, &quot;owe no more than $X&quot;),
/// solves for one or more of:
/// <list type="bullet">
///   <item>Extra per-paycheck federal withholding (W-4 Step 4c)</item>
///   <item>Extra per-paycheck state withholding</item>
///   <item>A suggested 1040-ES quarterly payment (useful when SE income
///     has driven the shortfall)</item>
/// </list>
///
/// The math is intentionally simple and dollar-accurate:
/// <code>
///   needed = targetRefundOrOwe − currentProjectedRefundOrOwe
/// </code>
/// with the convention that positive = refund, negative = owe, so a
/// positive <c>needed</c> means the taxpayer must increase payments
/// (withhold more, send estimates), and a negative <c>needed</c> means
/// they are already over-paying relative to the target.
///
/// The engine is deliberately decoupled from the underlying projection
/// pipeline: callers pass in the currently-projected refund/owe plus the
/// remaining capacity per channel (pay periods, quarters). That keeps it
/// usable from both the per-paycheck flow (<see cref="AnnualProjection"/>)
/// and the annual Form 1040 flow (<see cref="AnnualTaxResult"/>) without
/// coupling to either.
///
/// This calculator does NOT modify tax withholding data itself — it only
/// recommends adjustments. The UI layer is responsible for presenting the
/// suggestion and letting the taxpayer apply it to their W-4/IT-2104/etc.
/// </summary>
public sealed class WithholdingSuggestionCalculator
{
    /// <summary>
    /// Compute a suggestion for the requested target.
    /// </summary>
    public WithholdingSuggestionResult Calculate(WithholdingSuggestionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var target = input.Target.TargetRefundOrOwe;
        var current = input.CurrentProjectedRefundOrOwe;

        // Positive needed = taxpayer must send MORE dollars to the IRS
        // between now and year-end. Negative needed = already over-paying.
        var needed = R(target - current);

        var notes = new List<string>();

        // ── Case A: already over-paying relative to target ───
        // W-4 Step 4c can only add withholding; we cannot solve a negative
        // adjustment here. Surface that plainly so the UI can route the
        // user to W-4 Step 3 (dependents) / Step 2 / filing status changes.
        if (needed < 0m)
        {
            notes.Add(
                "Projection already exceeds the target. Extra withholding (W-4 Step 4c) " +
                "can only add to payments; to reduce withholding, adjust W-4 Step 3 " +
                "(dependents/credits) or file a new W-4 with different allowances.");
            return new WithholdingSuggestionResult
            {
                TargetRefundOrOwe = target,
                CurrentProjectedRefundOrOwe = current,
                NeededAdjustment = needed,
                Allocation = input.Allocation,
                IsFeasible = false,
                IsReducingBaseWithholdingAdvised = true,
                UnallocatedAdjustment = needed, // negative residual
                Notes = notes
            };
        }

        // ── Case B: exactly on target already ────────────────
        if (needed == 0m)
        {
            notes.Add("Current projection already matches the target. No change recommended.");
            return new WithholdingSuggestionResult
            {
                TargetRefundOrOwe = target,
                CurrentProjectedRefundOrOwe = current,
                NeededAdjustment = 0m,
                Allocation = input.Allocation,
                IsFeasible = true,
                Notes = notes
            };
        }

        // ── Case C: need to add 'needed' dollars via the selected channels ──
        var remainingFed = Math.Max(0, input.RemainingFederalPayPeriods);
        var remainingState = Math.Max(0, input.RemainingStatePayPeriods);
        var remainingEs = Math.Clamp(input.RemainingEstimatedPaymentPeriods, 0, 4);

        decimal federalShare = 0m;
        decimal stateShare = 0m;
        decimal estimatesShare = 0m;

        switch (input.Allocation)
        {
            case SuggestionAllocation.FederalWithholdingOnly:
                federalShare = needed;
                break;

            case SuggestionAllocation.StateWithholdingOnly:
                stateShare = needed;
                break;

            case SuggestionAllocation.EstimatedPaymentsOnly:
                estimatesShare = needed;
                break;

            case SuggestionAllocation.FederalFirstThenEstimates:
                if (remainingFed > 0)
                {
                    federalShare = needed;
                }
                else if (remainingEs > 0)
                {
                    estimatesShare = needed;
                    notes.Add(
                        "No remaining pay periods; routing the entire adjustment through " +
                        "1040-ES quarterly estimated payments.");
                }
                else
                {
                    // Nowhere to put it — leave everything unallocated.
                    notes.Add(
                        "No remaining pay periods and no remaining 1040-ES quarters; " +
                        "the shortfall cannot be absorbed before year-end.");
                }
                break;

            case SuggestionAllocation.Split:
                (federalShare, stateShare, estimatesShare) = SplitAdjustment(
                    needed,
                    input.FederalWeight, remainingFed,
                    input.StateWeight, remainingState,
                    input.EstimatedPaymentWeight, remainingEs,
                    notes);
                break;
        }

        // ── Turn each channel's dollar share into a per-period amount ──
        var (extraFedPerPaycheck, totalFed, fedNote) = DividePerPeriod(
            federalShare, remainingFed, "federal withholding");
        if (fedNote is not null) notes.Add(fedNote);

        var (extraStatePerPaycheck, totalState, stateNote) = DividePerPeriod(
            stateShare, remainingState, "state withholding");
        if (stateNote is not null) notes.Add(stateNote);

        var (perQuarter, totalEs, esNote) = DividePerPeriod(
            estimatesShare, remainingEs, "1040-ES estimated payments");
        if (esNote is not null) notes.Add(esNote);

        var allocated = R(totalFed + totalState + totalEs);
        var unallocated = R(needed - allocated);

        // Rounding residual tolerance: anything within a penny is considered
        // fully solved.
        var feasible = Math.Abs(unallocated) <= 0.01m;

        return new WithholdingSuggestionResult
        {
            TargetRefundOrOwe = target,
            CurrentProjectedRefundOrOwe = current,
            NeededAdjustment = needed,
            Allocation = input.Allocation,

            ExtraPerPaycheckFederalWithholding = extraFedPerPaycheck,
            TotalExtraFederalWithholding = totalFed,

            ExtraPerPaycheckStateWithholding = extraStatePerPaycheck,
            TotalExtraStateWithholding = totalState,

            SuggestedQuarterlyEstimatedPayment = perQuarter,
            TotalSuggestedEstimatedPayments = totalEs,

            IsFeasible = feasible,
            IsReducingBaseWithholdingAdvised = false,
            UnallocatedAdjustment = feasible ? 0m : unallocated,
            Notes = notes
        };
    }

    /// <summary>
    /// Divide <paramref name="shareDollars"/> across <paramref name="periods"/>
    /// equal buckets. Returns the per-period dollar amount (rounded to cents)
    /// and the total actually allocated (per-period × periods — may differ
    /// from the input share by a sub-cent rounding remainder). Appends a
    /// note when the caller asked for a share but no capacity exists.
    /// </summary>
    private static (decimal perPeriod, decimal total, string? note) DividePerPeriod(
        decimal shareDollars, int periods, string channelLabel)
    {
        if (shareDollars <= 0m || periods <= 0)
        {
            if (shareDollars > 0m && periods <= 0)
            {
                return (0m, 0m,
                    $"Could not allocate ${shareDollars:0.00} to {channelLabel}: " +
                    "no remaining periods.");
            }
            return (0m, 0m, null);
        }

        var perPeriod = R(shareDollars / periods);
        var total = R(perPeriod * periods);
        return (perPeriod, total, null);
    }

    /// <summary>
    /// Proportional split across the three channels honoring each channel's
    /// weight and capacity. Channels with zero weight or zero capacity get
    /// nothing and their share is redistributed among the remaining
    /// weighted channels. If all channels are excluded, the shortfall is
    /// returned as zero-per-channel and the caller will report it as an
    /// unallocated residual.
    /// </summary>
    private static (decimal fed, decimal state, decimal es) SplitAdjustment(
        decimal needed,
        decimal fedWeight, int fedPeriods,
        decimal stateWeight, int statePeriods,
        decimal esWeight, int esPeriods,
        List<string> notes)
    {
        var fedEligible = fedWeight > 0m && fedPeriods > 0;
        var stateEligible = stateWeight > 0m && statePeriods > 0;
        var esEligible = esWeight > 0m && esPeriods > 0;

        // Sanitize weights: non-eligible channels contribute 0.
        var fw = fedEligible ? Math.Max(0m, fedWeight) : 0m;
        var sw = stateEligible ? Math.Max(0m, stateWeight) : 0m;
        var ew = esEligible ? Math.Max(0m, esWeight) : 0m;
        var totalWeight = fw + sw + ew;

        if (totalWeight <= 0m)
        {
            notes.Add(
                "Split allocation requested but no eligible channel has both a " +
                "positive weight and remaining capacity.");
            return (0m, 0m, 0m);
        }

        // Compute raw shares, then roll any rounding remainder into whichever
        // eligible channel carried the largest share so the three shares sum
        // to exactly `needed`.
        var fedShare = R(needed * fw / totalWeight);
        var stateShare = R(needed * sw / totalWeight);
        var esShareRaw = R(needed - fedShare - stateShare);
        // Guard against a negative residual from rounding that could happen
        // if fed+state overshot by a penny: push it back to the largest non-
        // zero eligible channel.
        var esShare = esEligible ? esShareRaw : 0m;
        if (!esEligible && esShareRaw != 0m)
        {
            if (stateShare >= fedShare) stateShare = R(stateShare + esShareRaw);
            else fedShare = R(fedShare + esShareRaw);
        }

        return (fedShare, stateShare, esShare);
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
