using PaycheckCalc.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PaycheckCalc.Core.Export;

/// <summary>
/// Generates a PDF document from a <see cref="SelfEmploymentResult"/>.
/// </summary>
public static class PdfSelfEmploymentExporter
{
    /// <summary>
    /// Produces a PDF byte array containing a formatted self-employment tax summary.
    /// </summary>
    public static byte[] Generate(SelfEmploymentResult result)
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
                    col.Item().Text("Self-Employment Tax Summary")
                        .FontSize(22).Bold().FontColor(Colors.Teal.Darken2);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(6);

                    // Schedule C
                    SectionHeader(col, "Schedule C — Net Profit");
                    ResultRow(col, "Gross Revenue", result.GrossRevenue);
                    if (result.CostOfGoodsSold > 0)
                        ResultRow(col, "Cost of Goods Sold", result.CostOfGoodsSold);
                    ResultRow(col, "Business Expenses", result.TotalExpenses);
                    ResultRow(col, "Net Profit", result.NetProfit, bold: true);

                    // SE Tax
                    SectionHeader(col, "Self-Employment Tax (Schedule SE)");
                    ResultRow(col, "SE Taxable Earnings (92.35%)", result.SeTaxableEarnings);
                    ResultRow(col, "Social Security Tax (12.4%)", result.SocialSecurityTax);
                    ResultRow(col, "Medicare Tax (2.9%)", result.MedicareTax);
                    if (result.AdditionalMedicareTax > 0)
                        ResultRow(col, "Additional Medicare Tax (0.9%)", result.AdditionalMedicareTax);
                    ResultRow(col, "Total SE Tax", result.TotalSeTax, bold: true);
                    ResultRow(col, "Deductible Half of SE Tax", result.DeductibleHalfOfSeTax);

                    // Income Tax
                    SectionHeader(col, "Income Tax");
                    if (result.OtherIncome > 0)
                        ResultRow(col, "Other Income", result.OtherIncome);
                    ResultRow(col, "Adjusted Gross Income", result.AdjustedGrossIncome);
                    ResultRow(col, "Standard Deduction", result.StandardDeduction);
                    if (result.QbiDeduction > 0)
                        ResultRow(col, "QBI Deduction (Section 199A)", result.QbiDeduction);
                    ResultRow(col, "Taxable Income", result.TaxableIncome);
                    ResultRow(col, "Federal Income Tax", result.FederalIncomeTax);
                    ResultRow(col, "State Income Tax (" + result.State + ")", result.StateIncomeTax);

                    // Summary
                    SectionHeader(col, "Summary");
                    ResultRow(col, "Total Federal Tax (Income + SE)", result.TotalFederalTax, bold: true);
                    ResultRow(col, "Total State Tax", result.TotalStateTax);

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Text("Total Tax").Bold().FontSize(14);
                        row.ConstantItem(120).AlignRight()
                            .Text(result.TotalTax.ToString("C"))
                            .Bold().FontSize(14).FontColor(Colors.Red.Darken2);
                    });

                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"Effective Tax Rate: {result.EffectiveTaxRate:F2}%")
                            .FontSize(12).FontColor(Colors.Grey.Darken1);
                    });

                    // Quarterly Estimates
                    SectionHeader(col, "Quarterly Estimated Payments (Form 1040-ES)");
                    ResultRow(col, "Estimated Quarterly Payment", result.EstimatedQuarterlyPayment, bold: true);

                    if (result.OverUnderPayment != 0)
                    {
                        var label = result.OverUnderPayment > 0 ? "Overpayment (Refund)" : "Underpayment (Balance Due)";
                        var color = result.OverUnderPayment > 0 ? Colors.Green.Darken2 : Colors.Red.Darken2;
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text(label).Bold();
                            row.ConstantItem(120).AlignRight()
                                .Text(Math.Abs(result.OverUnderPayment).ToString("C"))
                                .Bold().FontColor(color);
                        });
                    }
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
            .FontSize(13).Bold().FontColor(Colors.Teal.Darken1);
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
