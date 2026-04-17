using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class NycWithholdingCalculatorTest
{
    private static readonly string Json = """
    {
      "year": 2026,
      "statuses": {
        "Single": { "brackets": [
          { "min": 0, "rate": 0.03078 },
          { "min": 12000, "rate": 0.03762 },
          { "min": 25000, "rate": 0.03819 },
          { "min": 50000, "rate": 0.03876 }
        ] },
        "MarriedFilingJointly": { "brackets": [
          { "min": 0, "rate": 0.03078 },
          { "min": 21600, "rate": 0.03762 },
          { "min": 45000, "rate": 0.03819 },
          { "min": 90000, "rate": 0.03876 }
        ] },
        "HeadOfHousehold": { "brackets": [
          { "min": 0, "rate": 0.03078 },
          { "min": 14400, "rate": 0.03762 },
          { "min": 30000, "rate": 0.03819 },
          { "min": 60000, "rate": 0.03876 }
        ] }
      }
    }
    """;

    private static CommonLocalWithholdingContext Ctx(decimal gross, bool isResident,
        PayFrequency freq = PayFrequency.Biweekly)
    {
        var common = new CommonWithholdingContext(UsState.NY, gross, freq, Year: 2026);
        return new CommonLocalWithholdingContext(common, null, null, isResident,
            NycWithholdingCalculator.LocalityKey);
    }

    [Fact]
    public void NonResident_ReturnsZero()
    {
        var calc = new NycWithholdingCalculator(Json);
        var result = calc.Calculate(Ctx(10000m, isResident: false),
            new LocalInputValues { [NycWithholdingCalculator.FilingStatusKey] = "Single" });

        Assert.Equal(0m, result.Withholding);
        Assert.Equal(0m, result.TaxableWages);
    }

    [Fact]
    public void Resident_SingleBelowFirstBracketBoundary_UsesBaseRate()
    {
        // Biweekly gross $400 → annualized 10,400 → below $12,000 boundary → 3.078%
        var calc = new NycWithholdingCalculator(Json);
        var values = new LocalInputValues { [NycWithholdingCalculator.FilingStatusKey] = "Single" };

        var result = calc.Calculate(Ctx(400m, isResident: true), values);

        // Annual tax = 10,400 * 0.03078 = 320.112 → per-period = 320.112/26 = 12.312 → round 12.31
        Assert.Equal(12.31m, result.Withholding);
    }

    [Fact]
    public void Resident_SingleCrossingBracket_TaxesMarginally()
    {
        // Biweekly 1000 → annualized 26,000. 
        // 12,000 * 0.03078 + 13,000 * 0.03762 + 1,000 * 0.03819
        //   = 369.36 + 489.06 + 38.19 = 896.61
        // per-period = 896.61 / 26 = 34.48500 → round 34.49
        var calc = new NycWithholdingCalculator(Json);
        var values = new LocalInputValues { [NycWithholdingCalculator.FilingStatusKey] = "Single" };

        var result = calc.Calculate(Ctx(1000m, isResident: true), values);

        Assert.Equal(34.49m, result.Withholding);
    }

    [Fact]
    public void MfjBracketHigherThanSingle()
    {
        // Same wages, MFJ should use the wider brackets → lower tax than Single.
        var calc = new NycWithholdingCalculator(Json);
        var single = calc.Calculate(Ctx(1000m, true),
            new LocalInputValues { [NycWithholdingCalculator.FilingStatusKey] = "Single" });
        var mfj = calc.Calculate(Ctx(1000m, true),
            new LocalInputValues { [NycWithholdingCalculator.FilingStatusKey] = "MarriedFilingJointly" });

        Assert.True(mfj.Withholding < single.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_Added()
    {
        var calc = new NycWithholdingCalculator(Json);
        var values = new LocalInputValues
        {
            [NycWithholdingCalculator.FilingStatusKey] = "Single",
            [NycWithholdingCalculator.AdditionalWithholdingKey] = 5m
        };
        var baseline = calc.Calculate(Ctx(400m, true),
            new LocalInputValues { [NycWithholdingCalculator.FilingStatusKey] = "Single" });
        var withExtra = calc.Calculate(Ctx(400m, true), values);

        Assert.Equal(baseline.Withholding + 5m, withExtra.Withholding);
    }

    [Fact]
    public void UnknownStatus_FailsValidation()
    {
        var calc = new NycWithholdingCalculator(Json);
        var errors = calc.Validate(new LocalInputValues
        {
            [NycWithholdingCalculator.FilingStatusKey] = "NotReal"
        });
        Assert.Single(errors);
    }
}
