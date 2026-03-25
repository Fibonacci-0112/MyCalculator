using PaycheckCalc.Core.Export;
using PaycheckCalc.Core.Models;
using Xunit;

namespace PaycheckCalc.Tests;

public class CsvPaycheckExporterTest
{
    [Fact]
    public void Generate_StandardResult_ContainsHeaderRow()
    {
        var result = CreateSampleResult();

        var csv = CsvPaycheckExporter.Generate(result);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Field,Amount", lines[0]);
    }

    [Fact]
    public void Generate_StandardResult_ContainsAllFields()
    {
        var result = CreateSampleResult();

        var csv = CsvPaycheckExporter.Generate(result);

        Assert.Contains("Gross Pay,2187.50", csv);
        Assert.Contains("Pre-Tax Deductions,200", csv);
        Assert.Contains("Post-Tax Deductions,100", csv);
        Assert.Contains("Federal Taxable Income,1987.50", csv);
        Assert.Contains("Federal Withholding,100.00", csv);
        Assert.Contains("Social Security Tax,135.63", csv);
        Assert.Contains("Medicare Tax,31.72", csv);
        Assert.Contains("Additional Medicare Tax,0", csv);
        Assert.Contains("State,OK", csv);
        Assert.Contains("State Taxable Wages,1987.50", csv);
        Assert.Contains("State Withholding,75.00", csv);
        Assert.Contains("State Disability Insurance,0", csv);
        Assert.Contains("Net Pay,1500.00", csv);
    }

    [Fact]
    public void Generate_StandardResult_ProducesCorrectRowCount()
    {
        var result = CreateSampleResult();

        var csv = CsvPaycheckExporter.Generate(result);

        // 1 header + 14 data rows
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(15, lines.Length);
    }

    [Fact]
    public void Generate_TotalTaxes_MatchesSumOfWithholdings()
    {
        var result = CreateSampleResult();

        var csv = CsvPaycheckExporter.Generate(result);

        // Total taxes = 100 + 135.63 + 31.72 + 0 + 75 + 0 = 342.35
        Assert.Contains("Total Taxes,342.35", csv);
    }

    [Fact]
    public void Generate_NullResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CsvPaycheckExporter.Generate(null!));
    }

    [Fact]
    public void Generate_ZeroValues_ProducesValidCsv()
    {
        var result = new PaycheckResult
        {
            GrossPay = 0m,
            PreTaxDeductions = 0m,
            PostTaxDeductions = 0m,
            State = UsState.TX,
            StateTaxableWages = 0m,
            StateWithholding = 0m,
            StateDisabilityInsurance = 0m,
            SocialSecurityWithholding = 0m,
            MedicareWithholding = 0m,
            AdditionalMedicareWithholding = 0m,
            FederalTaxableIncome = 0m,
            FederalWithholding = 0m,
            NetPay = 0m
        };

        var csv = CsvPaycheckExporter.Generate(result);

        Assert.Contains("Gross Pay,0", csv);
        Assert.Contains("Net Pay,0", csv);
        Assert.Contains("State,TX", csv);
    }

    [Fact]
    public void Generate_WithStateDisabilityInsurance_IncludesSdiAmount()
    {
        var result = new PaycheckResult
        {
            GrossPay = 3000m,
            PreTaxDeductions = 0m,
            PostTaxDeductions = 0m,
            State = UsState.CA,
            StateTaxableWages = 3000m,
            StateWithholding = 150m,
            StateDisabilityInsurance = 33m,
            StateDisabilityInsuranceLabel = "State Disability Insurance (SDI)",
            SocialSecurityWithholding = 186m,
            MedicareWithholding = 43.50m,
            AdditionalMedicareWithholding = 0m,
            FederalTaxableIncome = 3000m,
            FederalWithholding = 200m,
            NetPay = 2387.50m
        };

        var csv = CsvPaycheckExporter.Generate(result);

        Assert.Contains("State Disability Insurance (SDI),33", csv);
        Assert.Contains("State,CA", csv);
    }

    [Fact]
    public void Generate_ConnecticutResult_UsesFamilyLeaveInsuranceLabel()
    {
        var result = new PaycheckResult
        {
            GrossPay = 5000m,
            PreTaxDeductions = 0m,
            PostTaxDeductions = 0m,
            State = UsState.CT,
            StateTaxableWages = 5000m,
            StateWithholding = 200m,
            StateDisabilityInsurance = 25m,
            StateDisabilityInsuranceLabel = "Family Leave Insurance (FLI)",
            SocialSecurityWithholding = 310m,
            MedicareWithholding = 72.50m,
            AdditionalMedicareWithholding = 0m,
            FederalTaxableIncome = 5000m,
            FederalWithholding = 400m,
            NetPay = 3992.50m
        };

        var csv = CsvPaycheckExporter.Generate(result);

        Assert.Contains("Family Leave Insurance (FLI),25", csv);
        Assert.DoesNotContain("State Disability Insurance", csv);
    }

    private static PaycheckResult CreateSampleResult() => new()
    {
        GrossPay = 2187.50m,
        PreTaxDeductions = 200m,
        PostTaxDeductions = 100m,
        State = UsState.OK,
        StateTaxableWages = 1987.50m,
        StateWithholding = 75.00m,
        StateDisabilityInsurance = 0m,
        SocialSecurityWithholding = 135.63m,
        MedicareWithholding = 31.72m,
        AdditionalMedicareWithholding = 0m,
        FederalTaxableIncome = 1987.50m,
        FederalWithholding = 100.00m,
        NetPay = 1500.00m
    };
}
