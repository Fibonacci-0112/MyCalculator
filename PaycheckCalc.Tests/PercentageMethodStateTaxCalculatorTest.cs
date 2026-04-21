using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class PercentageMethodWithholdingAdapterExtendedTest
{
    // ── Flat-rate state tests ────────────────────────────────────────

    [Fact]
    public void Utah_FlatRate_AppliedToGrossWages()
    {
        var calc = CreateCalculator(UsState.UT);

        var context = new CommonWithholdingContext(
            UsState.UT,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // 5000 * 26 = 130,000 annual, no std ded, 130000 * 4.65% = 6,045 annual, / 26 = 232.50
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(232.50m, result.Withholding);
    }

    // Illinois uses a dedicated calculator (IllinoisWithholdingCalculator)

    [Fact]
    public void NorthCarolina_FlatRate_WithStandardDeduction_Married()
    {
        var calc = CreateCalculator(UsState.NC);

        var context = new CommonWithholdingContext(
            UsState.NC,
            GrossWages: 6000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

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

        var context = new CommonWithholdingContext(
            UsState.VA,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 1,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 5000 * 12 = 60,000
        // std ded single = 8,750
        // allowance deduction = 1 * 930 = 930
        // taxable = 60000 - 8750 - 930 = 50,320
        // brackets: 0-3000@2% = 60, 3000-5000@3% = 60, 5000-17000@5% = 600, 17000-50320@5.75% = 1915.90
        // total = 60 + 60 + 600 + 1915.90 = 2635.90
        // per period = 2635.90 / 12 = 219.6583... rounds to 219.66
        Assert.Equal(219.66m, result.Withholding);
    }

    [Fact]
    public void California_GraduatedBrackets_Single()
    {
        // California now uses a dedicated Method B calculator (CaliforniaPercentageCalculator).
        // See CaliforniaPercentageCalculatorTest for Method B tests.
        // This test verifies CA is no longer in the generic percentage method configs.
        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.CA),
            "CA should not be in generic StateTaxConfigs2026 — it uses CaliforniaPercentageCalculator.");
    }

    [Fact]
    public void NewYork_GraduatedBrackets_Married()
    {
        var calc = CreateCalculator(UsState.NY);

        var context = new CommonWithholdingContext(
            UsState.NY,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

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

        var context = new CommonWithholdingContext(
            UsState.OH,
            GrossWages: 900m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

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

        var context = new CommonWithholdingContext(
            UsState.OH,
            GrossWages: 3000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // annual = 3000 * 26 = 78,000
        // 0-26050 @ 0% = 0
        // 26050-78000 @ 2.75% = 51950 * 0.0275 = 1428.625
        // per period = 1428.625 / 26 = 54.9471... rounds to 54.95
        Assert.Equal(54.95m, result.Withholding);
    }

    // ── Allowance credit tests ───────────────────────────────────────
    // Delaware now uses a dedicated calculator (DelawareWithholdingCalculator).
    // See DelawareWithholdingCalculatorTest for allowance credit coverage.

    [Fact]
    public void Nebraska_AllowanceCredit_ReducesTax()
    {
        var calc = CreateCalculator(UsState.NE);

        var context = new CommonWithholdingContext(
            UsState.NE,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 2,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // Nebraska: AllowanceCreditAmount = $171, std ded single = $8,600
        // annual = 4000 * 12 = 48,000
        // taxable = 48,000 - 8,600 = 39,400
        // NE brackets (single):
        //   0-4,030 @ 2.46% = 99.138
        //   4,030-24,120 @ 3.51% = 705.159
        //   24,120-38,870 @ 5.01% = 738.975
        //   38,870-39,400 @ 5.20% = 27.56
        //   total = 1570.832
        // credit = 2 * 171 = 342
        // net tax = 1570.832 - 342 = 1228.832
        // per period = 1228.832 / 12 = 102.4026..., rounds to 102.40
        Assert.Equal(102.40m, result.Withholding);
    }

    // ── Additional withholding test ──────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = CreateCalculator(UsState.UT);

        var context = new CommonWithholdingContext(
            UsState.UT,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 50m
        };

        var result = calc.Calculate(context, values);

        // base = 232.50 + 50 = 282.50
        Assert.Equal(282.50m, result.Withholding);
    }

    // ── Pre-tax deductions test ──────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = CreateCalculator(UsState.UT);

        var context = new CommonWithholdingContext(
            UsState.UT,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        // taxable wages = 5000 - 1000 = 4000
        // annual = 4000 * 26 = 104,000
        // 104000 * 4.65% = 4,836
        // per period = 4,836 / 26 = 186.00
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(186.00m, result.Withholding);
    }

    // ── Zero wages edge case ─────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = CreateCalculator(UsState.UT);

        var context = new CommonWithholdingContext(
            UsState.UT,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Allowances"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── State property test ──────────────────────────────────────────

    [Theory]
    [InlineData(UsState.UT)]
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
            var calc = new PercentageMethodWithholdingAdapter(state, config);
            var context = new CommonWithholdingContext(
                state,
                GrossWages: 5000m,
                PayPeriod: PayFrequency.Biweekly,
                Year: 2026);
            var values = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 0,
                ["AdditionalWithholding"] = 0m
            };

            var result = calc.Calculate(context, values);

            Assert.True(result.TaxableWages >= 0m, $"{state} should have non-negative taxable wages");
            Assert.True(result.Withholding >= 0m, $"{state} should have non-negative withholding");
        }
    }

    [Fact]
    public void AllExpectedStates_AreConfigured()
    {
        // Arizona is intentionally absent: it uses the dedicated
        // ArizonaWithholdingCalculator (Form A-4 percentage election)
        // rather than the generic annualized percentage method.
        //
        // District of Columbia is also absent: it uses the dedicated
        // DistrictOfColumbiaWithholdingCalculator (D-4 annualized
        // percentage method).
        //
        // Hawaii is also absent: it uses the dedicated
        // HawaiiWithholdingCalculator (Booklet A percentage method with
        // HW-4 allowances).
        //
        // Idaho is also absent: it uses the dedicated
        // IdahoWithholdingCalculator (flat 5.3% with ID W-4 filing-status
        // standard deduction and per-allowance amount).
        //
        // Indiana is also absent: it uses the dedicated
        // IndianaWithholdingCalculator (flat 3.05% with WH-4 personal
        // and additional dependent exemptions).
        //
        // Iowa is also absent: it uses the dedicated
        // IowaWithholdingCalculator (flat 3.65% with optional extra
        // withholding per IA W-4 Line 6).
        //
        // Kansas is also absent: it uses the dedicated
        // KansasWithholdingCalculator (K-4 filing status, $3,605/$8,240
        // standard deduction, $2,250 per K-4 allowance, two graduated
        // brackets 5.20%/5.58%).
        //
        // Louisiana is also absent: it uses the dedicated
        // LouisianaWithholdingCalculator (L-4 filing statuses, $4,500/$9,000
        // personal exemption, $1,000 per-dependent deduction, and three
        // graduated brackets 1.85%/3.50%/4.25% per R-1306).
        //
        // Maine is also absent: it uses the dedicated
        // MaineWithholdingCalculator (W-4ME filing status, $15,300/$30,600
        // standard deduction, $5,300 allowance, three graduated brackets).
        //
        // Maryland is also absent: it uses the dedicated
        // MarylandWithholdingCalculator (MW507 filing statuses, variable
        // standard deduction 15% of wages with min/max, $3,200 per exemption,
        // ten graduated brackets 2%–6.5%).
        //
        // Massachusetts is also absent: it uses the dedicated
        // MassachusettsWithholdingCalculator (M-4 filing statuses, personal/
        // dependent/blind/age exemptions, flat 5% with 4% surtax above $1M).
        //
        // Michigan is also absent: it uses the dedicated
        // MichiganWithholdingCalculator (flat 4.25% with MI-W4 exemptions).
        UsState[] expectedStates =
        [
            UsState.KY,
            UsState.MN, UsState.MO, UsState.MS, UsState.MT, UsState.NC,
            UsState.ND, UsState.NE, UsState.NJ, UsState.NM, UsState.NY,
            UsState.OH, UsState.OR, UsState.RI, UsState.SC, UsState.UT,
            UsState.VA, UsState.VT, UsState.WI, UsState.WV
        ];

        foreach (var state in expectedStates)
            Assert.True(StateTaxConfigs2026.Configs.ContainsKey(state), $"{state} should be configured");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.AZ),
            "AZ should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.DC),
            "DC should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.HI),
            "HI should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.IA),
            "IA should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.ID),
            "ID should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.IN),
            "IN should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.KS),
            "KS should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.LA),
            "LA should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MD),
            "MD should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.ME),
            "ME should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MI),
            "MI should not be in StateTaxConfigs2026 — it has a dedicated calculator.");

        Assert.False(StateTaxConfigs2026.Configs.ContainsKey(UsState.MA),
            "MA should not be in StateTaxConfigs2026 — it has a dedicated calculator.");
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static PercentageMethodWithholdingAdapter CreateCalculator(UsState state)
    {
        var config = StateTaxConfigs2026.Configs[state];
        return new PercentageMethodWithholdingAdapter(state, config);
    }
}
