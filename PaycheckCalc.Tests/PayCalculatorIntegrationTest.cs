using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using Xunit;

/// <summary>
/// End-to-end integration tests for <see cref="PayCalculator.Calculate"/>,
/// verifying that all components (gross pay, federal withholding, FICA, state
/// withholding, deductions) interact correctly to produce the right net pay.
/// </summary>
public class PayCalculatorIntegrationTest
{
    private static PayCalculator BuildCalculator(params IStateWithholdingCalculator[] extraCalculators)
    {
        var fedJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json"));
        var fed = new Irs15TPercentageCalculator(fedJson);
        var fica = new FicaCalculator();

        var registry = new StateCalculatorRegistry();
        registry.Register(new PennsylvaniaWithholdingCalculator());
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));
        foreach (var calc in extraCalculators)
            registry.Register(calc);

        return new PayCalculator(registry, fica, fed);
    }

    // ── Basic paycheck ───────────────────────────────────────────────

    [Fact]
    public void BasicPaycheck_PA_Single_Biweekly_NoDeductions()
    {
        var calculator = BuildCalculator();

        // gross = 25 * 80 = 2,000
        // FICA: SS = 2000 * 6.2% = 124.00; Medicare = 2000 * 1.45% = 29.00; Addl = 0
        // Federal (Single, biweekly, $2,000):
        //   annual = 2000 * 26 = 52,000; 1g = 8,600; adjusted = 43,400
        //   bracket over 19,900 @ 12%: 1,240 + (43,400 - 19,900) * 0.12 = 1,240 + 2,820 = 4,060
        //   per period = 4,060 / 26 = 156.153... → rounds to 156.15
        // PA state: 2000 * 3.07% = 61.40
        // net = 2000 - 124.00 - 29.00 - 0 - 156.15 - 61.40 = 1,629.45
        var input = new PaycheckInput
        {
            HourlyRate = 25m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.PA,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
        Assert.Equal(124.00m, result.SocialSecurityWithholding);
        Assert.Equal(29.00m, result.MedicareWithholding);
        Assert.Equal(0m, result.AdditionalMedicareWithholding);
        Assert.Equal(156.15m, result.FederalWithholding);
        Assert.Equal(61.40m, result.StateWithholding);
        Assert.Equal(0m, result.PreTaxDeductions);
        Assert.Equal(0m, result.PostTaxDeductions);
        Assert.Equal(1629.45m, result.NetPay);
    }

    // ── Pre-tax deductions ────────────────────────────────────────────

    [Fact]
    public void PreTaxDeduction_ReducesFicaFederalAndStateWages()
    {
        var calculator = BuildCalculator();

        // gross = 2,000; pre-tax 401k = 200
        // ficaWages = 1,800; SS = 1800 * 6.2% = 111.60; Medicare = 1800 * 1.45% = 26.10
        // Federal (Single, biweekly, $1,800):
        //   annual = 1800 * 26 = 46,800; 1g = 8,600; adjusted = 38,200
        //   bracket over 19,900 @ 12%: 1,240 + (38,200 - 19,900) * 0.12 = 1,240 + 2,196 = 3,436
        //   per period = 3,436 / 26 = 132.153... → rounds to 132.15
        // PA state: (2000 - 200) * 3.07% = 55.26
        // net = 2000 - 200 - 55.26 - 111.60 - 26.10 - 0 - 132.15 = 1,474.89
        var input = new PaycheckInput
        {
            HourlyRate = 25m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.PA,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            },
            Deductions =
            [
                new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = 200m }
            ]
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
        Assert.Equal(200m, result.PreTaxDeductions);
        Assert.Equal(1800m, result.FederalTaxableIncome);
        Assert.Equal(111.60m, result.SocialSecurityWithholding);
        Assert.Equal(26.10m, result.MedicareWithholding);
        Assert.Equal(132.15m, result.FederalWithholding);
        Assert.Equal(55.26m, result.StateWithholding);
        Assert.Equal(1474.89m, result.NetPay);
    }

    // ── Post-tax deductions ───────────────────────────────────────────

    [Fact]
    public void PostTaxDeduction_DoesNotReduceTaxableWages()
    {
        var calculator = BuildCalculator();

        // Post-tax deduction does NOT reduce FICA or income tax wages
        // Taxes are same as BasicPaycheck_PA_Single_Biweekly_NoDeductions
        // Net = 1,629.45 - 200 (post-tax) = 1,429.45
        var input = new PaycheckInput
        {
            HourlyRate = 25m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.PA,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            },
            Deductions =
            [
                new Deduction { Name = "Health Insurance", Type = DeductionType.PostTax, Amount = 200m }
            ]
        };

        var result = calculator.Calculate(input);

        Assert.Equal(2000m, result.GrossPay);
        Assert.Equal(0m, result.PreTaxDeductions);
        Assert.Equal(200m, result.PostTaxDeductions);
        // Taxable income is unchanged by post-tax deductions
        Assert.Equal(2000m, result.FederalTaxableIncome);
        Assert.Equal(124.00m, result.SocialSecurityWithholding);
        Assert.Equal(29.00m, result.MedicareWithholding);
        Assert.Equal(156.15m, result.FederalWithholding);
        Assert.Equal(61.40m, result.StateWithholding);
        Assert.Equal(1429.45m, result.NetPay);
    }

    // ── Overtime ──────────────────────────────────────────────────────

    [Fact]
    public void OvertimePay_IncludedInGross_At1Point5x()
    {
        var calculator = BuildCalculator();

        // gross = (80 * 20) + (10 * 20 * 1.5) = 1,600 + 300 = 1,900
        var input = new PaycheckInput
        {
            HourlyRate = 20m,
            RegularHours = 80m,
            OvertimeHours = 10m,
            OvertimeMultiplier = 1.5m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        Assert.Equal(1900m, result.GrossPay);
    }

    [Fact]
    public void CustomOvertimeMultiplier_IsApplied()
    {
        var calculator = BuildCalculator();

        // gross = (40 * 30) + (5 * 30 * 2.0) = 1,200 + 300 = 1,500
        var input = new PaycheckInput
        {
            HourlyRate = 30m,
            RegularHours = 40m,
            OvertimeHours = 5m,
            OvertimeMultiplier = 2.0m,
            Frequency = PayFrequency.Weekly,
            State = UsState.TX
        };

        var result = calculator.Calculate(input);

        Assert.Equal(1500m, result.GrossPay);
    }

    // ── No-income-tax state ───────────────────────────────────────────

    [Fact]
    public void NoIncomeTaxState_TX_StateWithholdingIsZero()
    {
        var calculator = BuildCalculator();

        var input = new PaycheckInput
        {
            HourlyRate = 25m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            }
        };

        var result = calculator.Calculate(input);

        Assert.Equal(0m, result.StateWithholding);
        Assert.Equal(0m, result.StateDisabilityInsurance);
        // FICA and federal still apply
        Assert.True(result.SocialSecurityWithholding > 0m);
        Assert.True(result.FederalWithholding > 0m);
    }

    // ── Social Security wage base ─────────────────────────────────────

    [Fact]
    public void YtdNearSsWageBase_CapsSsWithholding()
    {
        var calculator = BuildCalculator();

        // gross = 100 * 80 = 8,000
        // YTD SS = $184,000 → remaining base = $500
        // SS = 500 * 6.2% = 31.00
        // Medicare = 8000 * 1.45% = 116.00
        var input = new PaycheckInput
        {
            HourlyRate = 100m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX,
            YtdSocialSecurityWages = 184_000m,
            YtdMedicareWages = 184_000m
        };

        var result = calculator.Calculate(input);

        Assert.Equal(8000m, result.GrossPay);
        Assert.Equal(31.00m, result.SocialSecurityWithholding);
        Assert.Equal(116.00m, result.MedicareWithholding);
    }

    [Fact]
    public void YtdAtSsWageBase_ZeroSsWithholding()
    {
        var calculator = BuildCalculator();

        var input = new PaycheckInput
        {
            HourlyRate = 50m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX,
            YtdSocialSecurityWages = 184_500m,
            YtdMedicareWages = 0m
        };

        var result = calculator.Calculate(input);

        Assert.Equal(0m, result.SocialSecurityWithholding);
        Assert.True(result.MedicareWithholding > 0m);
    }

    // ── Net pay invariant ─────────────────────────────────────────────

    [Theory]
    [InlineData(20.0, 80.0, 0.0)]
    [InlineData(50.0, 80.0, 10.0)]
    [InlineData(100.0, 80.0, 20.0)]
    public void NetPay_EqualsGrossMinus_AllTaxesAndDeductions(
        double hourlyRateD, double regularHoursD, double preTaxAmountD)
    {
        decimal hourlyRate = (decimal)hourlyRateD;
        decimal regularHours = (decimal)regularHoursD;
        decimal preTaxAmount = (decimal)preTaxAmountD;
        var calculator = BuildCalculator();

        var deductions = preTaxAmount > 0
            ? new[] { new Deduction { Name = "401k", Type = DeductionType.PreTax, Amount = preTaxAmount } }
            : Array.Empty<Deduction>();

        var input = new PaycheckInput
        {
            HourlyRate = hourlyRate,
            RegularHours = regularHours,
            Frequency = PayFrequency.Biweekly,
            State = UsState.PA,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            },
            Deductions = deductions
        };

        var result = calculator.Calculate(input);

        var expectedNet = result.GrossPay
            - result.PreTaxDeductions
            - result.PostTaxDeductions
            - result.StateWithholding
            - result.StateDisabilityInsurance
            - result.SocialSecurityWithholding
            - result.MedicareWithholding
            - result.AdditionalMedicareWithholding
            - result.FederalWithholding;

        Assert.Equal(expectedNet, result.NetPay, precision: 1);  // within 1 cent due to pre/post rounding of components
    }

    // ── TotalTaxes property ───────────────────────────────────────────

    [Fact]
    public void TotalTaxes_SumsAllTaxComponents()
    {
        var calculator = BuildCalculator();

        var input = new PaycheckInput
        {
            HourlyRate = 25m,
            RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.PA,
            FederalW4 = new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            }
        };

        var result = calculator.Calculate(input);

        var expectedTotal = result.StateWithholding
            + result.StateDisabilityInsurance
            + result.SocialSecurityWithholding
            + result.MedicareWithholding
            + result.AdditionalMedicareWithholding
            + result.FederalWithholding;

        Assert.Equal(expectedTotal, result.TotalTaxes);
    }

    // ── State field ───────────────────────────────────────────────────

    [Fact]
    public void Result_ReflectsInputState()
    {
        var calculator = BuildCalculator();

        var inputPA = new PaycheckInput
        {
            HourlyRate = 25m, RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.PA
        };
        var inputTX = new PaycheckInput
        {
            HourlyRate = 25m, RegularHours = 80m,
            Frequency = PayFrequency.Biweekly,
            State = UsState.TX
        };

        Assert.Equal(UsState.PA, calculator.Calculate(inputPA).State);
        Assert.Equal(UsState.TX, calculator.Calculate(inputTX).State);
    }
}
