using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="ChildTaxCreditCalculator"/> verifying OBBBA 2026 rules:
/// $2,200/child nonrefundable CTC, $1,700/child refundable ACTC cap, $500 ODC,
/// $200k/$400k AGI phase-out ($50 per $1,000), and the 15%-of-earned-income-
/// over-$2,500 ACTC ceiling.
/// Expected values are derived by hand from §24 and Form 8812 mechanics.
/// </summary>
public class ChildTaxCreditCalculatorTest
{
    private readonly ChildTaxCreditCalculator _calc = new();

    [Fact]
    public void NoChildren_NoOtherDependents_ProducesZero()
    {
        var result = _calc.Calculate(
            new ChildTaxCreditInput(),
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 50_000m,
            taxBeforeCtc: 5_000m);

        Assert.Equal(0m, result.NonrefundableApplied);
        Assert.Equal(0m, result.RefundableActc);
    }

    [Fact]
    public void TwoChildren_BelowPhaseout_AmpleTax_FullyNonrefundable()
    {
        // 2 qualifying children × $2,200 = $4,400. Tax = $10,000 (ample).
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 2,
            EarnedIncome = 60_000m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 60_000m,
            taxBeforeCtc: 10_000m);

        Assert.Equal(4_400m, result.CtcBeforePhaseout);
        Assert.Equal(0m, result.PhaseoutReduction);
        Assert.Equal(4_400m, result.NonrefundableApplied);
        Assert.Equal(0m, result.RefundableActc); // no tax leftover to convert
    }

    [Fact]
    public void OneChild_PlusOneOtherDependent_BelowPhaseout()
    {
        // 1 QC × $2,200 + 1 OD × $500 = $2,700
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 1,
            OtherDependents = 1,
            EarnedIncome = 50_000m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 50_000m,
            taxBeforeCtc: 10_000m);

        Assert.Equal(2_200m, result.CtcBeforePhaseout);
        Assert.Equal(500m, result.OdcBeforePhaseout);
        Assert.Equal(2_700m, result.NonrefundableApplied);
    }

    [Fact]
    public void Single_Above200kThreshold_PhasesOut()
    {
        // AGI $215,500 → excess $15,500 → rounded up to $16,000 → 16 × $50 = $800 reduction.
        // 2 QC × $2,200 = $4,400 → after phase-out = $3,600.
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 2,
            EarnedIncome = 215_500m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 215_500m,
            taxBeforeCtc: 40_000m);

        Assert.Equal(800m, result.PhaseoutReduction);
        Assert.Equal(3_600m, result.NonrefundableApplied);
    }

    [Fact]
    public void Single_AtExactThreshold_NoPhaseout()
    {
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 1,
            EarnedIncome = 200_000m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 200_000m,
            taxBeforeCtc: 40_000m);

        Assert.Equal(0m, result.PhaseoutReduction);
        Assert.Equal(2_200m, result.NonrefundableApplied);
    }

    [Fact]
    public void Mfj_Uses400kThreshold()
    {
        // AGI $410,001 → excess $10,001 → rounded up to $11,000 → 11 × $50 = $550 reduction.
        // 3 QC × $2,200 = $6,600 → after phase-out = $6,050.
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 3,
            EarnedIncome = 410_001m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.MarriedFilingJointly,
            adjustedGrossIncome: 410_001m,
            taxBeforeCtc: 80_000m);

        Assert.Equal(550m, result.PhaseoutReduction);
        Assert.Equal(6_050m, result.NonrefundableApplied);
    }

    [Fact]
    public void PhaseoutAboveCeiling_FullyEliminatesCredit()
    {
        // 1 QC × $2,200. Single threshold $200k. AGI $260k → excess $60k → 60 steps × $50 = $3,000.
        // 2,200 − 3,000 < 0 → clamped to 0.
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 1,
            EarnedIncome = 260_000m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 260_000m,
            taxBeforeCtc: 40_000m);

        Assert.Equal(0m, result.NonrefundableApplied);
        Assert.Equal(0m, result.RefundableActc);
    }

    [Fact]
    public void LowIncome_TriggersRefundableActc()
    {
        // 2 QC × $2,200 = $4,400 nonrefundable ceiling.
        // Tiny tax of $200 → $200 applied nonrefundably, $4,200 unused.
        // Earned income $20,000 → 15% × ($20k − $2.5k) = 15% × $17,500 = $2,625.
        // Per-child refundable cap = 2 × $1,700 = $3,400.
        // Refundable = min($2,625, $3,400, $4,200) = $2,625.
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 2,
            EarnedIncome = 20_000m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 20_000m,
            taxBeforeCtc: 200m);

        Assert.Equal(200m, result.NonrefundableApplied);
        Assert.Equal(2_625m, result.RefundableActc);
    }

    [Fact]
    public void RefundableActc_CappedAt1700PerChild()
    {
        // 1 QC, very high earned income, zero tax.
        // 15% × ($200k − $2.5k) = $29,625 but capped at $1,700.
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 1,
            EarnedIncome = 200_000m // just at threshold — no phaseout
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 200_000m,
            taxBeforeCtc: 0m);

        Assert.Equal(0m, result.NonrefundableApplied);
        Assert.Equal(1_700m, result.RefundableActc);
    }

    [Fact]
    public void NoEarnedIncome_NoRefundableActc()
    {
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 2,
            EarnedIncome = 0m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 10_000m,
            taxBeforeCtc: 0m);

        Assert.Equal(0m, result.RefundableActc);
    }

    [Fact]
    public void OdcIsNotRefundable()
    {
        // 0 QC, 2 OD × $500 = $1,000. Tax = $0. No refundable ODC.
        var input = new ChildTaxCreditInput
        {
            OtherDependents = 2,
            EarnedIncome = 50_000m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 50_000m,
            taxBeforeCtc: 0m);

        Assert.Equal(0m, result.NonrefundableApplied);
        Assert.Equal(0m, result.RefundableActc);
    }

    [Fact]
    public void PhaseoutRoundsExcessUpToNext1000()
    {
        // AGI $200,001 → excess $1 → rounded up to $1,000 → 1 × $50 = $50 reduction.
        var input = new ChildTaxCreditInput
        {
            QualifyingChildren = 1,
            EarnedIncome = 200_001m
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 200_001m,
            taxBeforeCtc: 40_000m);

        Assert.Equal(50m, result.PhaseoutReduction);
        Assert.Equal(2_150m, result.NonrefundableApplied);
    }
}
