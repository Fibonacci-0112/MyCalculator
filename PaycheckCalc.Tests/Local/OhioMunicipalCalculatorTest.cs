using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class OhioMunicipalCalculatorTest
{
    private static readonly string RitaJson = """
    {
      "year": 2026,
      "agency": "RITA",
      "munis": [
        { "code": "CLEV", "name": "Cleveland",  "rate": 0.025, "creditRate": 0.025, "creditCapRate": 1.0 },
        { "code": "LAKE", "name": "Lakewood",   "rate": 0.015, "creditRate": 0.005, "creditCapRate": 0.5 },
        { "code": "PARM", "name": "Parma",      "rate": 0.025, "creditRate": 0.01,  "creditCapRate": 1.0 }
      ]
    }
    """;

    private static CommonLocalWithholdingContext Ctx(decimal gross, bool isResident) =>
        new(new CommonWithholdingContext(UsState.OH, gross, PayFrequency.Biweekly, Year: 2026),
            HomeLocality: null, WorkLocality: null, IsResident: isResident,
            CurrentLocality: OhRitaCalculator.LocalityKey);

    [Fact]
    public void WorkMuniOnly_NonResident_PaysWorkRate()
    {
        var calc = new OhRitaCalculator(RitaJson);
        var values = new LocalInputValues { [OhioMunicipalCalculator.WorkMuniKey] = "CLEV" };

        var result = calc.Calculate(Ctx(2000m, isResident: false), values);

        // 2000 * 0.025 = 50.00
        Assert.Equal(50.00m, result.Withholding);
        Assert.Equal("Cleveland", result.LocalityName);
    }

    [Fact]
    public void ResidentOnly_NoWorkMuni_PaysResidentRate()
    {
        var calc = new OhRitaCalculator(RitaJson);
        var values = new LocalInputValues { [OhioMunicipalCalculator.ResidentMuniKey] = "CLEV" };

        var result = calc.Calculate(Ctx(2000m, isResident: true), values);

        // 2000 * 0.025 = 50.00
        Assert.Equal(50.00m, result.Withholding);
    }

    [Fact]
    public void Resident_WorksInSameMuni_NoCreditApplied()
    {
        var calc = new OhRitaCalculator(RitaJson);
        var values = new LocalInputValues
        {
            [OhioMunicipalCalculator.ResidentMuniKey] = "CLEV",
            [OhioMunicipalCalculator.WorkMuniKey] = "CLEV"
        };

        var result = calc.Calculate(Ctx(2000m, isResident: true), values);

        // Same-muni → single tax at 2.5% = 50.00
        Assert.Equal(50.00m, result.Withholding);
    }

    [Fact]
    public void Resident_WorksInOtherMuni_AppliesCredit()
    {
        var calc = new OhRitaCalculator(RitaJson);
        var values = new LocalInputValues
        {
            [OhioMunicipalCalculator.ResidentMuniKey] = "PARM", // resident rate 2.5%, credit 1%, cap 100%
            [OhioMunicipalCalculator.WorkMuniKey] = "CLEV"      // work rate 2.5%
        };

        var result = calc.Calculate(Ctx(2000m, isResident: true), values);

        // work tax = 2000 * 0.025 = 50
        // resident tax = 2000 * 0.025 = 50
        // credit = min(2000 * 0.01, 50 * 1.0) = min(20, 50) = 20
        // resident portion after credit = 50 - 20 = 30
        // total withheld = work + resident-portion = 50 + 30 = 80.00
        Assert.Equal(80.00m, result.Withholding);
    }

    [Fact]
    public void Resident_WorksInOtherMuni_CreditCapsAtMaxCreditRate()
    {
        var calc = new OhRitaCalculator(RitaJson);
        var values = new LocalInputValues
        {
            [OhioMunicipalCalculator.ResidentMuniKey] = "LAKE", // rate 1.5%, credit 0.5%, cap 50%
            [OhioMunicipalCalculator.WorkMuniKey] = "CLEV"       // rate 2.5%
        };

        var result = calc.Calculate(Ctx(2000m, isResident: true), values);

        // work tax = 2000 * 0.025 = 50
        // resident tax = 2000 * 0.015 = 30
        // credit = min(2000 * 0.005, 50 * 0.5) = min(10, 25) = 10
        // resident portion = 30 - 10 = 20
        // total = 50 + 20 = 70.00
        Assert.Equal(70.00m, result.Withholding);
    }

    [Fact]
    public void NoMuniSupplied_ReturnsZero()
    {
        var calc = new OhRitaCalculator(RitaJson);
        var result = calc.Calculate(Ctx(2000m, true), new LocalInputValues());
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Validate_RequiresAtLeastOneMuni()
    {
        var calc = new OhRitaCalculator(RitaJson);
        var errors = calc.Validate(new LocalInputValues());
        Assert.NotEmpty(errors);
    }
}
