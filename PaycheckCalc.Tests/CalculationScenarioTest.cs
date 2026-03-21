using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for the <see cref="CalculationScenario"/> domain DTO and
/// the overall domain/presentation separation pattern.
/// </summary>
public sealed class CalculationScenarioTest
{
    // ── CalculationScenario basic construction ─────────────────────

    [Fact]
    public void Scenario_WrapsInputAndResult()
    {
        var input = CreateSampleInput();
        var result = CreateSampleResult();

        var scenario = new CalculationScenario { Input = input, Result = result };

        Assert.Same(input, scenario.Input);
        Assert.Same(result, scenario.Result);
    }

    [Fact]
    public void Scenario_PreservesAllInputFields()
    {
        var input = CreateSampleInput();
        var scenario = new CalculationScenario
        {
            Input = input,
            Result = CreateSampleResult()
        };

        Assert.Equal(PayFrequency.Biweekly, scenario.Input.Frequency);
        Assert.Equal(25m, scenario.Input.HourlyRate);
        Assert.Equal(80m, scenario.Input.RegularHours);
        Assert.Equal(5m, scenario.Input.OvertimeHours);
        Assert.Equal(1.5m, scenario.Input.OvertimeMultiplier);
        Assert.Equal(UsState.OK, scenario.Input.State);
        Assert.Equal(FederalFilingStatus.SingleOrMarriedSeparately, scenario.Input.FederalW4.FilingStatus);
    }

    [Fact]
    public void Scenario_PreservesAllResultFields()
    {
        var result = CreateSampleResult();
        var scenario = new CalculationScenario
        {
            Input = CreateSampleInput(),
            Result = result
        };

        Assert.Equal(2187.50m, scenario.Result.GrossPay);
        Assert.Equal(1500.00m, scenario.Result.NetPay);
        Assert.Equal(100.00m, scenario.Result.FederalWithholding);
        Assert.Equal(135.63m, scenario.Result.SocialSecurityWithholding);
        Assert.Equal(31.72m, scenario.Result.MedicareWithholding);
        Assert.Equal(75.00m, scenario.Result.StateWithholding);
    }

    [Fact]
    public void Scenario_ResultTotalTaxesCalculated()
    {
        var scenario = new CalculationScenario
        {
            Input = CreateSampleInput(),
            Result = CreateSampleResult()
        };

        // TotalTaxes = State + SDI + SS + Medicare + AddlMedicare + Federal
        var expected = 75.00m + 0m + 135.63m + 31.72m + 0m + 100.00m;
        Assert.Equal(expected, scenario.Result.TotalTaxes);
    }

    // ── PaycheckResult domain model: TotalTaxes includes SDI ──────

    [Fact]
    public void PaycheckResult_TotalTaxes_IncludesDisabilityInsurance()
    {
        var result = new PaycheckResult
        {
            GrossPay = 5000m,
            FederalWithholding = 500m,
            StateWithholding = 200m,
            StateDisabilityInsurance = 65m,
            SocialSecurityWithholding = 310m,
            MedicareWithholding = 72.50m,
            AdditionalMedicareWithholding = 0m,
            NetPay = 3852.50m
        };

        Assert.Equal(500m + 200m + 65m + 310m + 72.50m, result.TotalTaxes);
    }

    // ── Domain model immutability (init-only) ─────────────────────

    [Fact]
    public void PaycheckInput_DeductionsDefaultToEmpty()
    {
        var input = new PaycheckInput();
        Assert.Empty(input.Deductions);
    }

    [Fact]
    public void PaycheckInput_StateInputValuesDefaultsToNull()
    {
        var input = new PaycheckInput();
        Assert.Null(input.StateInputValues);
    }

    [Fact]
    public void CalculationScenario_CanStoreWithStateInputValues()
    {
        var stateVals = new StateInputValues { ["FilingStatus"] = "Single", ["Allowances"] = 2 };
        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Monthly,
            HourlyRate = 30m,
            RegularHours = 160m,
            State = UsState.CA,
            StateInputValues = stateVals
        };

        var scenario = new CalculationScenario
        {
            Input = input,
            Result = CreateSampleResult()
        };

        Assert.Equal("Single", scenario.Input.StateInputValues!.GetValueOrDefault<string>("FilingStatus"));
        Assert.Equal(2, scenario.Input.StateInputValues!.GetValueOrDefault<int>("Allowances"));
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static PaycheckInput CreateSampleInput() => new()
    {
        Frequency = PayFrequency.Biweekly,
        HourlyRate = 25m,
        RegularHours = 80m,
        OvertimeHours = 5m,
        OvertimeMultiplier = 1.5m,
        State = UsState.OK,
        FederalW4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
        },
        Deductions = new[]
        {
            new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = 200m, ReducesStateTaxableWages = true },
            new Deduction { Name = "Roth", Type = DeductionType.PostTax, Amount = 100m }
        }
    };

    private static PaycheckResult CreateSampleResult() => new()
    {
        GrossPay = 2187.50m,
        PreTaxDeductions = 200m,
        PostTaxDeductions = 100m,
        State = UsState.OK,
        StateTaxableWages = 1987.50m,
        StateWithholding = 75.00m,
        StateDisabilityInsurance = 0m,
        SocialSecurityWithholding = 135.63m,
        MedicareWithholding = 31.72m,
        AdditionalMedicareWithholding = 0m,
        FederalTaxableIncome = 1987.50m,
        FederalWithholding = 100.00m,
        NetPay = 1500.00m
    };
}
