using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class PaLstCalculatorTest
{
    private static CommonLocalWithholdingContext Ctx(PayFrequency freq)
    {
        var common = new CommonWithholdingContext(UsState.PA, 2500m, freq, Year: 2026);
        return new CommonLocalWithholdingContext(common, null, null, true, PaLstCalculator.LocalityKey);
    }

    [Fact]
    public void Biweekly_52Cap_ProratesTo2Dollars()
    {
        var calc = new PaLstCalculator();
        var values = new LocalInputValues { [PaLstCalculator.AnnualAmountKey] = 52m };

        var result = calc.Calculate(Ctx(PayFrequency.Biweekly), values);

        // 52 / 26 = 2.00
        Assert.Equal(2.00m, result.HeadTax);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Weekly_52Cap_ProratesToOneDollar()
    {
        var calc = new PaLstCalculator();
        var values = new LocalInputValues { [PaLstCalculator.AnnualAmountKey] = 52m };

        var result = calc.Calculate(Ctx(PayFrequency.Weekly), values);

        Assert.Equal(1.00m, result.HeadTax);
    }

    [Fact]
    public void Exempt_ReturnsZero()
    {
        var calc = new PaLstCalculator();
        var values = new LocalInputValues
        {
            [PaLstCalculator.AnnualAmountKey] = 52m,
            [PaLstCalculator.ExemptKey] = true
        };

        var result = calc.Calculate(Ctx(PayFrequency.Biweekly), values);
        Assert.Equal(0m, result.HeadTax);
    }

    [Fact]
    public void AmountAboveStatutoryCap_FailsValidation()
    {
        var calc = new PaLstCalculator();
        var errors = calc.Validate(new LocalInputValues
        {
            [PaLstCalculator.AnnualAmountKey] = 100m
        });
        Assert.Contains(errors, e => e.Contains("52", StringComparison.Ordinal));
    }

    [Fact]
    public void AmountAboveCap_ClampedAtRuntime()
    {
        var calc = new PaLstCalculator();
        var values = new LocalInputValues { [PaLstCalculator.AnnualAmountKey] = 200m };

        var result = calc.Calculate(Ctx(PayFrequency.Biweekly), values);

        // clamped to 52 then / 26 = 2.00
        Assert.Equal(2.00m, result.HeadTax);
    }
}
