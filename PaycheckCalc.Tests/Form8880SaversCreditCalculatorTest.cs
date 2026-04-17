using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="Form8880SaversCreditCalculator"/>. Verifies the 2026
/// AGI bands (Single 25k/27.25k/42k, HoH 37.5k/40.875k/63k, MFJ 50k/54.5k/84k)
/// and the 50%/20%/10%/0% rate selection, plus the $2,000 per-taxpayer cap.
/// </summary>
public class Form8880SaversCreditCalculatorTest
{
    private readonly Form8880SaversCreditCalculator _calc = new();

    [Fact]
    public void Single_Below25k_Gets50PercentRate()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 20_000m);

        Assert.Equal(0.50m, result.Rate);
        Assert.Equal(1_000m, result.Credit);
    }

    [Fact]
    public void Single_Between25kAnd27_25k_Gets20Percent()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 26_000m);

        Assert.Equal(0.20m, result.Rate);
        Assert.Equal(400m, result.Credit);
    }

    [Fact]
    public void Single_Between27_25kAnd42k_Gets10Percent()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 35_000m);

        Assert.Equal(0.10m, result.Rate);
        Assert.Equal(200m, result.Credit);
    }

    [Fact]
    public void Single_Above42k_NoCredit()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 45_000m);

        Assert.Equal(0m, result.Rate);
        Assert.Equal(0m, result.Credit);
    }

    [Fact]
    public void ContributionsCappedAt2000PerTaxpayer()
    {
        // Even if taxpayer contributed $5,000, only $2,000 counts.
        var input = new SaversCreditInput { TaxpayerContributions = 5_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 20_000m);

        Assert.Equal(2_000m, result.EligibleContributions);
        Assert.Equal(1_000m, result.Credit); // 50% × $2,000
    }

    [Fact]
    public void Mfj_BothSpousesContribute_EachGets2kCap()
    {
        // Both contribute $2,500 → each capped at $2,000 → $4,000 eligible.
        var input = new SaversCreditInput
        {
            TaxpayerContributions = 2_500m,
            SpouseContributions = 2_500m
        };

        var result = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 45_000m);

        Assert.Equal(4_000m, result.EligibleContributions);
        Assert.Equal(0.50m, result.Rate);
        Assert.Equal(2_000m, result.Credit);
    }

    [Fact]
    public void Mfj_Band50kTo54_5k_Gets20Percent()
    {
        var input = new SaversCreditInput
        {
            TaxpayerContributions = 2_000m,
            SpouseContributions = 2_000m
        };

        var result = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 52_000m);

        Assert.Equal(0.20m, result.Rate);
        Assert.Equal(800m, result.Credit); // 20% × $4,000
    }

    [Fact]
    public void Mfj_Band54_5kTo84k_Gets10Percent()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 70_000m);

        Assert.Equal(0.10m, result.Rate);
        Assert.Equal(200m, result.Credit);
    }

    [Fact]
    public void Mfj_Above84k_NoCredit()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 90_000m);

        Assert.Equal(0m, result.Credit);
    }

    [Fact]
    public void Hoh_Band37_5kTo40_875k_Gets20Percent()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.HeadOfHousehold, 39_000m);

        Assert.Equal(0.20m, result.Rate);
        Assert.Equal(400m, result.Credit);
    }

    [Fact]
    public void Hoh_Above63k_NoCredit()
    {
        var input = new SaversCreditInput { TaxpayerContributions = 2_000m };

        var result = _calc.Calculate(input, FederalFilingStatus.HeadOfHousehold, 65_000m);

        Assert.Equal(0m, result.Credit);
    }

    [Fact]
    public void SpouseContributionsIgnoredForNonMfj()
    {
        // Single filer: spouse contributions are ignored entirely.
        var input = new SaversCreditInput
        {
            TaxpayerContributions = 1_000m,
            SpouseContributions = 2_000m
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 20_000m);

        Assert.Equal(1_000m, result.EligibleContributions);
        Assert.Equal(500m, result.Credit);
    }

    [Fact]
    public void NoContributions_ProducesZero()
    {
        var result = _calc.Calculate(new SaversCreditInput(), FederalFilingStatus.SingleOrMarriedSeparately, 20_000m);
        Assert.Equal(0m, result.Credit);
    }
}
