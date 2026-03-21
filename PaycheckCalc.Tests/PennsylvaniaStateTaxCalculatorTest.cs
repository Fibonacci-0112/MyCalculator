using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class PennsylvaniaWithholdingCalculatorTest
{
    [Fact]
    public void State_ReturnsPennsylvania()
    {
        var calc = new PennsylvaniaWithholdingCalculator();
        Assert.Equal(UsState.PA, calc.State);
    }

    [Fact]
    public void FlatRate_AppliedToGrossWages()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // 5000 * 0.0307 = 153.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(153.50m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        // (5000 - 1000) * 0.0307 = 122.80
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(122.80m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues { ["AdditionalWithholding"] = 25m };

        var result = calc.Calculate(context, values);

        // 5000 * 0.0307 + 25 = 178.50
        Assert.Equal(178.50m, result.Withholding);
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new PennsylvaniaWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.PA,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }
}
