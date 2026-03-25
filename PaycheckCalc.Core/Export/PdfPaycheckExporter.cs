using PaycheckCalc.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PaycheckCalc.Core.Export;

/// <summary>
/// Generates a PDF document from a <see cref="PaycheckResult"/>.
/// </summary>
public static class PdfPaycheckExporter
{
    /// <summary>
    /// Produces a PDF byte array containing a formatted paycheck summary.
    /// </summary>
    public static byte[] Generate(PaycheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("Paycheck Summary")
                        .FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(6);

                    // Income section
                    SectionHeader(col, "Income");
                    ResultRow(col, "Gross Pay", result.GrossPay);
                    ResultRow(col, "Federal Taxable Income", result.FederalTaxableIncome);
                    ResultRow(col, "State Taxable Wages", result.StateTaxableWages);

                    // Deductions section
                    if (result.PreTaxDeductions > 0 || result.PostTaxDeductions > 0)
                    {
                        SectionHeader(col, "Deductions");
                        ResultRow(col, "Pre-Tax Deductions", result.PreTaxDeductions);
                        ResultRow(col, "Post-Tax Deductions", result.PostTaxDeductions);
                    }

                    // Tax withholdings section
                    SectionHeader(col, "Tax Withholdings");
                    ResultRow(col, "Federal Withholding", result.FederalWithholding);
                    ResultRow(col, "Social Security Tax", result.SocialSecurityWithholding);
                    ResultRow(col, "Medicare Tax", result.MedicareWithholding);

                    if (result.AdditionalMedicareWithholding > 0)
                        ResultRow(col, "Additional Medicare Tax", result.AdditionalMedicareWithholding);

                    ResultRow(col, "State Income Tax (" + result.State + ")", result.StateWithholding);

                    if (result.StateDisabilityInsurance > 0)
                        ResultRow(col, result.StateDisabilityInsuranceLabel, result.StateDisabilityInsurance);

                    // Totals section
                    SectionHeader(col, "Totals");
                    ResultRow(col, "Total Taxes", result.TotalTaxes, bold: true);

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Text("Net Pay").Bold().FontSize(14);
                        row.ConstantItem(120).AlignRight()
                            .Text(result.NetPay.ToString("C"))
                            .Bold().FontSize(14).FontColor(Colors.Green.Darken2);
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.Now.ToString("MMMM dd, yyyy"));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void SectionHeader(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(10).Text(title)
            .FontSize(13).Bold().FontColor(Colors.Blue.Darken1);
        col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
    }

    private static void ResultRow(ColumnDescriptor col, string label, decimal value, bool bold = false)
    {
        col.Item().Row(row =>
        {
            var labelText = row.RelativeItem().Text(label);
            if (bold) labelText.Bold();

            var valueText = row.ConstantItem(120).AlignRight().Text(value.ToString("C"));
            if (bold) valueText.Bold();
        });
    }
}
