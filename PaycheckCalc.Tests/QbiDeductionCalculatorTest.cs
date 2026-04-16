using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.SelfEmployment;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="QbiDeductionCalculator"/> covering simplified Form 8995,
/// full Form 8995-A phase-out, SSTB rules, and W-2/UBIA limitations.
/// </summary>
public class QbiDeductionCalculatorTest
{
    private readonly QbiDeductionCalculator _calc = new();

    // ── Simplified path (below threshold) ───────────────────

    [Fact]
    public void ZeroQbi_ReturnsZero()
    {
        var deduction = _calc.Calculate(0m, 50_000m, FederalFilingStatus.SingleOrMarriedSeparately);
        Assert.Equal(0m, deduction);
    }

    [Fact]
    public void NegativeQbi_ReturnsZero()
    {
        var deduction = _calc.Calculate(-5_000m, 50_000m, FederalFilingStatus.SingleOrMarriedSeparately);
        Assert.Equal(0m, deduction);
    }

    [Fact]
    public void ZeroTaxableIncome_ReturnsZero()
    {
        var deduction = _calc.Calculate(50_000m, 0m, FederalFilingStatus.SingleOrMarriedSeparately);
        Assert.Equal(0m, deduction);
    }

    [Fact]
    public void SimplifiedPath_TwentyPercentOfQbi()
    {
        // $80,000 QBI, $100,000 taxable income (below $200K threshold)
        var deduction = _calc.Calculate(80_000m, 100_000m, FederalFilingStatus.SingleOrMarriedSeparately);

        // 20% of QBI = $16,000; 20% of taxable = $20,000; min = $16,000
        Assert.Equal(16_000m, deduction);
    }

    [Fact]
    public void SimplifiedPath_CappedByTaxableIncome()
    {
        // $150,000 QBI, $60,000 taxable income (below threshold)
        var deduction = _calc.Calculate(150_000m, 60_000m, FederalFilingStatus.SingleOrMarriedSeparately);

        // 20% of QBI = $30,000; 20% of taxable = $12,000; min = $12,000
        Assert.Equal(12_000m, deduction);
    }

    [Fact]
    public void SimplifiedPath_MFJ_BelowThreshold()
    {
        // $100,000 QBI, $300,000 taxable income (MFJ threshold is $400K)
        var deduction = _calc.Calculate(100_000m, 300_000m, FederalFilingStatus.MarriedFilingJointly);

        // 20% of QBI = $20,000; 20% of taxable = $60,000; min = $20,000
        Assert.Equal(20_000m, deduction);
    }

    [Fact]
    public void SimplifiedPath_HeadOfHousehold_UsesNonMfjThreshold()
    {
        // $80,000 QBI, $180,000 taxable income (HoH uses single threshold $200K)
        var deduction = _calc.Calculate(80_000m, 180_000m, FederalFilingStatus.HeadOfHousehold);

        // Below $200K → simplified: 20% of QBI = $16,000
        Assert.Equal(16_000m, deduction);
    }

    // ── Full path (above threshold) ─────────────────────────

    [Fact]
    public void NonSstb_AboveThreshold_WithW2Wages_AppliesW2Limit()
    {
        // $200,000 QBI, $300,000 taxable income (single, fully above $200K + $50K range)
        // W-2 wages = $60,000, UBIA = $100,000
        var deduction = _calc.Calculate(
            200_000m, 300_000m, FederalFilingStatus.SingleOrMarriedSeparately,
            isSstb: false, w2Wages: 60_000m, ubia: 100_000m);

        // 20% of QBI = $40,000
        // W-2 limit = max(50% × $60K = $30,000, 25% × $60K + 2.5% × $100K = $15,000 + $2,500 = $17,500) = $30,000
        // Fully above phase-in: min($40,000, $30,000) = $30,000
        // Taxable cap: 20% × $300K = $60,000
        // Final = min($30,000, $60,000) = $30,000
        Assert.Equal(30_000m, deduction);
    }

    [Fact]
    public void NonSstb_AboveThreshold_ZeroW2_ZeroUbia_GetsZero()
    {
        // Above phase-in range with no W-2 wages and no UBIA → W-2/UBIA limit = 0
        var deduction = _calc.Calculate(
            100_000m, 300_000m, FederalFilingStatus.SingleOrMarriedSeparately,
            isSstb: false, w2Wages: 0m, ubia: 0m);

        // 20% of QBI = $20,000, W-2/UBIA limit = $0 → min = $0
        Assert.Equal(0m, deduction);
    }

    // ── SSTB phase-out ──────────────────────────────────────

    [Fact]
    public void Sstb_FullyAbovePhaseIn_ReturnsZero()
    {
        // Single filer, $260,000 taxable (above $200K + $50K phase-in range)
        // SSTB → completely phased out
        var deduction = _calc.Calculate(
            100_000m, 260_000m, FederalFilingStatus.SingleOrMarriedSeparately,
            isSstb: true, w2Wages: 50_000m, ubia: 0m);

        Assert.Equal(0m, deduction);
    }

    [Fact]
    public void Sstb_BelowThreshold_FullDeduction()
    {
        // Single filer, $150,000 taxable (below $200K threshold)
        // SSTB flag doesn't matter below threshold
        var deduction = _calc.Calculate(
            80_000m, 150_000m, FederalFilingStatus.SingleOrMarriedSeparately,
            isSstb: true);

        // Simplified: 20% of QBI = $16,000
        Assert.Equal(16_000m, deduction);
    }

    [Fact]
    public void Sstb_WithinPhaseIn_PartialDeduction()
    {
        // Single filer, $225,000 taxable income
        // Excess = $225K − $200K = $25K; phase-out ratio = $25K / $50K = 0.50
        // SSTB: applicable QBI = $100,000 × (1 − 0.50) = $50,000
        // 20% of applicable QBI = $10,000
        // W-2 wages: $30K × 0.50 = $15K; UBIA: $0 × 0.50 = $0
        // W-2 limit: max(50%×$15K=$7,500, 25%×$15K+2.5%×$0=$3,750) = $7,500
        // Within phase-in blending:
        //   reduction = max($10,000 − $7,500, 0) = $2,500
        //   phased reduction = $2,500 × 0.50 = $1,250
        //   qbi component = $10,000 − $1,250 = $8,750
        // Taxable cap: 20% × $225K = $45,000
        // Final = min($8,750, $45,000) = $8,750
        var deduction = _calc.Calculate(
            100_000m, 225_000m, FederalFilingStatus.SingleOrMarriedSeparately,
            isSstb: true, w2Wages: 30_000m, ubia: 0m);

        Assert.Equal(8_750m, deduction);
    }

    [Fact]
    public void Sstb_MFJ_ThresholdIsHigher()
    {
        // MFJ, $380,000 taxable (below MFJ threshold of $400K)
        var deduction = _calc.Calculate(
            120_000m, 380_000m, FederalFilingStatus.MarriedFilingJointly,
            isSstb: true);

        // Below threshold → simplified: 20% of QBI = $24,000
        Assert.Equal(24_000m, deduction);
    }

    [Fact]
    public void Sstb_MFJ_FullyPhasedOut()
    {
        // MFJ, $510,000 taxable (above $400K + $100K phase-in)
        var deduction = _calc.Calculate(
            120_000m, 510_000m, FederalFilingStatus.MarriedFilingJointly,
            isSstb: true, w2Wages: 50_000m);

        Assert.Equal(0m, deduction);
    }
}
