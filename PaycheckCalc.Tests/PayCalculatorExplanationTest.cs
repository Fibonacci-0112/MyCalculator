using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Verifies that <see cref="PayCalculator"/> populates the per-line-item
/// drill-down explanations (FICA, federal, state) with the correct method,
/// table, and key inputs so the UI can surface them through
/// <c>ResultCardModel</c> without touching raw tax knowledge.
/// </summary>
public sealed class PayCalculatorExplanationTest
{
    private static PayCalculator BuildCalculator()
    {
        var stateRegistry = new StateCalculatorRegistry();
        foreach (var (state, config) in StateTaxConfigs2026.Configs)
            stateRegistry.Register(new PercentageMethodWithholdingAdapter(state, config));
        foreach (var state in new[] { UsState.AK, UsState.FL, UsState.NV, UsState.NH, UsState.SD, UsState.TN, UsState.TX, UsState.WA, UsState.WY })
            stateRegistry.Register(new NoIncomeTaxWithholdingAdapter(state));

        var fica = new FicaCalculator();
        var fed = new Irs15TPercentageCalculator(
            File.ReadAllText("us_irs_15t_2026_percentage_automated.json"));
        return new PayCalculator(stateRegistry, fica, fed);
    }

    [Fact]
    public void SocialSecurityExplanation_IsPopulated_WithRateAndWageBaseAndWages()
    {
        var calc = BuildCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 30m,
            RegularHours = 80m,
            State = UsState.VA,
            YtdSocialSecurityWages = 10_000m,
        });

        var ss = result.SocialSecurityExplanation;
        Assert.NotNull(ss);
        Assert.Equal("Social Security Tax (FICA)", ss!.Title);
        Assert.Contains("6.2%", ss.Method);
        Assert.Contains("$184,500", ss.Table);
        // Rate input
        Assert.Contains(ss.Inputs, i => i.Label == "Rate" && i.Value.Contains("6.2"));
        // YTD input reflects caller-supplied value
        Assert.Contains(ss.Inputs, i => i.Label == "YTD Social Security Wages" && i.Value.Contains("10,000"));
        // Withholding value echoes the rounded period amount
        Assert.Contains(ss.Inputs,
            i => i.Label == "Withholding This Period"
                 && i.Value == result.SocialSecurityWithholding.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void MedicareExplanation_IsPopulated_With145PercentRate()
    {
        var calc = BuildCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 30m,
            RegularHours = 80m,
            State = UsState.VA,
        });

        var medicare = result.MedicareExplanation;
        Assert.NotNull(medicare);
        Assert.Equal("Medicare Tax (FICA)", medicare!.Title);
        Assert.Contains("1.45%", medicare.Method);
        Assert.Contains(medicare.Inputs, i => i.Label == "Rate" && i.Value.Contains("1.45"));
    }

    [Fact]
    public void AdditionalMedicareExplanation_IsNull_WhenUnderThreshold()
    {
        var calc = BuildCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 30m,
            RegularHours = 80m,
            State = UsState.VA,
            // YTD Medicare wages well under $200,000 threshold.
            YtdMedicareWages = 10_000m,
        });

        Assert.Equal(0m, result.AdditionalMedicareWithholding);
        Assert.Null(result.AdditionalMedicareExplanation);
    }

    [Fact]
    public void AdditionalMedicareExplanation_IsPopulated_WhenOverThreshold()
    {
        var calc = BuildCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 200m,
            RegularHours = 80m,
            State = UsState.VA,
            YtdSocialSecurityWages = 200_000m, // already past SS cap
            YtdMedicareWages = 200_000m,       // at/above additional-Medicare threshold
        });

        Assert.True(result.AdditionalMedicareWithholding > 0m);
        var addl = result.AdditionalMedicareExplanation;
        Assert.NotNull(addl);
        Assert.Contains("0.9", addl!.Method);
        Assert.Contains(addl.Inputs, i => i.Label == "Employer Withholding Threshold");
    }

    [Fact]
    public void FederalExplanation_IsPopulated_WithPub15TWorksheet1AAndFilingStatus()
    {
        var calc = BuildCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 30m,
            RegularHours = 80m,
            State = UsState.VA,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.MarriedFilingJointly,
                Step2Checked = false,
                Step4cExtraWithholding = 25m,
            },
        });

        var fed = result.FederalExplanation;
        Assert.NotNull(fed);
        Assert.Equal("Federal Income Tax", fed!.Title);
        Assert.Contains("Publication 15-T", fed.Method);
        Assert.Contains("Worksheet 1A", fed.Method);
        Assert.Contains("Married Filing Jointly", fed.Table);
        Assert.Contains(fed.Inputs, i => i.Label == "Filing Status" && i.Value == "Married Filing Jointly");
        Assert.Contains(fed.Inputs, i => i.Label == "Pay Frequency" && i.Value == "Biweekly");
        Assert.Contains(fed.Inputs, i => i.Label == "W-4 Step 2 (Two Jobs) Checked" && i.Value == "No");
        Assert.Contains(fed.Inputs, i => i.Label == "W-4 Step 4(c) Extra Withholding (period)" && i.Value.Contains("25"));
    }

    [Fact]
    public void StateExplanation_FromPercentageMethodAdapter_ReportsAnnualizedMethodAndKeyInputs()
    {
        var calc = BuildCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 30m,
            RegularHours = 80m,
            State = UsState.VA,
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 1,
                ["AdditionalWithholding"] = 10m,
            },
        });

        var state = result.StateExplanation;
        Assert.NotNull(state);
        Assert.Equal("State Income Tax", state!.Title);
        Assert.Contains("Annualized percentage method", state.Method);
        Assert.Contains("VA", state.Table);
        Assert.Contains(state.Inputs, i => i.Label == "State" && i.Value == "VA");
        Assert.Contains(state.Inputs, i => i.Label == "Filing Status" && i.Value == "Single");
        Assert.Contains(state.Inputs, i => i.Label == "State Allowances" && i.Value == "1");
        Assert.Contains(state.Inputs, i => i.Label == "Extra Withholding (period)" && i.Value.Contains("10"));
    }

    [Fact]
    public void StateExplanation_FromNoIncomeTaxAdapter_SaysNoStateIncomeTax()
    {
        var calc = BuildCalculator();
        var result = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 30m,
            RegularHours = 80m,
            State = UsState.TX,
        });

        Assert.Equal(0m, result.StateWithholding);
        var state = result.StateExplanation;
        Assert.NotNull(state);
        Assert.Contains("No state income tax", state!.Method);
        Assert.Contains(state.Inputs, i => i.Label == "State" && i.Value == "TX");
        Assert.False(string.IsNullOrWhiteSpace(state.Note));
    }
}
