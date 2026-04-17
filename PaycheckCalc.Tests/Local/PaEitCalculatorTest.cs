using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class PaEitCalculatorTest
{
    private static readonly string SampleJson = """
    {
      "year": 2026,
      "localities": [
        { "psd": "510101", "name": "Philadelphia", "county": "Philadelphia", "residentRate": 0.0375, "nonResidentRate": 0.0344 },
        { "psd": "020101", "name": "Pittsburgh",   "county": "Allegheny",    "residentRate": 0.03,   "nonResidentRate": 0.01 }
      ]
    }
    """;

    private static CommonLocalWithholdingContext Context(decimal gross, bool isResident,
        PayFrequency freq = PayFrequency.Biweekly)
    {
        var common = new CommonWithholdingContext(UsState.PA, gross, freq, Year: 2026);
        return new CommonLocalWithholdingContext(common, null, null, isResident, PaEitCalculator.LocalityKey);
    }

    [Fact]
    public void ResidentRate_UsedWhenHigherThanWorkNonResident()
    {
        var calc = new PaEitCalculator(new PaEitRateTable(SampleJson));
        var values = new LocalInputValues
        {
            [PaEitCalculator.HomePsdKey] = "510101", // Philly resident 3.75%
            [PaEitCalculator.WorkPsdKey] = "020101"  // Pittsburgh non-resident 1.0%
        };

        var result = calc.Calculate(Context(5000m, isResident: true), values);

        // max(3.75%, 1.0%) = 3.75% → 5000 * 0.0375 = 187.50
        Assert.Equal(187.50m, result.Withholding);
        Assert.Equal("Philadelphia", result.LocalityName);
    }

    [Fact]
    public void WorkNonResidentRate_UsedWhenHigher()
    {
        var calc = new PaEitCalculator(new PaEitRateTable(SampleJson));
        var values = new LocalInputValues
        {
            [PaEitCalculator.HomePsdKey] = "020101", // Pittsburgh resident 3%
            [PaEitCalculator.WorkPsdKey] = "510101"  // Philadelphia non-resident 3.44%
        };

        var result = calc.Calculate(Context(5000m, isResident: false), values);

        // max(3%, 3.44%) = 3.44% → 5000 * 0.0344 = 172.00
        Assert.Equal(172.00m, result.Withholding);
    }

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new PaEitCalculator(new PaEitRateTable(SampleJson));
        var common = new CommonWithholdingContext(UsState.PA,
            GrossWages: 5000m, PayPeriod: PayFrequency.Biweekly, Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var ctx = new CommonLocalWithholdingContext(common, null, null, true, PaEitCalculator.LocalityKey);
        var values = new LocalInputValues
        {
            [PaEitCalculator.HomePsdKey] = "510101",
            [PaEitCalculator.WorkPsdKey] = "510101"
        };

        var result = calc.Calculate(ctx, values);

        // (5000 - 500) * 0.0375 = 168.75
        Assert.Equal(4500m, result.TaxableWages);
        Assert.Equal(168.75m, result.Withholding);
    }

    [Fact]
    public void AdditionalWithholding_Added()
    {
        var calc = new PaEitCalculator(new PaEitRateTable(SampleJson));
        var values = new LocalInputValues
        {
            [PaEitCalculator.HomePsdKey] = "510101",
            [PaEitCalculator.WorkPsdKey] = "510101",
            [PaEitCalculator.AdditionalWithholdingKey] = 10m
        };

        var result = calc.Calculate(Context(5000m, isResident: true), values);

        // 5000 * 0.0375 + 10 = 197.50
        Assert.Equal(197.50m, result.Withholding);
    }

    [Fact]
    public void Validate_RequiresAtLeastOnePsd()
    {
        var calc = new PaEitCalculator(new PaEitRateTable(SampleJson));
        var errors = calc.Validate(new LocalInputValues());
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_UnknownPsdProducesError()
    {
        var calc = new PaEitCalculator(new PaEitRateTable(SampleJson));
        var errors = calc.Validate(new LocalInputValues
        {
            [PaEitCalculator.HomePsdKey] = "999999"
        });
        Assert.Contains(errors, e => e.Contains("999999"));
    }
}
