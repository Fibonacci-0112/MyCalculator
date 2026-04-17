using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Applies IRS 2026 Form 1040 income tax brackets to a taxable income amount
/// and returns both the tax and the marginal rate at that income.
///
/// Data is loaded from the JSON file
/// <c>PaycheckCalc.Core/Data/Federal2026/federal_1040_brackets_2026.json</c>,
/// sourced from IRS Rev. Proc. 2025-32 (IR-2025-103).
///
/// NOTE: These are Form 1040 income tax brackets, NOT the Pub 15-T withholding
/// tables used by <see cref="Irs15TPercentageCalculator"/>. They operate on
/// taxable income (after standard/itemized + QBI deductions), not on wages
/// adjusted by W-4 worksheet lines.
/// </summary>
public sealed class Federal1040TaxCalculator
{
    private readonly BracketsRoot _data;

    public Federal1040TaxCalculator(string json)
    {
        _data = JsonSerializer.Deserialize<BracketsRoot>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(
                "Failed to load Federal 1040 bracket JSON data.");
    }

    public int TaxYear => _data.TaxYear;

    /// <summary>Standard deduction for the given filing status.</summary>
    public decimal GetStandardDeduction(FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => _data.StandardDeduction.MarriedFilingJointly,
        FederalFilingStatus.HeadOfHousehold      => _data.StandardDeduction.HeadOfHousehold,
        _                                        => _data.StandardDeduction.SingleOrMfs
    };

    /// <summary>
    /// Computes income tax on <paramref name="taxableIncome"/> for the given
    /// filing status. Returns 0 for non-positive taxable income.
    /// </summary>
    public decimal CalculateTax(decimal taxableIncome, FederalFilingStatus status)
    {
        if (taxableIncome <= 0m) return 0m;

        var brackets = GetBrackets(status);
        var b = FindBracket(brackets, taxableIncome);
        var tax = b.Base + (taxableIncome - b.ExcessOver) * b.Rate;
        return Math.Round(tax, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Marginal tax rate (0–1 decimal) at <paramref name="taxableIncome"/>.</summary>
    public decimal GetMarginalRate(decimal taxableIncome, FederalFilingStatus status)
    {
        if (taxableIncome <= 0m) return 0m;
        var brackets = GetBrackets(status);
        return FindBracket(brackets, taxableIncome).Rate;
    }

    private List<TaxBracket> GetBrackets(FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => _data.Brackets.MarriedFilingJointly,
        FederalFilingStatus.HeadOfHousehold      => _data.Brackets.HeadOfHousehold,
        _                                        => _data.Brackets.SingleOrMfs
    };

    private static TaxBracket FindBracket(List<TaxBracket> brackets, decimal income)
    {
        foreach (var b in brackets)
        {
            var underOk = b.Under is null || income < b.Under.Value;
            if (income >= b.Over && underOk) return b;
        }
        return brackets[^1];
    }

    // ── JSON shapes ─────────────────────────────────────────
    private sealed class BracketsRoot
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonPropertyName("taxYear")] public int TaxYear { get; set; }
        [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
        [JsonPropertyName("standardDeduction")] public StandardDeductionTable StandardDeduction { get; set; } = new();
        [JsonPropertyName("brackets")] public BracketsByStatus Brackets { get; set; } = new();
    }

    private sealed class StandardDeductionTable
    {
        [JsonPropertyName("single_or_mfs")] public decimal SingleOrMfs { get; set; }
        [JsonPropertyName("married_filing_jointly")] public decimal MarriedFilingJointly { get; set; }
        [JsonPropertyName("head_of_household")] public decimal HeadOfHousehold { get; set; }
    }

    private sealed class BracketsByStatus
    {
        [JsonPropertyName("single_or_mfs")] public List<TaxBracket> SingleOrMfs { get; set; } = new();
        [JsonPropertyName("married_filing_jointly")] public List<TaxBracket> MarriedFilingJointly { get; set; } = new();
        [JsonPropertyName("head_of_household")] public List<TaxBracket> HeadOfHousehold { get; set; } = new();
    }

    private sealed class TaxBracket
    {
        [JsonPropertyName("over")] public decimal Over { get; set; }
        [JsonPropertyName("under")] public decimal? Under { get; set; }
        [JsonPropertyName("base")] public decimal Base { get; set; }
        [JsonPropertyName("rate")] public decimal Rate { get; set; }
        [JsonPropertyName("excessOver")] public decimal ExcessOver { get; set; }
    }
}
