using System.Globalization;
using System.Text;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Export;

/// <summary>
/// Generates a CSV representation of a <see cref="SelfEmploymentResult"/>.
/// </summary>
public static class CsvSelfEmploymentExporter
{
    /// <summary>
    /// Produces a two-column CSV (Field, Amount) from a self-employment result.
    /// </summary>
    public static string Generate(SelfEmploymentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine("Field,Amount");

        // Schedule C
        AppendRow(sb, "Gross Revenue", result.GrossRevenue);
        AppendRow(sb, "Cost of Goods Sold", result.CostOfGoodsSold);
        AppendRow(sb, "Business Expenses", result.TotalExpenses);
        AppendRow(sb, "Net Profit", result.NetProfit);

        // SE Tax
        AppendRow(sb, "SE Taxable Earnings (92.35%)", result.SeTaxableEarnings);
        AppendRow(sb, "Social Security Tax", result.SocialSecurityTax);
        AppendRow(sb, "Medicare Tax", result.MedicareTax);
        AppendRow(sb, "Additional Medicare Tax", result.AdditionalMedicareTax);
        AppendRow(sb, "Total SE Tax", result.TotalSeTax);
        AppendRow(sb, "Deductible Half of SE Tax", result.DeductibleHalfOfSeTax);

        // Income Tax
        AppendRow(sb, "Other Income", result.OtherIncome);
        AppendRow(sb, "Adjusted Gross Income", result.AdjustedGrossIncome);
        AppendRow(sb, "Standard Deduction", result.StandardDeduction);
        AppendRow(sb, "QBI Deduction", result.QbiDeduction);
        AppendRow(sb, "Taxable Income", result.TaxableIncome);
        AppendRow(sb, "Federal Income Tax", result.FederalIncomeTax);
        AppendRow(sb, "State", result.State.ToString());
        AppendRow(sb, "State Income Tax", result.StateIncomeTax);

        // Summary
        AppendRow(sb, "Total Federal Tax", result.TotalFederalTax);
        AppendRow(sb, "Total State Tax", result.TotalStateTax);
        AppendRow(sb, "Total Tax", result.TotalTax);
        AppendRow(sb, "Effective Tax Rate (%)", result.EffectiveTaxRate);
        AppendRow(sb, "Estimated Quarterly Payment", result.EstimatedQuarterlyPayment);
        AppendRow(sb, "Over/Under Payment", result.OverUnderPayment);

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string field, decimal value) =>
        sb.AppendLine($"{field},{value.ToString(CultureInfo.InvariantCulture)}");

    private static void AppendRow(StringBuilder sb, string field, string value) =>
        sb.AppendLine($"{field},{value}");
}
