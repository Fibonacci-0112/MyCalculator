using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class PennsylvaniaStateTaxCalculatorTest
{
    [Fact]
    public void State_ReturnsPennsylvania()
    {
        var calc = new PennsylvaniaStateTaxCalculator();
        Assert.Equal(UsState.PA, calc.State);
    }

    [Fact]
    public void FlatRate_AppliedToGrossWages()
    {
        var calc = new PennsylvaniaStateTaxCalculator();

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // 5000 * 0.0307 = 153.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(153.50m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new PennsylvaniaStateTaxCalculator();

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 1000m
        });

        // (5000 - 1000) * 0.0307 = 122.80
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(122.80m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = new PennsylvaniaStateTaxCalculator();

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 25m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // 5000 * 0.0307 + 25 = 178.50
        Assert.Equal(178.50m, result.Withholding);
    }

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new PennsylvaniaStateTaxCalculator();

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 0m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new PennsylvaniaStateTaxCalculator();

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 1000m,
            Frequency = PayFrequency.Monthly,
            FilingStatus = FilingStatus.Married,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 2000m
        });

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }
}
