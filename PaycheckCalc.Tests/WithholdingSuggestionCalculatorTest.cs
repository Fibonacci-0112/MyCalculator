using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Regression tests for Phase 7 <see cref="WithholdingSuggestionCalculator"/>.
///
/// Expected dollar amounts are worked out directly from the target-versus-
/// current arithmetic described by W-4 Step 4c and 1040-ES, NOT by
/// recomputing with production helpers, per the repository test instructions.
/// </summary>
public class WithholdingSuggestionCalculatorTest
{
    private readonly WithholdingSuggestionCalculator _calc = new();

    // ── Scenario 1: "$0 refund" target while currently projected to owe $1,040
    // over 8 remaining biweekly paychecks. Needed adjustment = 0 − (−1,040)
    // = $1,040. Split across 8 paychecks → $130.00 extra per check.

    [Fact]
    public void ZeroBalanceTarget_CurrentlyOwing_SolvesViaExtraFederalPerPaycheck()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -1_040m,
            RemainingFederalPayPeriods = 8,
            Target = WithholdingSuggestionTarget.ZeroBalance()
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.False(result.IsReducingBaseWithholdingAdvised);
        Assert.Equal(1_040m, result.NeededAdjustment);
        Assert.Equal(130.00m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(1_040.00m, result.TotalExtraFederalWithholding);
        Assert.Equal(0m, result.ExtraPerPaycheckStateWithholding);
        Assert.Equal(0m, result.SuggestedQuarterlyEstimatedPayment);
        Assert.Equal(0m, result.UnallocatedAdjustment);
    }

    // ── Scenario 2: "$500 refund" target while currently projected to owe
    // $1,500. Needed adjustment = 500 − (−1,500) = $2,000 over 10 paychecks
    // → $200.00 extra per paycheck.

    [Fact]
    public void ExactRefundTarget_SolvesForShortfallAboveZero()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -1_500m,
            RemainingFederalPayPeriods = 10,
            Target = WithholdingSuggestionTarget.Refund(500m)
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(2_000m, result.NeededAdjustment);
        Assert.Equal(200.00m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(2_000.00m, result.TotalExtraFederalWithholding);
    }

    // ── Scenario 3: "owe no more than $300" while currently projected to owe
    // $900. Target = −300. Needed = −300 − (−900) = +$600 over 6 paychecks
    // → $100.00 extra per paycheck.

