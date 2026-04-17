using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class PayCalculatorLocalIntegrationTest
{
    [Fact]
    public void NoLocalRegistry_ProducesBackwardCompatibleResult()
    {
        // Exercises the null-local-registry code path to guarantee no regression for
        // states without locality plugins (i.e. most of the country).
        var calc = BuildPayCalculator(localRegistry: null);

        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.PA
        };

        var result = calc.Calculate(input);

        Assert.Equal(0m, result.LocalWithholding);
        Assert.Equal(0m, result.LocalHeadTax);
        Assert.Empty(result.LocalBreakdown);
        Assert.Equal(string.Empty, result.LocalityLabel);
    }

    [Fact]
    public void LocalRegistryProvided_ButNoLocalityCodes_ProducesZeroLocal()
    {
        var registry = new LocalCalculatorRegistry();
        registry.Register(new PaLstCalculator());

        var calc = BuildPayCalculator(registry);

        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.PA
            // no HomeLocalityCode, no WorkLocalityCode
        };

        var result = calc.Calculate(input);

        Assert.Equal(0m, result.LocalWithholding);
        Assert.Equal(0m, result.LocalHeadTax);
    }

    [Fact]
    public void PaLst_Biweekly_SubtractedFromNetPay()
    {
        var registry = new LocalCalculatorRegistry();
        registry.Register(new PaLstCalculator());
        var calc = BuildPayCalculator(registry);

        var baselineInput = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.PA
        };
        var baseline = calc.Calculate(baselineInput);

        var withLst = calc.Calculate(new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.PA,
            HomeLocalityCode = PaLstCalculator.LocalityKey.Code,
            LocalInputValues = new LocalInputValues
            {
                [PaLstCalculator.AnnualAmountKey] = 52m
            }
        });

        // $52 / 26 = $2.00 per pay period
        Assert.Equal(2.00m, withLst.LocalHeadTax);
        Assert.Equal(baseline.NetPay - 2.00m, withLst.NetPay);
    }

    [Fact]
    public void LocalTaxes_DoNotReduceFederalOrStateTaxableWages()
    {
        // Guard: adding a locality plugin must not change FIT/SIT taxable wages or withholding.
        var baselineCalc = BuildPayCalculator(localRegistry: null);
        var registry = new LocalCalculatorRegistry();
        registry.Register(new MdCountyCalculator(File.ReadAllText("md_county_surtax_2026.json")));
        var localCalc = BuildPayCalculator(registry);

        var baseInput = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 30m,
            RegularHours = 80m,
            State = UsState.MD
        };

        var baseline = baselineCalc.Calculate(baseInput);
        var withLocal = localCalc.Calculate(new PaycheckInput
        {
            Frequency = baseInput.Frequency,
            HourlyRate = baseInput.HourlyRate,
            RegularHours = baseInput.RegularHours,
            State = UsState.MD,
            HomeLocalityCode = MdCountyCalculator.LocalityKey.Code,
            LocalInputValues = new LocalInputValues
            {
                [MdCountyCalculator.CountyKey] = "MONT"
            }
        });

        Assert.Equal(baseline.FederalTaxableIncome, withLocal.FederalTaxableIncome);
        Assert.Equal(baseline.FederalWithholding, withLocal.FederalWithholding);
        Assert.Equal(baseline.StateTaxableWages, withLocal.StateTaxableWages);
        Assert.Equal(baseline.StateWithholding, withLocal.StateWithholding);
        Assert.True(withLocal.LocalWithholding > 0m);
        Assert.Equal(baseline.NetPay - withLocal.LocalWithholding, withLocal.NetPay);
    }

    [Fact]
    public void UnknownLocalityCode_IsIgnoredGracefully()
    {
        var registry = new LocalCalculatorRegistry();
        var calc = BuildPayCalculator(registry);

        var input = new PaycheckInput
        {
            Frequency = PayFrequency.Biweekly,
            HourlyRate = 25m,
            RegularHours = 80m,
            State = UsState.PA,
            HomeLocalityCode = "NOT-REGISTERED"
        };

        var result = calc.Calculate(input);

        Assert.Equal(0m, result.LocalWithholding);
        Assert.Equal(0m, result.LocalHeadTax);
    }

    private static PayCalculator BuildPayCalculator(LocalCalculatorRegistry? localRegistry)
    {
        var stateRegistry = new StateCalculatorRegistry();
        stateRegistry.Register(new PennsylvaniaWithholdingCalculator());
        // MD → generic percentage-method adapter. Reuse StateTaxConfigs2026.
        foreach (var (state, config) in StateTaxConfigs2026.Configs)
            stateRegistry.Register(new PercentageMethodWithholdingAdapter(state, config));

        var fica = new FicaCalculator();
        var fed = new Irs15TPercentageCalculator(
            File.ReadAllText("us_irs_15t_2026_percentage_automated.json"));
        return new PayCalculator(stateRegistry, fica, fed, localRegistry);
    }
}
