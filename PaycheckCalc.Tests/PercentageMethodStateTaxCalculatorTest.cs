using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class PercentageMethodStateTaxCalculatorTest
{
    // ── Flat-rate state tests ────────────────────────────────────────

    [Fact]
    public void Arizona_FlatRate_AppliedToGrossWages()
    {
        var calc = CreateCalculator(UsState.AZ);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // 5000 * 26 = 130,000 annual, no std ded, 130000 * 2.5% = 3250 annual, / 26 = 125.00
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(125.00m, result.Withholding);
    }

    [Fact]
    public void Illinois_FlatRate_WithAllowances()
    {
        var calc = CreateCalculator(UsState.IL);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 4000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 2,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 4000 * 26 = 104,000
        // allowance deduction = 2 * 2775 = 5550
        // taxable = 104000 - 5550 = 98,450
        // tax = 98450 * 0.0495 = 4873.275
        // per period = 4873.275 / 26 = 187.43365... rounds to 187.43
        Assert.Equal(187.43m, result.Withholding);
    }

    [Fact]
    public void NorthCarolina_FlatRate_WithStandardDeduction_Married()
    {
        var calc = CreateCalculator(UsState.NC);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 6000m,
            Frequency = PayFrequency.Monthly,
            FilingStatus = FilingStatus.Married,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 6000 * 12 = 72,000
        // std ded married = 25,500
        // taxable = 72000 - 25500 = 46,500
        // tax = 46500 * 0.045 = 2092.50
        // per period = 2092.50 / 12 = 174.375 rounds to 174.38
        Assert.Equal(174.38m, result.Withholding);
    }

    // ── Graduated-bracket state tests ────────────────────────────────

    [Fact]
    public void Virginia_GraduatedBrackets_Single()
    {
        var calc = CreateCalculator(UsState.VA);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Monthly,
            FilingStatus = FilingStatus.Single,
            Allowances = 1,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 5000 * 12 = 60,000
        // std ded single = 8,750
        // allowance deduction = 1 * 930 = 930
        // taxable = 60000 - 8750 - 930 = 50,320
        // brackets: 0-3000@2% = 60, 3000-5000@3% = 60, 5000-17000@5% = 600, 17000-50320@5.75% = 1915.90
        // total = 60 + 60 + 600 + 1915.90 = 2635.90 (correction: (50320-17000)*0.0575 = 33320*0.0575 = 1915.90)
        // per period = 2635.90 / 12 = 219.6583... rounds to 219.66
        Assert.Equal(219.66m, result.Withholding);
    }

    [Fact]
    public void California_GraduatedBrackets_Single()
    {
        var calc = CreateCalculator(UsState.CA);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 10000m,
            Frequency = PayFrequency.Monthly,
            FilingStatus = FilingStatus.Single,
            Allowances = 1,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 10000 * 12 = 120,000
        // std ded single = 5,706
        // allowance = 1 * 1000 = 1000
        // taxable = 120000 - 5706 - 1000 = 113,294
        // Brackets:
        // 0-11079 @ 1% = 110.79
        // 11079-26264 @ 2% = 303.70
        // 26264-41452 @ 4% = 607.52
        // 41452-57542 @ 6% = 965.40
        // 57542-72724 @ 8% = 1214.56
        // 72724-113294 @ 9.3% = 3773.01
        // total = 110.79 + 303.70 + 607.52 + 965.40 + 1214.56 + 3773.01 = 6974.98
        // per period = 6974.98 / 12 = 581.2483... rounds to 581.25
        Assert.Equal(581.25m, result.Withholding);
    }

    [Fact]
    public void NewYork_GraduatedBrackets_Married()
    {
        var calc = CreateCalculator(UsState.NY);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 4000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Married,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 4000 * 26 = 104,000
        // std ded married = 16,050
        // taxable = 104000 - 16050 = 87,950
        // Married brackets:
        // 0-17150 @ 4% = 686.00
        // 17150-23600 @ 4.5% = 290.25
        // 23600-27900 @ 5.25% = 225.75
        // 27900-43000 @ 5.9% = 890.90
        // 43000-87950 @ 6.09% = 2737.455
        // total = 686.00 + 290.25 + 225.75 + 890.90 + 2737.455 = 4830.355
        // per period = 4830.355 / 26 = 185.7828... rounds to 185.78
        Assert.Equal(185.78m, result.Withholding);
    }

    [Fact]
    public void Ohio_ZeroBracket_LowIncome()
    {
        var calc = CreateCalculator(UsState.OH);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 900m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 900 * 26 = 23,400
        // no std ded, taxable = 23,400
        // 0-26050 @ 0% = 0
        // total tax = 0
        Assert.Equal(0m, result.Withholding);
    }

    [Fact]
    public void Ohio_AboveThreshold()
    {
        var calc = CreateCalculator(UsState.OH);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 3000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 3000 * 26 = 78,000
        // 0-26050 @ 0% = 0
        // 26050-78000 @ 2.75% = 51950 * 0.0275 = 1428.625
        // per period = 1428.625 / 26 = 54.9471... rounds to 54.95
        Assert.Equal(54.95m, result.Withholding);
    }

    // ── Allowance credit tests ───────────────────────────────────────

    [Fact]
    public void Delaware_AllowanceCredit_ReducesTax()
    {
        var calc = CreateCalculator(UsState.DE);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 4000m,
            Frequency = PayFrequency.Monthly,
            FilingStatus = FilingStatus.Single,
            Allowances = 2,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // annual = 4000 * 12 = 48,000
        // std ded single = 3,250
        // taxable = 48000 - 3250 = 44,750
        // Brackets:
        // 0-2000 @ 0% = 0
        // 2000-5000 @ 2.2% = 66.00
        // 5000-10000 @ 3.9% = 195.00
        // 10000-20000 @ 4.8% = 480.00
        // 20000-25000 @ 5.2% = 260.00
        // 25000-44750 @ 5.55% = 1096.125
        // total = 0 + 66 + 195 + 480 + 260 + 1096.125 = 2097.125
        // credit = 2 * 110 = 220
        // net tax = 2097.125 - 220 = 1877.125
        // per period = 1877.125 / 12 = 156.427083... rounds to 156.43
        Assert.Equal(156.43m, result.Withholding);
    }

    // ── Additional withholding test ──────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = CreateCalculator(UsState.AZ);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 50m,
            PreTaxDeductionsReducingStateWages = 0m
        });

        // base = 125.00 + 50 = 175.00
        Assert.Equal(175.00m, result.Withholding);
    }

    // ── Pre-tax deductions test ──────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = CreateCalculator(UsState.CO);

        var result = calc.CalculateWithholding(new StateTaxInput
        {
            GrossWages = 5000m,
            Frequency = PayFrequency.Biweekly,
            FilingStatus = FilingStatus.Single,
            Allowances = 0,
            AdditionalWithholding = 0m,
            PreTaxDeductionsReducingStateWages = 1000m
        });

        // taxable wages = 5000 - 1000 = 4000
        // annual = 4000 * 26 = 104,000
        // 104000 * 4.4% = 4576
        // per period = 4576 / 26 = 176.00
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(176.00m, result.Withholding);
    }

    // ── Zero wages edge case ─────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = CreateCalculator(UsState.CA);

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

    // ── State property test ──────────────────────────────────────────

    [Theory]
    [InlineData(UsState.AZ)]
    [InlineData(UsState.CA)]
    [InlineData(UsState.NY)]
    [InlineData(UsState.VA)]
    [InlineData(UsState.OH)]
    public void State_ReturnsCorrectState(UsState state)
    {
        var calc = CreateCalculator(state);
        Assert.Equal(state, calc.State);
    }

    // ── All configured states produce a result ───────────────────────

    [Fact]
    public void AllConfiguredStates_ProduceResult()
    {
        foreach (var (state, config) in StateTaxConfigs2026.Configs)
        {
            var calc = new PercentageMethodStateTaxCalculator(state, config);
            var result = calc.CalculateWithholding(new StateTaxInput
            {
                GrossWages = 5000m,
                Frequency = PayFrequency.Biweekly,
                FilingStatus = FilingStatus.Single,
                Allowances = 0,
                AdditionalWithholding = 0m,
                PreTaxDeductionsReducingStateWages = 0m
            });

            Assert.True(result.TaxableWages >= 0m, $"{state} should have non-negative taxable wages");
            Assert.True(result.Withholding >= 0m, $"{state} should have non-negative withholding");
        }
    }

    [Fact]
    public void AllExpectedStates_AreConfigured()
    {
        UsState[] expectedStates =
        [
            UsState.AR, UsState.AZ, UsState.CA, UsState.CO, UsState.CT,
            UsState.DC, UsState.DE, UsState.GA, UsState.HI, UsState.IA,
            UsState.ID, UsState.IL, UsState.IN, UsState.KS, UsState.KY,
            UsState.LA, UsState.MA, UsState.MD, UsState.ME, UsState.MI,
            UsState.MN, UsState.MO, UsState.MS, UsState.MT, UsState.NC,
            UsState.ND, UsState.NE, UsState.NJ, UsState.NM, UsState.NY,
            UsState.OH, UsState.OR, UsState.RI, UsState.SC, UsState.UT,
            UsState.VA, UsState.VT, UsState.WI, UsState.WV
        ];

        foreach (var state in expectedStates)
            Assert.True(StateTaxConfigs2026.Configs.ContainsKey(state), $"{state} should be configured");
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static PercentageMethodStateTaxCalculator CreateCalculator(UsState state)
    {
        var config = StateTaxConfigs2026.Configs[state];
        return new PercentageMethodStateTaxCalculator(state, config);
    }
}
