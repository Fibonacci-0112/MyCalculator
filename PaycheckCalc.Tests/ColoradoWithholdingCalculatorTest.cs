using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.State;
using Xunit;

public class ColoradoWithholdingCalculatorTest
{
    [Fact]
    public void State_ReturnsColorado()
    {
        var calc = new ColoradoWithholdingCalculator();
        Assert.Equal(UsState.CO, calc.State);
    }

    // ── Schema ───────────────────────────────────────────────────────

    [Fact]
    public void Schema_ContainsAnnualWithholdingAllowance()
    {
        var calc = new ColoradoWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AnnualWithholdingAllowance");
        Assert.Equal("Annual Withholding Allowance Amount", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
        Assert.Equal(0m, field.DefaultValue);
    }

    [Fact]
    public void Schema_ContainsAdditionalWithholding()
    {
        var calc = new ColoradoWithholdingCalculator();
        var schema = calc.GetInputSchema();

        var field = Assert.Single(schema, f => f.Key == "AdditionalWithholding");
        Assert.Equal("Extra Withholding", field.Label);
        Assert.Equal(StateFieldType.Decimal, field.FieldType);
    }

    // ── Flat rate 4.4% ──────────────────────────────────────────────

    [Fact]
    public void FlatRate_AppliedToGrossWages()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // annual = 5000 * 26 = 130,000
        // tax    = 130000 * 0.044 = 5720
        // period = 5720 / 26 = 220.00
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(220.00m, result.Withholding);
    }

    // ── Annual Withholding Allowance ────────────────────────────────

    [Fact]
    public void WithholdingAllowance_ReducesAnnualTaxableIncome()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["AnnualWithholdingAllowance"] = 10000m
        };

        var result = calc.Calculate(context, values);

        // annual  = 5000 * 26 = 130,000
        // adjusted = 130000 - 10000 = 120,000
        // tax     = 120000 * 0.044 = 5280
        // period  = 5280 / 26 = 203.08 (rounded from 203.076923...)
        Assert.Equal(5000m, result.TaxableWages);
        Assert.Equal(203.08m, result.Withholding);
    }

    [Fact]
    public void WithholdingAllowance_ExceedsAnnualWages_FloorsAtZero()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Annual,
            Year: 2026);
        var values = new StateInputValues
        {
            ["AnnualWithholdingAllowance"] = 5000m
        };

        var result = calc.Calculate(context, values);

        // annual  = 1000 * 1 = 1000
        // adjusted = max(0, 1000 - 5000) = 0
        // tax     = 0
        Assert.Equal(1000m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
    }

    // ── FMLI (Family and Medical Leave Insurance) ───────────────────

    [Fact]
    public void Fmli_CalculatedOnGrossWages()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        // FMLI = 5000 * 0.00044 = 2.20
        Assert.Equal(2.20m, result.DisabilityInsurance);
    }

    [Fact]
    public void Fmli_UsesGrossWages_NotReducedByPreTaxDeductions()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 10000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);

        var result = calc.Calculate(context, new StateInputValues());

        // FMLI uses gross wages (10000), not reduced wages (8000)
        // FMLI = 10000 * 0.00044 = 4.40
        Assert.Equal(4.40m, result.DisabilityInsurance);
    }

    // ── Pre-tax deductions ──────────────────────────────────────────

    [Fact]
    public void PreTaxDeductions_ReduceTaxableWages()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 1000m);

        var result = calc.Calculate(context, new StateInputValues());

        // taxable wages = 5000 - 1000 = 4000
        // annual = 4000 * 26 = 104,000
        // tax    = 104000 * 0.044 = 4576
        // period = 4576 / 26 = 176.00
        Assert.Equal(4000m, result.TaxableWages);
        Assert.Equal(176.00m, result.Withholding);
    }

    // ── Additional withholding ──────────────────────────────────────

    [Fact]
    public void AdditionalWithholding_IsAdded()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 5000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);
        var values = new StateInputValues
        {
            ["AdditionalWithholding"] = 25m
        };

        var result = calc.Calculate(context, values);

        // 220.00 + 25 = 245.00
        Assert.Equal(245.00m, result.Withholding);
    }

    // ── Zero wages edge case ────────────────────────────────────────

    [Fact]
    public void ZeroGrossWages_ReturnsZeroWithholding()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 0m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
        Assert.Equal(0m, result.DisabilityInsurance);
    }

    // ── Deductions exceed gross ─────────────────────────────────────

    [Fact]
    public void DeductionsExceedGross_TaxableWagesFloorAtZero()
    {
        var calc = new ColoradoWithholdingCalculator();

        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 1000m,
            PayPeriod: PayFrequency.Monthly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 2000m);

        var result = calc.Calculate(context, new StateInputValues());

        Assert.Equal(0m, result.TaxableWages);
        Assert.Equal(0m, result.Withholding);
        // FMLI still applies to gross wages
        // FMLI = 1000 * 0.00044 = 0.44
        Assert.Equal(0.44m, result.DisabilityInsurance);
    }

    // ── Combined scenario ───────────────────────────────────────────

    [Fact]
    public void CombinedScenario_AllowanceAndDeductionsAndFmli()
    {
        var calc = new ColoradoWithholdingCalculator();

        // Biweekly employee: $4000 gross, $500 pre-tax, $6000 annual allowance
        var context = new CommonWithholdingContext(
            UsState.CO,
            GrossWages: 4000m,
            PayPeriod: PayFrequency.Biweekly,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 500m);
        var values = new StateInputValues
        {
            ["AnnualWithholdingAllowance"] = 6000m,
            ["AdditionalWithholding"] = 10m
        };

        var result = calc.Calculate(context, values);

        // taxable wages = 4000 - 500 = 3500
        // annual  = 3500 * 26 = 91,000
        // adjusted = 91000 - 6000 = 85,000
        // tax     = 85000 * 0.044 = 3740
        // period  = 3740 / 26 = 143.85 (rounded from 143.846153...)
        // withholding = 143.85 + 10 = 153.85
        Assert.Equal(3500m, result.TaxableWages);
        Assert.Equal(153.85m, result.Withholding);

        // FMLI = 4000 * 0.00044 = 1.76
        Assert.Equal(1.76m, result.DisabilityInsurance);
    }
}
