using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class MdCountyCalculatorTest
{
    private static readonly string Json = """
    {
      "year": 2026,
      "counties": [
        { "code": "MONT", "name": "Montgomery", "rate": 0.032 },
        { "code": "WORC", "name": "Worcester",  "rate": 0.0225 },
        { "code": "NONRESIDENT", "name": "Non-resident Special Rate", "rate": 0.0225 }
      ]
    }
    """;

    private static CommonLocalWithholdingContext Ctx(decimal gross) =>
        new(new CommonWithholdingContext(UsState.MD, gross, PayFrequency.Biweekly, Year: 2026),
            null, null, true, MdCountyCalculator.LocalityKey);

    [Fact]
    public void Montgomery_3_2Percent()
    {
        var calc = new MdCountyCalculator(Json);
        var values = new LocalInputValues { [MdCountyCalculator.CountyKey] = "MONT" };

        var result = calc.Calculate(Ctx(2500m), values);

        // 2500 * 0.032 = 80.00
        Assert.Equal(80.00m, result.Withholding);
        Assert.Equal("Montgomery", result.LocalityName);
    }

    [Fact]
    public void Worcester_LowestRate()
    {
        var calc = new MdCountyCalculator(Json);
        var values = new LocalInputValues { [MdCountyCalculator.CountyKey] = "WORC" };

        var result = calc.Calculate(Ctx(2500m), values);

        // 2500 * 0.0225 = 56.25
        Assert.Equal(56.25m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new MdCountyCalculator(Json);
        var common = new CommonWithholdingContext(UsState.MD, 2500m, PayFrequency.Biweekly,
            Year: 2026, PreTaxDeductionsReducingStateWages: 500m);
        var ctx = new CommonLocalWithholdingContext(common, null, null, true, MdCountyCalculator.LocalityKey);
        var values = new LocalInputValues { [MdCountyCalculator.CountyKey] = "MONT" };

        var result = calc.Calculate(ctx, values);

        // (2500 - 500) * 0.032 = 64.00
        Assert.Equal(2000m, result.TaxableWages);
        Assert.Equal(64.00m, result.Withholding);
    }

    [Fact]
    public void UnknownCounty_FailsValidation()
    {
        var calc = new MdCountyCalculator(Json);
        var errors = calc.Validate(new LocalInputValues { [MdCountyCalculator.CountyKey] = "XXXX" });
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void MissingCounty_FailsValidation()
    {
        var calc = new MdCountyCalculator(Json);
        var errors = calc.Validate(new LocalInputValues());
        Assert.NotEmpty(errors);
    }
}
