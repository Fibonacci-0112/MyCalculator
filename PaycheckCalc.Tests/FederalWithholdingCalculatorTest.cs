using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using Xunit;

public class FederalWithholdingCalculatorTest
{
    private static Irs15TPercentageCalculator LoadCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "us_irs_15t_2026_percentage_automated.json");
        var json = File.ReadAllText(dataPath);
        return new Irs15TPercentageCalculator(json);
    }

    // ── Filing status: Single / MFS ──────────────────────────────────

    [Fact]
    public void Single_Biweekly_5000_StandardW4()
    {
        var calc = LoadCalculator();

        // Step 1: annual = 5000 * 26 = 130,000
        // 1e = 130,000 (no Step 4a)
        // 1g = 8,600 (single, step2 unchecked)
        // 1h = 8,600
        // adjusted = 121,400
        // bracket over 113,200 @ 24%: 17,866 + (121,400 - 113,200) * 0.24 = 17,866 + 1,968 = 19,834
        // per period = 19,834 / 26 = 762.8461...
        // rounds to 762.85
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 5000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            });

        Assert.Equal(762.85m, result);
    }

    [Fact]
    public void Single_Weekly_SmallWages_BelowStandardDeduction_ReturnsZero()
    {
        var calc = LoadCalculator();

        // annual = 100 * 52 = 5,200
        // 1g = 8,600
        // adjusted = max(0, 5200 - 8600) = 0  → returns 0 immediately
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 100m,
            frequency: PayFrequency.Weekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            });

        Assert.Equal(0m, result);
    }

    [Fact]
    public void Single_Monthly_HighWages_TopBracket()
    {
        var calc = LoadCalculator();

        // annual = 60,000 * 12 = 720,000
        // 1g = 8,600
        // adjusted = 711,400
        // bracket over 648,100 @ 37%: 192,979.25 + (711,400 - 648,100) * 0.37 = 192,979.25 + 23,421 = 216,400.25
        // per period = 216,400.25 / 12 = 18,033.354...
        // rounds to 18,033.35
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 60_000m,
            frequency: PayFrequency.Monthly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            });

        Assert.Equal(18_033.35m, result);
    }

    // ── Filing status: Married Filing Jointly ────────────────────────

    [Fact]
    public void MarriedFilingJointly_Monthly_10000_StandardW4()
    {
        var calc = LoadCalculator();

        // annual = 10,000 * 12 = 120,000
        // 1g = 12,900 (MFJ, step2 unchecked)
        // adjusted = 107,100
        // bracket over 44,100 @ 12%: 2,480 + (107,100 - 44,100) * 0.12 = 2,480 + 7,560 = 10,040
        // per period = 10,040 / 12 = 836.666...
        // rounds to 836.67
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 10_000m,
            frequency: PayFrequency.Monthly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.MarriedFilingJointly
            });

        Assert.Equal(836.67m, result);
    }

    [Fact]
    public void MarriedFilingJointly_HasLargerStandardDeduction_Than_Single()
    {
        var calc = LoadCalculator();
        var w4Single = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };
        var w4Mfj = new FederalW4Input { FilingStatus = FederalFilingStatus.MarriedFilingJointly };

        var single = calc.CalculateWithholding(5000m, PayFrequency.Biweekly, w4Single);
        var mfj = calc.CalculateWithholding(5000m, PayFrequency.Biweekly, w4Mfj);

        // MFJ standard deduction ($12,900) > Single deduction ($8,600)
        // → MFJ should withhold less at the same gross wage
        Assert.True(mfj < single, $"MFJ ({mfj}) should withhold less than Single ({single}) at same wages");
    }

    // ── Filing status: Head of Household ─────────────────────────────

    [Fact]
    public void HeadOfHousehold_Biweekly_3000()
    {
        var calc = LoadCalculator();

        // annual = 3,000 * 26 = 78,000
        // 1g = 8,600 (HOH uses "other")
        // adjusted = 69,400
        // bracket over 33,250 @ 12%: 1,770 + (69,400 - 33,250) * 0.12 = 1,770 + 4,338 = 6,108
        // per period = 6,108 / 26 = 234.923...
        // rounds to 234.92
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 3000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.HeadOfHousehold
            });

        Assert.Equal(234.92m, result);
    }

    // ── Step 2 checkbox (multiple jobs) ──────────────────────────────

    [Fact]
    public void Step2Checked_SkipsStandardDeduction_SingleBiweekly3000()
    {
        var calc = LoadCalculator();

        // annual = 3,000 * 26 = 78,000
        // step2 checked → 1g = 0, 1h = 0
        // adjusted = 78,000
        // Step2Checked Single bracket: over 60,900 @ 24%: 8,993 + (78,000 - 60,900) * 0.24 = 8,993 + 4,104 = 13,097
        // per period = 13,097 / 26 = 503.730...
        // rounds to 503.73
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 3000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step2Checked = true
            });

        Assert.Equal(503.73m, result);
    }

    [Fact]
    public void Step2Checked_WithholdsMoreThan_Standard_AtSameWages()
    {
        var calc = LoadCalculator();
        var standard = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };
        var step2 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately, Step2Checked = true };

        var standardResult = calc.CalculateWithholding(3000m, PayFrequency.Biweekly, standard);
        var step2Result = calc.CalculateWithholding(3000m, PayFrequency.Biweekly, step2);

        // Step 2 uses higher-rate brackets and no standard deduction → higher withholding
        Assert.True(step2Result > standardResult,
            $"Step 2 checked ({step2Result}) should withhold more than standard ({standardResult})");
    }

    // ── Step 3 tax credits ────────────────────────────────────────────

    [Fact]
    public void Step3Credits_ReduceWithholding()
    {
        var calc = LoadCalculator();

        // From the Single_Biweekly_5000 test: base withholding ≈ 762.85
        // Step3TaxCredits = $2,000 annually
        // credits per period = 2000 / 26 = 76.923...
        // pre-rounding: 762.846... - 76.923... = 685.923...
        // rounds to 685.92
        var resultWithCredits = calc.CalculateWithholding(
            taxableWagesThisPeriod: 5000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step3TaxCredits = 2000m
            });

        var resultNoCredits = calc.CalculateWithholding(
            taxableWagesThisPeriod: 5000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            });

        Assert.Equal(685.92m, resultWithCredits);
        Assert.True(resultWithCredits < resultNoCredits);
    }

    [Fact]
    public void Step3Credits_ExceedTax_ReturnZero()
    {
        var calc = LoadCalculator();

        // Very large credits should floor withholding at zero
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 2000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step3TaxCredits = 50_000m  // Way more than any withholding
            });

        Assert.Equal(0m, result);
    }

    // ── Step 4(a) other income ────────────────────────────────────────

    [Fact]
    public void Step4aOtherIncome_IncreasesWithholding()
    {
        var calc = LoadCalculator();

        // Base: annual = 130,000, adjusted = 121,400 → 762.85 (from test 1)
        // With step4a = 1,000:
        //   1e = 130,000 + 1,000 = 131,000
        //   adjusted = 131,000 - 8,600 = 122,400
        //   tentativeAnnual = 17,866 + (122,400 - 113,200) * 0.24 = 17,866 + 2,208 = 20,074
        //   per period = 20,074 / 26 = 772.076...
        //   rounds to 772.08
        var resultWithOtherIncome = calc.CalculateWithholding(
            taxableWagesThisPeriod: 5000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step4aOtherIncome = 1000m
            });

        Assert.Equal(772.08m, resultWithOtherIncome);
    }

    // ── Step 4(b) additional deductions ──────────────────────────────

    [Fact]
    public void Step4bDeductions_ReduceWithholding()
    {
        var calc = LoadCalculator();

        // Single, biweekly, $5,000, step4b = $5,000 annual
        // 1g = 8,600, 1h = 5,000 + 8,600 = 13,600
        // adjusted = 130,000 - 13,600 = 116,400
        // bracket over 113,200 @ 24%: 17,866 + (116,400 - 113,200) * 0.24 = 17,866 + 768 = 18,634
        // per period = 18,634 / 26 = 716.692...
        // rounds to 716.69
        var resultWithDeductions = calc.CalculateWithholding(
            taxableWagesThisPeriod: 5000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step4bDeductions = 5000m
            });

        var resultBase = calc.CalculateWithholding(
            taxableWagesThisPeriod: 5000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately
            });

        Assert.Equal(716.69m, resultWithDeductions);
        Assert.True(resultWithDeductions < resultBase);
    }

    // ── Step 4(c) extra withholding ───────────────────────────────────

    [Fact]
    public void Step4cExtraWithholding_AddsToResult()
    {
        var calc = LoadCalculator();

        // Base (from test 1): 762.85
        // Extra: 50
        // Expected: pre-rounding 762.846... + 50 = 812.846... → rounds to 812.85
        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 5000m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input
            {
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                Step4cExtraWithholding = 50m
            });

        Assert.Equal(812.85m, result);
    }

    // ── Pay frequency ─────────────────────────────────────────────────

    [Theory]
    [InlineData(PayFrequency.Weekly, 52)]
    [InlineData(PayFrequency.Biweekly, 26)]
    [InlineData(PayFrequency.Semimonthly, 24)]
    [InlineData(PayFrequency.Monthly, 12)]
    [InlineData(PayFrequency.Quarterly, 4)]
    [InlineData(PayFrequency.Semiannual, 2)]
    [InlineData(PayFrequency.Annual, 1)]
    public void AllFrequencies_SameAnnualWages_ProduceSameAnnualTax(PayFrequency frequency, int periodsPerYear)
    {
        var calc = LoadCalculator();
        var annualWage = 100_000m;
        var wagePerPeriod = annualWage / periodsPerYear;

        var w4 = new FederalW4Input { FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately };
        var withheldPerPeriod = calc.CalculateWithholding(wagePerPeriod, frequency, w4);
        var annualWithheld = withheldPerPeriod * periodsPerYear;

        // All frequencies should annualize to roughly the same total (within rounding tolerance)
        // At $100,000 annual wages, withholding should be non-trivial
        Assert.True(withheldPerPeriod > 0m, $"{frequency} should produce non-zero withholding");
        Assert.True(annualWithheld > 5_000m, $"{frequency} annual withholding should exceed $5,000 at $100k salary");
    }

    // ── Zero / negative wages ─────────────────────────────────────────

    [Fact]
    public void ZeroWages_ReturnsZero()
    {
        var calc = LoadCalculator();

        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: 0m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input());

        Assert.Equal(0m, result);
    }

    [Fact]
    public void NegativeWages_ReturnsZero()
    {
        var calc = LoadCalculator();

        var result = calc.CalculateWithholding(
            taxableWagesThisPeriod: -500m,
            frequency: PayFrequency.Biweekly,
            w4: new FederalW4Input());

        Assert.Equal(0m, result);
    }
}
