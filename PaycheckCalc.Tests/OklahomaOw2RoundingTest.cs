using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class OklahomaOw2RoundingTest
{
    private static OklahomaOw2PercentageCalculator LoadOkCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "ok_ow2_2026_percentage.json");
        var json = File.ReadAllText(dataPath);
        return new OklahomaOw2PercentageCalculator(json);
    }

    [Fact]
    public void SemiMonthly_Married_TwoAllowances_RoundsTo37()
    {
        var ok = LoadOkCalculator();

        var allowanceTotal = ok.GetAllowanceAmount(PayFrequency.Semimonthly) * 2m;
        var okTaxable = 1825.00m - allowanceTotal;

        var wh = ok.CalculateWithholding(okTaxable, PayFrequency.Semimonthly, FilingStatus.Married);
        Assert.Equal(37m, wh);
    }

    [Fact]
    public void OklahomaStateTaxCalculator_ReturnsCorrectResult()
    {
        var inner = LoadOkCalculator();
        var adapter = new OklahomaStateTaxCalculator(inner);

        Assert.Equal(UsState.OK, adapter.State);

        var result = adapter.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 1825.00m,
            Frequency = PayFrequency.Semimonthly,
            FilingStatus = FilingStatus.Married,
            Allowances = 2,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        Assert.Equal(37m, result.Withholding);
        Assert.True(result.TaxableWages > 0m);
    }
}

public class NoIncomeTaxCalculatorTest
{
    [Theory]
    [InlineData(UsState.AK)]
    [InlineData(UsState.FL)]
    [InlineData(UsState.NV)]
    [InlineData(UsState.NH)]
    [InlineData(UsState.SD)]
    [InlineData(UsState.TN)]
    [InlineData(UsState.TX)]
    [InlineData(UsState.WA)]
    [InlineData(UsState.WY)]
    public void NoIncomeTaxStates_ReturnZeroWithholding(UsState state)
    {
        var calc = new NoIncomeTaxCalculator(state);

        Assert.Equal(state, calc.State);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 1,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }
}

public class StateTaxCalculatorFactoryTest
{
    [Fact]
    public void GetCalculator_RegisteredState_ReturnsCalculator()
    {
        var factory = new StateTaxCalculatorFactory();
        factory.Register(new NoIncomeTaxCalculator(UsState.TX));

        var calc = factory.GetCalculator(UsState.TX);

        Assert.NotNull(calc);
        Assert.Equal(UsState.TX, calc.State);
    }

    [Fact]
    public void GetCalculator_UnregisteredState_ThrowsNotSupportedException()
    {
        var factory = new StateTaxCalculatorFactory();

        Assert.Throws<NotSupportedException>(() => factory.GetCalculator(UsState.CA));
    }

    [Fact]
    public void IsSupported_ReturnsCorrectValue()
    {
        var factory = new StateTaxCalculatorFactory();
        factory.Register(new NoIncomeTaxCalculator(UsState.FL));

        Assert.True(factory.IsSupported(UsState.FL));
        Assert.False(factory.IsSupported(UsState.NY));
    }

    [Fact]
    public void SupportedStates_ReturnsRegisteredStatesInOrder()
    {
        var factory = new StateTaxCalculatorFactory();
        factory.Register(new NoIncomeTaxCalculator(UsState.TX));
        factory.Register(new NoIncomeTaxCalculator(UsState.FL));
        factory.Register(new NoIncomeTaxCalculator(UsState.AK));

        var states = factory.SupportedStates;

        Assert.Equal(3, states.Count);
        Assert.Equal(UsState.AK, states[0]);
        Assert.Equal(UsState.FL, states[1]);
        Assert.Equal(UsState.TX, states[2]);
    }
}
