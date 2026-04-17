using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="Form8960NiitCalculator"/>. NIIT = 3.8% × min(NII, MAGI − threshold).
/// Statutory thresholds: $200k (Single/MFS/HoH folded) and $250k (MFJ).
/// </summary>
public class Form8960NiitCalculatorTest
{
    private readonly Form8960NiitCalculator _calc = new();

    [Fact]
    public void Single_BelowThreshold_NoTax()
    {
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = 50_000m };
        var tax = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 150_000m);
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void Single_MagiAboveThreshold_NiiSmaller_TaxesNii()
    {
        // AGI $250k → excess $50k over $200k. NII $10k → min = $10k.
        // Tax = 3.8% × $10,000 = $380.
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = 10_000m };
        var tax = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 250_000m);
        Assert.Equal(380m, tax);
    }

    [Fact]
    public void Single_MagiAboveThreshold_ExcessSmaller_TaxesExcess()
    {
        // AGI $210k → excess $10k. NII $40k → min = $10k.
        // Tax = 3.8% × $10,000 = $380.
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = 40_000m };
        var tax = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 210_000m);
        Assert.Equal(380m, tax);
    }

    [Fact]
    public void Mfj_Uses250kThreshold()
    {
        // AGI $300k MFJ → excess $50k. NII $30k → min = $30k.
        // Tax = 3.8% × $30,000 = $1,140.
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = 30_000m };
        var tax = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 300_000m);
        Assert.Equal(1_140m, tax);
    }

    [Fact]
    public void Mfj_At250kThreshold_NoTax()
    {
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = 30_000m };
        var tax = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 250_000m);
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void ZeroNii_NoTax()
    {
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = 0m };
        var tax = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 500_000m);
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void NegativeNii_NoTax()
    {
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = -5_000m };
        var tax = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 500_000m);
        Assert.Equal(0m, tax);
    }

    [Fact]
    public void ModifiedAgiOverride_IsRespected()
    {
        // Engine AGI only $150k; override pushes MAGI to $300k (e.g. FEIE add-back).
        var input = new NetInvestmentIncomeInput
        {
            NetInvestmentIncome = 10_000m,
            ModifiedAgiOverride = 300_000m
        };
        var tax = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 150_000m);
        // 3.8% × min($10k, $100k) = 3.8% × $10,000 = $380
        Assert.Equal(380m, tax);
    }

    [Fact]
    public void RoundsHalfAwayFromZero()
    {
        // 3.8% × $13 = $0.494 → rounds to $0.49 (away from zero keeps $0.49 since halfway not in play).
        // Pick numbers producing exactly $0.005 residual to exercise rounding.
        // 3.8% × $131.75 = $5.0065 → rounds to $5.01 (AwayFromZero from $5.005 would be $5.01).
        // Set NII=$131.75, MAGI=excess big enough.
        var input = new NetInvestmentIncomeInput { NetInvestmentIncome = 131.75m };
        var tax = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 500_000m);
        Assert.Equal(5.01m, tax);
    }
}
