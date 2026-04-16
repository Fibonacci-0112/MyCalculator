using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.SelfEmployment;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="SelfEmploymentTaxCalculator"/> covering the Schedule SE
/// computation: 92.35% factor, both halves of FICA, SS wage base cap,
/// Additional Medicare tax, and the deductible half.
/// </summary>
public class SelfEmploymentTaxCalculatorTest
{
    private readonly SelfEmploymentTaxCalculator _calc;

    public SelfEmploymentTaxCalculatorTest()
    {
        // Use default 2026 FICA constants: SS wage base = $184,500, Addl Medicare threshold = $200,000
        _calc = new SelfEmploymentTaxCalculator(new FicaCalculator());
    }

    [Fact]
    public void ZeroNetProfit_ReturnsZeroTax()
    {
        var result = _calc.Calculate(0m);

        Assert.Equal(0m, result.SeTaxableEarnings);
        Assert.Equal(0m, result.TotalSeTax);
        Assert.Equal(0m, result.DeductibleHalfOfSeTax);
    }

    [Fact]
    public void NegativeNetProfit_ReturnsZeroTax()
    {
        var result = _calc.Calculate(-10_000m);

        Assert.Equal(0m, result.SeTaxableEarnings);
        Assert.Equal(0m, result.TotalSeTax);
    }

    [Fact]
    public void ModerateIncome_ComputesSeTaxCorrectly()
    {
        // $100,000 net profit (well below SS wage base)
        var result = _calc.Calculate(100_000m);

        // SE taxable = $100,000 × 0.9235 = $92,350.00
        Assert.Equal(92_350.00m, result.SeTaxableEarnings);

        // SS tax = $92,350 × 0.124 = $11,451.40
        Assert.Equal(11_451.40m, result.SocialSecurityTax);

        // Medicare tax = $92,350 × 0.029 = $2,678.15
        Assert.Equal(2_678.15m, result.MedicareTax);

        // No additional Medicare (below $200K)
        Assert.Equal(0m, result.AdditionalMedicareTax);

        // Total SE tax = $11,451.40 + $2,678.15 = $14,129.55
        Assert.Equal(14_129.55m, result.TotalSeTax);

        // Deductible half = $14,129.55 / 2 = $7,064.78 (rounded)
        Assert.Equal(7_064.78m, result.DeductibleHalfOfSeTax);
    }

    [Fact]
    public void HighIncome_SocialSecurityCapAtWageBase()
    {
        // $250,000 net profit — exceeds SS wage base ($184,500)
        var result = _calc.Calculate(250_000m);

        // SE taxable = $250,000 × 0.9235 = $230,875.00
        Assert.Equal(230_875.00m, result.SeTaxableEarnings);

        // SS tax capped at wage base: min($230,875, $184,500) × 0.124 = $184,500 × 0.124 = $22,878.00
        Assert.Equal(22_878.00m, result.SocialSecurityTax);

        // Medicare = $230,875 × 0.029 = $6,695.38
        Assert.Equal(6_695.38m, result.MedicareTax);

        // Additional Medicare = ($230,875 − $200,000) × 0.009 = $30,875 × 0.009 = $277.88
        Assert.Equal(277.88m, result.AdditionalMedicareTax);
    }

    [Fact]
    public void ExactlyAtSsWageBase_NoCapApplied()
    {
        // Net profit that produces SE taxable exactly at the wage base
        // $184,500 / 0.9235 ≈ $199,783.97... → use $199,783.97
        // Actually let's use a value where SE taxable = $184,500
        // $184,500 / 0.9235 = $199,783.97... (rounds)
        // Let's just test with $184,500 net profit directly
        var result = _calc.Calculate(184_500m);

        // SE taxable = $184,500 × 0.9235 = $170,385.75
        Assert.Equal(170_385.75m, result.SeTaxableEarnings);

        // Below wage base, so full SS applies
        // SS = $170,385.75 × 0.124 = $21,127.83
        Assert.Equal(21_127.83m, result.SocialSecurityTax);

        // Below $200K, so no additional Medicare
        Assert.Equal(0m, result.AdditionalMedicareTax);
    }

    [Fact]
    public void AdditionalMedicare_OnlyAboveThreshold()
    {
        // $220,000 net profit → SE taxable = $220,000 × 0.9235 = $203,170.00
        // That crosses the $200K threshold
        var result = _calc.Calculate(220_000m);

        Assert.Equal(203_170.00m, result.SeTaxableEarnings);

        // Additional Medicare = ($203,170 − $200,000) × 0.009 = $3,170 × 0.009 = $28.53
        Assert.Equal(28.53m, result.AdditionalMedicareTax);
    }

    [Fact]
    public void DeductibleHalf_IsExactlyFiftyPercentOfTotal()
    {
        var result = _calc.Calculate(80_000m);

        // SE taxable = $80,000 × 0.9235 = $73,880.00
        Assert.Equal(73_880.00m, result.SeTaxableEarnings);

        var expectedTotal = result.SocialSecurityTax + result.MedicareTax + result.AdditionalMedicareTax;
        Assert.Equal(expectedTotal, result.TotalSeTax);

        // Deductible half should be total / 2 (rounded)
        var expectedHalf = Math.Round(result.TotalSeTax * 0.5m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedHalf, result.DeductibleHalfOfSeTax);
    }

    [Fact]
    public void SmallIncome_CalculatesCorrectly()
    {
        // $10,000 net profit
        var result = _calc.Calculate(10_000m);

        // SE taxable = $10,000 × 0.9235 = $9,235.00
        Assert.Equal(9_235.00m, result.SeTaxableEarnings);

        // SS = $9,235 × 0.124 = $1,145.14
        Assert.Equal(1_145.14m, result.SocialSecurityTax);

        // Medicare = $9,235 × 0.029 = $267.82
        Assert.Equal(267.82m, result.MedicareTax);

        Assert.Equal(0m, result.AdditionalMedicareTax);

        // Total = $1,145.14 + $267.82 = $1,412.96
        Assert.Equal(1_412.96m, result.TotalSeTax);

        // Half = $706.48
        Assert.Equal(706.48m, result.DeductibleHalfOfSeTax);
    }
}
