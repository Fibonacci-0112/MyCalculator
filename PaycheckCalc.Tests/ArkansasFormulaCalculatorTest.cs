using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.State;
using Xunit;

// ─── ArkansasFormulaCalculator Tests ────────────────────────────────────

public class ArkansasFormulaCalculatorTest
{
    private static ArkansasFormulaCalculator LoadCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "ar_withholding_2026.json");
        var json = File.ReadAllText(dataPath);
        return new ArkansasFormulaCalculator(json);
    }

    // ── Step-by-step formula verification ───────────────────────────────

    /// <summary>
    /// Biweekly, $2,000 gross, 0 exemptions.
    /// Annual gross = 2,000 × 26 = 52,000.
    /// Net taxable = 52,000 − 2,470 = 49,530.
    /// Rounded to nearest $50 = 49,550 (since 49,530/50 = 990.6 → 991 × 50 = 49,550).
    /// Bracket: $26,400–$94,700 → 3.9% × 49,550 − 419.96 = 1,932.45 − 419.96 = 1,512.49.
    /// Credits = 0 × $29 = 0.
    /// Annual net tax = 1,512.49.
    /// Per period = 1,512.49 / 26 = 58.17.
    /// </summary>
    [Fact]
    public void Biweekly_2000Gross_ZeroExemptions()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(2000m, 26, 0);
        Assert.Equal(58.17m, result);
    }

    /// <summary>
    /// Monthly, $5,000 gross, 3 exemptions.
    /// Annual gross = 5,000 × 12 = 60,000.
    /// Net taxable = 60,000 − 2,470 = 57,530.
    /// Rounded to nearest $50 = 57,550 (57,530/50 = 1150.6 → 1151 × 50 = 57,550).
    /// Bracket: $26,400–$94,700 → 3.9% × 57,550 − 419.96 = 2,244.45 − 419.96 = 1,824.49.
    /// Credits = 3 × $29 = $87.
    /// Annual net tax = 1,824.49 − 87 = 1,737.49.
    /// Per period = 1,737.49 / 12 = 144.79.
    /// </summary>
    [Fact]
    public void Monthly_5000Gross_ThreeExemptions()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(5000m, 12, 3);
        Assert.Equal(144.79m, result);
    }

    // ── Bracket boundary tests ──────────────────────────────────────────

    /// <summary>
    /// Income falls in the 0% bracket (no tax owed).
    /// Weekly $100, 52 periods → annual = 5,200.
    /// Net taxable = 5,200 − 2,470 = 2,730.
    /// Rounded to nearest $50 = 2,750 (2,730/50 = 54.6 → 55 × 50 = 2,750).
    /// Bracket: $0–$5,599 → 0% → tax = $0.
    /// </summary>
    [Fact]
    public void ZeroBracket_NoTaxOwed()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(100m, 52, 0);
        Assert.Equal(0m, result);
    }

    /// <summary>
    /// Income at the start of the 2% bracket.
    /// Net taxable income near $5,600.
    /// Annual gross = 8,100 → net = 8,100 − 2,470 = 5,630.
    /// Rounded = 5,650 (5,630/50 = 112.6 → 113 × 50 = 5,650).
    /// Bracket: $5,600–$11,199 → 2% × 5,650 − 111.98 = 113 − 111.98 = 1.02.
    /// Per period = 1.02 / 1 = 1.02.
    /// </summary>
    [Fact]
    public void TwoPercentBracket_LowEnd()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(8100m, 1, 0);
        Assert.Equal(1.02m, result);
    }

    /// <summary>
    /// Income in the 3% bracket.
    /// Annual gross = 14,000, 1 period. Net = 14,000 − 2,470 = 11,530.
    /// Rounded = 11,550 (11,530/50 = 230.6 → 231 × 50 = 11,550).
    /// Bracket: $11,200–$15,999 → 3% × 11,550 − 223.97 = 346.50 − 223.97 = 122.53.
    /// Per period = 122.53 / 1 = 122.53.
    /// </summary>
    [Fact]
    public void ThreePercentBracket()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(14000m, 1, 0);
        Assert.Equal(122.53m, result);
    }

    /// <summary>
    /// Income in the 3.4% bracket.
    /// Annual gross = 22,000, 1 period. Net = 22,000 − 2,470 = 19,530.
    /// Rounded = 19,550 (19,530/50 = 390.6 → 391 × 50 = 19,550).
    /// Bracket: $16,000–$26,399 → 3.4% × 19,550 − 287.97 = 664.70 − 287.97 = 376.73.
    /// Per period = 376.73 / 1 = 376.73.
    /// </summary>
    [Fact]
    public void ThreePointFourPercentBracket()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(22000m, 1, 0);
        Assert.Equal(376.73m, result);
    }

    // ── Transitional zone tests ─────────────────────────────────────────

    /// <summary>
    /// Income in the transition zone ($94,701–$94,800).
    /// Annual gross = 97,220, 1 period. Net = 97,220 − 2,470 = 94,750.
    /// Rounded = 94,750 (already a multiple of $50).
    /// Bracket: $94,701–$94,800 → 3.9% × 94,750 − 399.30 = 3,695.25 − 399.30 = 3,295.95.
    /// Per period = 3,295.95 / 1 = 3,295.95.
    /// </summary>
    [Fact]
    public void TransitionalZone_FirstBracket()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(97220m, 1, 0);
        Assert.Equal(3295.95m, result);
    }

    /// <summary>
    /// Income in the final transition bracket ($97,701–$97,800).
    /// Annual gross = 100,270, 1 period. Net = 100,270 − 2,470 = 97,800.
    /// Rounded = 97,800 (already a multiple of $50).
    /// Bracket: $97,701–$97,800 → 3.9% × 97,800 − 99.30 = 3,814.20 − 99.30 = 3,714.90.
    /// Per period = 3,714.90 / 1 = 3,714.90.
    /// </summary>
    [Fact]
    public void TransitionalZone_LastBracket()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(100270m, 1, 0);
        Assert.Equal(3714.90m, result);
    }

    /// <summary>
    /// Income over $100,001 (no rounding, final bracket $97,801+).
    /// Annual gross = 150,000, 1 period. Net = 150,000 − 2,470 = 147,530.
    /// No rounding (≥ 100,001). Exact amount used.
    /// Bracket: $97,801+ → 3.9% × 147,530 − 89.30 = 5,753.67 − 89.30 = 5,664.37.
    /// Per period = 5,664.37 / 1 = 5,664.37.
    /// </summary>
    [Fact]
    public void HighIncome_NoRounding_FinalBracket()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(150000m, 1, 0);
        Assert.Equal(5664.37m, result);
    }

    /// <summary>
    /// Income over $100,001 where gross tax has more than two decimal places.
    /// Annual gross = 105,001, 1 period. Net = 105,001 − 2,470 = 102,531.
    /// No $50 rounding (≥ 100,001). Exact amount used.
    /// Bracket: $97,801+ → 3.9% × 102,531 − 89.30 = 3,998.709 − 89.30 = 3,909.409.
    /// Round annual gross tax → 3,909.41.
    /// Credits = 2 × $29 = $58.
    /// Net tax = 3,909.41 − 58 = 3,851.41.
    /// Per period = 3,851.41 / 1 = 3,851.41.
    /// </summary>
    [Fact]
    public void HighIncome_GrossTaxRounded_BeforeCredits()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(105001m, 1, 2);
        Assert.Equal(3851.41m, result);
    }

    // ── Exemptions and personal tax credits ─────────────────────────────

    /// <summary>
    /// Credits exceed gross tax → withholding floors at $0.
    /// Annual gross = 8,100, 1 period. Net = 5,630 → rounded 5,650.
    /// Gross tax = 2% × 5,650 − 111.98 = 1.02.
    /// Credits = 1 × $29 = $29.
    /// Net tax = max(0, 1.02 − 29) = $0.
    /// </summary>
    [Fact]
    public void Credits_ExceedGrossTax_FloorsAtZero()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(8100m, 1, 1);
        Assert.Equal(0m, result);
    }

    /// <summary>
    /// Multiple exemptions reduce tax.
    /// Biweekly $3,000, 26 periods, 5 exemptions.
    /// Annual gross = 78,000. Net = 78,000 − 2,470 = 75,530.
    /// Rounded = 75,550 (75,530/50 = 1510.6 → 1511 × 50 = 75,550).
    /// Bracket: $26,400–$94,700 → 3.9% × 75,550 − 419.96 = 2,946.45 − 419.96 = 2,526.49.
    /// Credits = 5 × $29 = $145.
    /// Net tax = 2,526.49 − 145 = 2,381.49.
    /// Per period = 2,381.49 / 26 = 91.60.
    /// </summary>
    [Fact]
    public void MultipleExemptions_ReduceTax()
    {
        var calc = LoadCalculator();
        var result = calc.CalculateWithholding(3000m, 26, 5);
        Assert.Equal(91.60m, result);
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZero()
    {
        var calc = LoadCalculator();
        Assert.Equal(0m, calc.CalculateWithholding(0m, 26, 0));
    }

    [Fact]
    public void NegativeGrossWages_ReturnsZero()
    {
        var calc = LoadCalculator();
        Assert.Equal(0m, calc.CalculateWithholding(-500m, 26, 0));
    }

    /// <summary>
    /// Gross income below the standard deduction → no tax.
    /// Annual gross = 80 × 26 = 2,080 &lt; 2,470 standard deduction.
    /// </summary>
    [Fact]
    public void IncomeBelowStandardDeduction_ReturnsZero()
    {
        var calc = LoadCalculator();
        Assert.Equal(0m, calc.CalculateWithholding(80m, 26, 0));
    }

    // ── Rounding tests ──────────────────────────────────────────────────

    [Fact]
    public void RoundToNearest50_ExamplesFromFormula()
    {
        // Per the DFA document: 23,054 = 23,050 and 23,099 = 23,100
        Assert.Equal(23050m, ArkansasFormulaCalculator.RoundToNearest50(23054m));
        Assert.Equal(23100m, ArkansasFormulaCalculator.RoundToNearest50(23099m));
    }

    [Fact]
    public void RoundToNearest50_ExactMultipleUnchanged()
    {
        Assert.Equal(50000m, ArkansasFormulaCalculator.RoundToNearest50(50000m));
    }

    [Fact]
    public void RoundToNearest50_MidpointRoundsUp()
    {
        // $25 above a $50 boundary → rounds up
        Assert.Equal(23100m, ArkansasFormulaCalculator.RoundToNearest50(23075m));
    }
}

