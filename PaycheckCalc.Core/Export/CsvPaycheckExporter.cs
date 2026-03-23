using System.Globalization;
using System.Text;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Export;

/// <summary>
/// Generates a CSV representation of a <see cref="PaycheckResult"/>.
/// </summary>
public static class CsvPaycheckExporter
{
    /// <summary>
    /// Produces a two-column CSV (Field, Amount) from a paycheck result.
    /// The returned string includes a header row and uses standard
    /// comma-separated format with quoted fields where necessary.
    /// </summary>
    public static string Generate(PaycheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine("Field,Amount");

        AppendRow(sb, "Gross Pay", result.GrossPay);
        AppendRow(sb, "Pre-Tax Deductions", result.PreTaxDeductions);
        AppendRow(sb, "Post-Tax Deductions", result.PostTaxDeductions);
        AppendRow(sb, "Federal Taxable Income", result.FederalTaxableIncome);
        AppendRow(sb, "Federal Withholding", result.FederalWithholding);
        AppendRow(sb, "Social Security Tax", result.SocialSecurityWithholding);
        AppendRow(sb, "Medicare Tax", result.MedicareWithholding);
        AppendRow(sb, "Additional Medicare Tax", result.AdditionalMedicareWithholding);
        AppendRow(sb, "State", result.State.ToString());
        AppendRow(sb, "State Taxable Wages", result.StateTaxableWages);
        AppendRow(sb, "State Withholding", result.StateWithholding);
        AppendRow(sb, "State Disability Insurance", result.StateDisabilityInsurance);
        AppendRow(sb, "Total Taxes", result.TotalTaxes);
        AppendRow(sb, "Net Pay", result.NetPay);

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string field, decimal value) =>
        sb.AppendLine($"{field},{value.ToString(CultureInfo.InvariantCulture)}");

    private static void AppendRow(StringBuilder sb, string field, string value) =>
        sb.AppendLine($"{field},{value}");
}
