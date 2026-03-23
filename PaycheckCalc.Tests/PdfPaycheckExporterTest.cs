using PaycheckCalc.Core.Export;
using PaycheckCalc.Core.Models;
using Xunit;

namespace PaycheckCalc.Tests;

public class PdfPaycheckExporterTest
{
    [Fact]
    public void Generate_StandardResult_ProducesNonEmptyByteArray()
    {
        var result = CreateSampleResult();

        var pdf = PdfPaycheckExporter.Generate(result);

        Assert.NotNull(pdf);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void Generate_StandardResult_StartsWithPdfHeader()
    {
        var result = CreateSampleResult();

        var pdf = PdfPaycheckExporter.Generate(result);

        // All valid PDFs start with the %PDF magic bytes
        Assert.True(pdf.Length > 4);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void Generate_NullResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PdfPaycheckExporter.Generate(null!));
    }

    [Fact]
    public void Generate_ZeroValues_ProducesValidPdf()
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

        var pdf = PdfPaycheckExporter.Generate(result);

        Assert.NotEmpty(pdf);
        Assert.Equal((byte)'%', pdf[0]);
    }

    [Fact]
    public void Generate_WithDeductions_ProducesValidPdf()
    {
        var result = new PaycheckResult
        {
            GrossPay = 5000m,
            PreTaxDeductions = 500m,
            PostTaxDeductions = 200m,
            State = UsState.CA,
            StateTaxableWages = 4500m,
            StateWithholding = 225m,
            StateDisabilityInsurance = 55m,
            SocialSecurityWithholding = 310m,
            MedicareWithholding = 72.50m,
            AdditionalMedicareWithholding = 10m,
            FederalTaxableIncome = 4500m,
            FederalWithholding = 400m,
            NetPay = 3227.50m
        };

        var pdf = PdfPaycheckExporter.Generate(result);

        Assert.NotEmpty(pdf);
        // Should be a reasonable size for a single-page document
        Assert.True(pdf.Length > 500);
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