    [Fact]
    public void OweNoMoreThanTarget_SolvesWithPartialAdjustment()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -900m,
            RemainingFederalPayPeriods = 6,
            Target = WithholdingSuggestionTarget.OweNoMoreThan(300m)
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(-300m, result.TargetRefundOrOwe);
        Assert.Equal(600m, result.NeededAdjustment);
        Assert.Equal(100.00m, result.ExtraPerPaycheckFederalWithholding);
    }

    // ── Scenario 4: Already over-withheld. Currently projected $2,000 refund
    // but the taxpayer wants a $0 refund (too much was being withheld).
    // Needed adjustment = 0 − 2,000 = −$2,000. W-4 Step 4c cannot subtract,
    // so the engine must flag this as infeasible and recommend reducing
    // base withholding instead.

    [Fact]
    public void CurrentlyOverWithheld_RecommendsReducingBaseWithholding()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = 2_000m,
            RemainingFederalPayPeriods = 10,
            Target = WithholdingSuggestionTarget.ZeroBalance()
        };

        var result = _calc.Calculate(input);

        Assert.False(result.IsFeasible);
        Assert.True(result.IsReducingBaseWithholdingAdvised);
        Assert.Equal(-2_000m, result.NeededAdjustment);
        Assert.Equal(0m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(-2_000m, result.UnallocatedAdjustment);
        Assert.NotEmpty(result.Notes);
    }

    // ── Scenario 5: Already on target — no change recommended.

    [Fact]
    public void AlreadyOnTarget_NoChangeRecommended()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = 0m,
            RemainingFederalPayPeriods = 12,
            Target = WithholdingSuggestionTarget.ZeroBalance()
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(0m, result.NeededAdjustment);
        Assert.Equal(0m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(0m, result.TotalExtraFederalWithholding);
    }

    // ── Scenario 6: Zero remaining pay periods but 4 remaining 1040-ES
    // quarters. FederalFirstThenEstimates mode should fall through to
    // quarterly estimates. Needed = $2,000, 4 quarters → $500 per quarter.

    [Fact]
    public void NoRemainingPaychecks_FallsBackToQuarterlyEstimates()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -2_000m,
            RemainingFederalPayPeriods = 0,
            RemainingEstimatedPaymentPeriods = 4,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.FederalFirstThenEstimates
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(0m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(500.00m, result.SuggestedQuarterlyEstimatedPayment);
        Assert.Equal(2_000.00m, result.TotalSuggestedEstimatedPayments);
        Assert.Contains(result.Notes, n => n.Contains("1040-ES", StringComparison.Ordinal));
    }

    // ── Scenario 7: No paychecks AND no remaining quarters — infeasible.

    [Fact]
    public void NoRemainingCapacity_ReportsUnallocatedShortfall()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -1_000m,
            RemainingFederalPayPeriods = 0,
            RemainingEstimatedPaymentPeriods = 0,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.FederalFirstThenEstimates
        };

        var result = _calc.Calculate(input);

        Assert.False(result.IsFeasible);
        Assert.Equal(1_000m, result.NeededAdjustment);
        Assert.Equal(1_000m, result.UnallocatedAdjustment);
        Assert.Equal(0m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(0m, result.SuggestedQuarterlyEstimatedPayment);
    }

    // ── Scenario 8: State-only allocation. Needed $600 over 12 periods
    // → $50.00 extra state per paycheck.

    [Fact]
    public void StateOnlyAllocation_RoutesAdjustmentThroughState()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -600m,
            RemainingStatePayPeriods = 12,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.StateWithholdingOnly
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(0m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(50.00m, result.ExtraPerPaycheckStateWithholding);
        Assert.Equal(600.00m, result.TotalExtraStateWithholding);
    }

    // ── Scenario 9: Estimated-payments-only for SE income. Needed $4,000
    // over 4 quarters → $1,000 per quarter.

    [Fact]
    public void EstimatedPaymentsOnly_ForSelfEmploymentIncome()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -4_000m,
            RemainingEstimatedPaymentPeriods = 4,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.EstimatedPaymentsOnly
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(1_000.00m, result.SuggestedQuarterlyEstimatedPayment);
        Assert.Equal(4_000.00m, result.TotalSuggestedEstimatedPayments);
        Assert.Equal(0m, result.ExtraPerPaycheckFederalWithholding);
    }

    // ── Scenario 10: Split mode — 50% federal, 25% state, 25% estimates.
    // Needed $4,000 → $2,000 fed / $1,000 state / $1,000 estimates.
    // Federal: $2,000 / 10 = $200.00 per paycheck.
    // State:   $1,000 / 10 = $100.00 per paycheck.
    // Estimates: $1,000 / 4 = $250.00 per quarter.

    [Fact]
    public void SplitAllocation_DividesProportionallyAcrossAllChannels()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -4_000m,
            RemainingFederalPayPeriods = 10,
            RemainingStatePayPeriods = 10,
            RemainingEstimatedPaymentPeriods = 4,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.Split,
            FederalWeight = 2m,
            StateWeight = 1m,
            EstimatedPaymentWeight = 1m
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(200.00m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(100.00m, result.ExtraPerPaycheckStateWithholding);
        Assert.Equal(250.00m, result.SuggestedQuarterlyEstimatedPayment);
        Assert.Equal(2_000.00m, result.TotalExtraFederalWithholding);
        Assert.Equal(1_000.00m, result.TotalExtraStateWithholding);
        Assert.Equal(1_000.00m, result.TotalSuggestedEstimatedPayments);
    }

    // ── Scenario 11: Split mode skips ineligible channels (0 weight) and
    // redistributes across eligible channels. Fed weight 1, state weight 0:
    // entire $900 goes to federal. 9 paychecks → $100.00 each.

    [Fact]
    public void SplitAllocation_ZeroWeightChannelIsSkipped()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -900m,
            RemainingFederalPayPeriods = 9,
            RemainingStatePayPeriods = 9,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.Split,
            FederalWeight = 1m,
            StateWeight = 0m,
            EstimatedPaymentWeight = 0m
        };

        var result = _calc.Calculate(input);

        Assert.True(result.IsFeasible);
        Assert.Equal(100.00m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(0m, result.ExtraPerPaycheckStateWithholding);
        Assert.Equal(0m, result.SuggestedQuarterlyEstimatedPayment);
    }

    // ── Scenario 12: Rounding residual reported cleanly. $100 needed over
    // 3 paychecks → $33.33/period → $99.99 total. Unallocated $0.01 is
    // reported as a residual but the suggestion is still considered
    // feasible because the tolerance is ±$0.01.

    [Fact]
    public void PennyRoundingResidual_IsTreatedAsFeasible()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -100m,
            RemainingFederalPayPeriods = 3,
            Target = WithholdingSuggestionTarget.ZeroBalance()
        };

        var result = _calc.Calculate(input);

        Assert.Equal(33.33m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(99.99m, result.TotalExtraFederalWithholding);
        Assert.True(result.IsFeasible);
        Assert.Equal(0m, result.UnallocatedAdjustment);
    }

    // ── Scenario 13: Split allocation with no eligible channel surfaces the
    // shortfall as unallocated instead of silently swallowing it. All
    // weights are 0 — nothing eligible.

    [Fact]
    public void SplitAllocation_NoEligibleChannel_IsInfeasible()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -500m,
            RemainingFederalPayPeriods = 5,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.Split,
            FederalWeight = 0m,
            StateWeight = 0m,
            EstimatedPaymentWeight = 0m
        };

        var result = _calc.Calculate(input);

        Assert.False(result.IsFeasible);
        Assert.Equal(500m, result.UnallocatedAdjustment);
        Assert.NotEmpty(result.Notes);
    }

    // ── Scenario 14: FederalWithholdingOnly with zero remaining periods —
    // engine cannot route via federal and leaves the adjustment unallocated
    // (does NOT silently fall through to estimates, because caller pinned
    // the channel).

    [Fact]
    public void FederalOnly_NoPeriods_ReportsUnallocatedShortfall()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -400m,
            RemainingFederalPayPeriods = 0,
            RemainingEstimatedPaymentPeriods = 4, // available but not allowed
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.FederalWithholdingOnly
        };

        var result = _calc.Calculate(input);

        Assert.False(result.IsFeasible);
        Assert.Equal(400m, result.UnallocatedAdjustment);
        Assert.Equal(0m, result.ExtraPerPaycheckFederalWithholding);
        Assert.Equal(0m, result.SuggestedQuarterlyEstimatedPayment);
    }

    // ── Round-trip verification ──────────────────────────────────────
    //
    // Phase 9 priority: "apply suggestion → projected result hits target
    // within $1". Simulates the taxpayer applying the calculator's
    // suggestion for a full tax year and verifies that the new projected
    // refund/owe position lands at the target (within the ±$1 tolerance
    // from W-4 Step 4c penny rounding).
    //
    // New projection = current + totalExtraFederal + totalExtraState
    //                          + totalEstimatedPayments.

    private const decimal RoundTripToleranceDollars = 1m;

    private static decimal RoundTripProjection(
        WithholdingSuggestionInput input,
        WithholdingSuggestionResult result)
        => input.CurrentProjectedRefundOrOwe
           + result.TotalExtraFederalWithholding
           + result.TotalExtraStateWithholding
           + result.TotalSuggestedEstimatedPayments;

    [Fact]
    public void RoundTrip_ZeroBalanceFederalOnly_LandsOnTarget()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -1_040m,
            RemainingFederalPayPeriods = 8,
            Target = WithholdingSuggestionTarget.ZeroBalance()
        };

        var result = _calc.Calculate(input);
        var newProjection = RoundTripProjection(input, result);

        Assert.True(result.IsFeasible);
        // -1040 + 1040.00 = 0.00 → well within $1 of target (0).
        Assert.True(
            Math.Abs(newProjection - result.TargetRefundOrOwe) <= RoundTripToleranceDollars,
            $"Round-trip missed target: projected={newProjection}, target={result.TargetRefundOrOwe}");
        Assert.Equal(0m, newProjection);
    }

    [Fact]
    public void RoundTrip_Refund500Target_LandsOnTarget()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -1_500m,
            RemainingFederalPayPeriods = 10,
            Target = WithholdingSuggestionTarget.Refund(500m)
        };

        var result = _calc.Calculate(input);
        var newProjection = RoundTripProjection(input, result);

        Assert.True(result.IsFeasible);
        // -1500 + 2000.00 = 500.00 = target refund exactly.
        Assert.Equal(500m, newProjection);
        Assert.True(Math.Abs(newProjection - 500m) <= RoundTripToleranceDollars);
    }

    [Fact]
    public void RoundTrip_OweNoMoreThan300_LandsOnTarget()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -900m,
            RemainingFederalPayPeriods = 6,
            Target = WithholdingSuggestionTarget.OweNoMoreThan(300m)
        };

        var result = _calc.Calculate(input);
        var newProjection = RoundTripProjection(input, result);

        Assert.True(result.IsFeasible);
        // Target = owe $300 = refund-or-owe of −$300. −900 + 600 = −300.
        Assert.Equal(-300m, newProjection);
        Assert.True(Math.Abs(newProjection - (-300m)) <= RoundTripToleranceDollars);
    }

    [Fact]
    public void RoundTrip_StateOnlyAllocation_LandsOnTarget()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -600m,
            RemainingStatePayPeriods = 12,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.StateWithholdingOnly
        };

        var result = _calc.Calculate(input);
        var newProjection = RoundTripProjection(input, result);

        Assert.True(result.IsFeasible);
        // -600 + 600.00 (state) = 0 → on target.
        Assert.Equal(0m, newProjection);
    }

    [Fact]
    public void RoundTrip_EstimatedPaymentsOnly_LandsOnTarget()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -4_000m,
            RemainingEstimatedPaymentPeriods = 4,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.EstimatedPaymentsOnly
        };

        var result = _calc.Calculate(input);
        var newProjection = RoundTripProjection(input, result);

        Assert.True(result.IsFeasible);
        // -4000 + 4000 (4 × $1000 estimates) = 0.
        Assert.Equal(0m, newProjection);
    }

    [Fact]
    public void RoundTrip_SplitAllocation_LandsOnTarget()
    {
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -4_000m,
            RemainingFederalPayPeriods = 10,
            RemainingStatePayPeriods = 10,
            RemainingEstimatedPaymentPeriods = 4,
            Target = WithholdingSuggestionTarget.ZeroBalance(),
            Allocation = SuggestionAllocation.Split,
            FederalWeight = 2m,
            StateWeight = 1m,
            EstimatedPaymentWeight = 1m
        };

        var result = _calc.Calculate(input);
        var newProjection = RoundTripProjection(input, result);

        Assert.True(result.IsFeasible);
        // -4000 + 2000 + 1000 + 1000 = 0 exactly.
        Assert.Equal(0m, newProjection);
        Assert.True(Math.Abs(newProjection - result.TargetRefundOrOwe) <= RoundTripToleranceDollars);
    }

    [Fact]
    public void RoundTrip_PennyRoundingResidual_StaysWithinOneDollarTolerance()
    {
        // $100 across 3 paychecks → $33.33 × 3 = $99.99. Residual $0.01
        // leaves the projection one penny short of target but well inside
        // the Phase 9 ±$1 tolerance.
        var input = new WithholdingSuggestionInput
        {
            CurrentProjectedRefundOrOwe = -100m,
            RemainingFederalPayPeriods = 3,
            Target = WithholdingSuggestionTarget.ZeroBalance()
        };

        var result = _calc.Calculate(input);
        var newProjection = RoundTripProjection(input, result);

        Assert.True(result.IsFeasible);
        Assert.Equal(-0.01m, newProjection); // one-cent residual
        Assert.True(Math.Abs(newProjection - result.TargetRefundOrOwe) <= RoundTripToleranceDollars);
    }
}
