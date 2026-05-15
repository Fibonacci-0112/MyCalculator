using PaycheckCalc.Core.Explanation;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Verifies that <see cref="PayCalculator"/> assembles a complete
/// <see cref="PaycheckExplanation"/> alongside each <see cref="PaycheckResult"/>.
/// </summary>
public sealed class PayCalculatorExplanationTest
{
    [Fact]
    public void Result_CarriesNonEmptyExplanation()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.NotNull(result.Explanation);
        Assert.NotEmpty(result.Explanation.Lines);
    }

    [Fact]
    public void Explanation_IncludesGrossFederalSocialSecurityMedicareStateAndNet()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.GrossPay));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.FederalWithholding));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.SocialSecurity));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.Medicare));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.StateWithholding));
        Assert.NotNull(result.Explanation.Get(ExplanationLineKey.NetPay));
    }

    [Fact]
    public void AdditionalMedicare_ExplanationOmitted_WhenZero()
    {
        // Normal wages ($1,440 biweekly) never cross the $200k threshold, so the
        // Additional Medicare line is zero and its explanation should be omitted.
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        Assert.Equal(0m, result.AdditionalMedicareWithholding);
        Assert.Null(result.Explanation.Get(ExplanationLineKey.AdditionalMedicare));
    }

    [Fact]
    public void FederalExplanation_FinalAmount_MatchesResultLine()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var federalExpl = result.Explanation.Get(ExplanationLineKey.FederalWithholding);
        Assert.NotNull(federalExpl);
        Assert.Equal(result.FederalWithholding, federalExpl!.FinalAmount);
    }

    [Fact]
    public void SocialSecurityExplanation_FinalAmount_MatchesResultLine()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var ssExpl = result.Explanation.Get(ExplanationLineKey.SocialSecurity);
        Assert.NotNull(ssExpl);
        Assert.Equal(result.SocialSecurityWithholding, ssExpl!.FinalAmount);
    }

    [Fact]
    public void StateExplanation_ContainsTaxableWagesStep()
    {
        // Generic fallback: even though Oklahoma's calculator doesn't yet opt in
        // to a custom explanation, PayCalculator builds a wage-base + withholding
        // breakdown from the StateWithholdingResult.
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var stateExpl = result.Explanation.Get(ExplanationLineKey.StateWithholding);
        Assert.NotNull(stateExpl);
        Assert.Contains(stateExpl!.Steps, s => s.Label == "State taxable wages");
    }

    [Fact]
    public void NetExplanation_AggregatesGrossLessDeductionsLessTaxes()
    {
        var calc = CreateCalculator();
        var result = calc.Calculate(SampleInput());

        var netExpl = result.Explanation.Get(ExplanationLineKey.NetPay);
        Assert.NotNull(netExpl);
        var netStep = Assert.Single(netExpl!.Steps, s => s.Label == "Net pay");
        Assert.Equal(result.NetPay, netStep.Value);
    }

    private static PaycheckInput SampleInput() => new()
    {
        Frequency = PayFrequency.Biweekly,
        HourlyRate = 18m,
        RegularHours = 80m,
        State = UsState.OK,
        FederalW4 = new FederalW4Input
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            Step2Checked = true
        },
        StateInputValues = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        }
    };

    private static PayCalculator CreateCalculator()
    {
        var registry = new StateCalculatorRegistry();
        var okJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json"));
        registry.Register(new OklahomaWithholdingCalculator(new OklahomaOw2PercentageCalculator(okJson), TestSchemas.Provider));
        var fica = new FicaCalculator();
        var fedJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        return new PayCalculator(registry, fica, fed);
    }
}