// ─── ArkansasWithholdingCalculator Tests ────────────────────────────────

public class ArkansasWithholdingCalculatorTest
{
    private static ArkansasWithholdingCalculator LoadCalculator()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "ar_withholding_2026.json");
        var json = File.ReadAllText(dataPath);
        return new ArkansasWithholdingCalculator(new ArkansasFormulaCalculator(json));
    }

    [Fact]
    public void State_ReturnsArkansas()
    {
        var calc = LoadCalculator();
        Assert.Equal(UsState.AR, calc.State);
    }

    [Fact]
    public void Schema_HasThreeFields()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        Assert.Equal(3, schema.Count);
        Assert.Equal("FilingStatus", schema[0].Key);
        Assert.Equal("Exemptions", schema[1].Key);
        Assert.Equal("AdditionalWithholding", schema[2].Key);
    }

    [Fact]
    public void Schema_FilingStatusOptions()
    {
        var calc = LoadCalculator();
        var schema = calc.GetInputSchema();

        Assert.Equal(StateFieldType.Picker, schema[0].FieldType);
        Assert.Equal(3, schema[0].Options!.Count);
        Assert.Contains("Single", schema[0].Options!);
        Assert.Contains("Married Filing Jointly", schema[0].Options!);
        Assert.Contains("Head of Household", schema[0].Options!);
    }

    [Fact]
    public void Validate_ValidStatus_ReturnsNoErrors()
    {
        var calc = LoadCalculator();
        Assert.Empty(calc.Validate(new StateInputValues { ["FilingStatus"] = "Single" }));
        Assert.Empty(calc.Validate(new StateInputValues { ["FilingStatus"] = "Married Filing Jointly" }));
        Assert.Empty(calc.Validate(new StateInputValues { ["FilingStatus"] = "Head of Household" }));
    }

    [Fact]
    public void Validate_InvalidStatus_ReturnsError()
    {
        var calc = LoadCalculator();
        Assert.Single(calc.Validate(new StateInputValues { ["FilingStatus"] = "InvalidStatus" }));
    }

    /// <summary>
    /// Biweekly, $2,000 gross, Single, 0 exemptions, 0 additional.
    /// Same as the core calculator test above → $58.17.
    /// </summary>
    [Fact]
    public void Calculate_Biweekly_Single_ZeroExemptions()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(UsState.AR, 2000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Exemptions"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);
        Assert.Equal(58.17m, result.Withholding);
    }

    /// <summary>
    /// Biweekly, $2,000 gross, Head of Household, 0 exemptions, 0 additional.
    /// Arkansas uses the same formula for all filing statuses → $58.17.
    /// </summary>
    [Fact]
    public void Calculate_Biweekly_HeadOfHousehold_ZeroExemptions()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(UsState.AR, 2000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Head of Household",
            ["Exemptions"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);
        Assert.Equal(58.17m, result.Withholding);
    }

    /// <summary>
    /// Additional withholding is added to the calculated amount.
    /// </summary>
    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(UsState.AR, 2000m, PayFrequency.Biweekly, 2026);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Exemptions"] = 0,
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);
        Assert.Equal(58.17m + 25m, result.Withholding);
    }

    /// <summary>
    /// Pre-tax deductions reduce wages before state withholding calculation.
    /// Biweekly, $2,000 gross, $200 pre-tax deductions → effective $1,800.
    /// Annual gross = 1,800 × 26 = 46,800.
    /// Net = 46,800 − 2,470 = 44,330. Rounded = 44,350.
    /// Bracket: $26,400–$94,700 → 3.9% × 44,350 − 419.96 = 1,729.65 − 419.96 = 1,309.69.
    /// Per period = 1,309.69 / 26 = 50.37.
    /// </summary>
    [Fact]
    public void PreTaxDeductions_ReduceWages()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.AR, 2000m, PayFrequency.Biweekly, 2026,
            PreTaxDeductionsReducingStateWages: 200m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Single",
            ["Exemptions"] = 0,
            ["AdditionalWithholding"] = 0m
        };

        var result = calc.Calculate(context, values);
        Assert.Equal(50.37m, result.Withholding);
        Assert.Equal(1800m, result.TaxableWages);
    }

    [Fact]
    public void TaxableWages_EqualsGrossMinusPreTax()
    {
        var calc = LoadCalculator();
        var context = new CommonWithholdingContext(
            UsState.AR, 5000m, PayFrequency.Monthly, 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["FilingStatus"] = "Married Filing Jointly",
            ["Exemptions"] = 2
        };

        var result = calc.Calculate(context, values);
        Assert.Equal(4500m, result.TaxableWages);
    }
}
