using PaycheckCalc.Core.Export;
using PaycheckCalc.Core.Models;
using Xunit;

namespace PaycheckCalc.Tests;

public class CsvSelfEmploymentExporterTest
{
    private static SelfEmploymentResult SampleResult() => new()
    {
        GrossRevenue = 120_000m,
        CostOfGoodsSold = 5_000m,
        TotalExpenses = 20_000m,
        NetProfit = 95_000m,
        SeTaxableEarnings = 87_732.50m,
        SocialSecurityTax = 10_878.83m,
        MedicareTax = 2_544.24m,
        AdditionalMedicareTax = 0m,
        TotalSeTax = 13_423.07m,
        DeductibleHalfOfSeTax = 6_711.54m,
        OtherIncome = 10_000m,
        AdjustedGrossIncome = 98_288.46m,
        StandardDeduction = 15_700m,
        QbiDeduction = 16_517.69m,
        TaxableIncome = 66_070.77m,
        FederalIncomeTax = 9_000m,
        State = UsState.TX,
        StateIncomeTax = 0m,
        TotalFederalTax = 22_423.07m,
        TotalStateTax = 0m,
        TotalTax = 22_423.07m,
        EffectiveTaxRate = 17.25m,
        EstimatedQuarterlyPayment = 5_605.77m,
        OverUnderPayment = -22_423.07m
    };

    [Fact]
    public void Generate_NullResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CsvSelfEmploymentExporter.Generate(null!));
    }

    [Fact]
    public void Generate_ContainsHeaderRow()
    {
        var csv = CsvSelfEmploymentExporter.Generate(SampleResult());
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Field,Amount", lines[0]);
    }

    [Fact]
    public void Generate_ContainsScheduleCFields()
    {
        var csv = CsvSelfEmploymentExporter.Generate(SampleResult());
        Assert.Contains("Gross Revenue,120000", csv);
        Assert.Contains("Cost of Goods Sold,5000", csv);
        Assert.Contains("Business Expenses,20000", csv);
        Assert.Contains("Net Profit,95000", csv);
    }

    [Fact]
    public void Generate_ContainsSeTaxFields()
    {
        var csv = CsvSelfEmploymentExporter.Generate(SampleResult());
        Assert.Contains("SE Taxable Earnings (92.35%),87732.50", csv);
        Assert.Contains("Total SE Tax,13423.07", csv);
        Assert.Contains("Deductible Half of SE Tax,6711.54", csv);
    }

    [Fact]
    public void Generate_ContainsFederalTaxFields()
    {
        var csv = CsvSelfEmploymentExporter.Generate(SampleResult());
        Assert.Contains("Federal Income Tax,9000", csv);
        Assert.Contains("QBI Deduction,16517.69", csv);
        Assert.Contains("Taxable Income,66070.77", csv);
    }

    [Fact]
    public void Generate_ContainsSummaryFields()
    {
        var csv = CsvSelfEmploymentExporter.Generate(SampleResult());
        Assert.Contains("Total Tax,22423.07", csv);
        Assert.Contains("Effective Tax Rate (%),17.25", csv);
        Assert.Contains("Estimated Quarterly Payment,5605.77", csv);
        Assert.Contains("Over/Under Payment,-22423.07", csv);
    }

    [Fact]
    public void Generate_ContainsStateField()
    {
        var csv = CsvSelfEmploymentExporter.Generate(SampleResult());
        Assert.Contains("State,TX", csv);
    }

    [Fact]
    public void Generate_ProducesCorrectRowCount()
    {
        var csv = CsvSelfEmploymentExporter.Generate(SampleResult());
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        // Header + 24 data rows = 25
        Assert.Equal(25, lines.Length);
    }
}
