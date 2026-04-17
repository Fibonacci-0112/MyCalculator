using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Local.Maryland;

/// <summary>
/// Maryland county income-tax surcharge. MD state withholding is calculated separately
/// by the generic state adapter; this calculator attributes the *county* portion
/// (a flat percent of taxable wages) to the local bucket so the accounting stays clean.
/// <para>
/// Rate source: Maryland Comptroller tax-year 2026 local income tax rate table.
/// Non-residents working in MD pay the 2.25% "non-resident special rate".
/// </para>
/// </summary>
public sealed class MdCountyCalculator : ILocalWithholdingCalculator
{
    public const string CountyKey = "County";
    public const string AdditionalWithholdingKey = "AdditionalWithholding";

    public static readonly LocalityId LocalityKey =
        new(UsState.MD, "MD-COUNTY", "Maryland County Surtax");

    private readonly MdCountyRateTable _rates;

    public MdCountyCalculator(string json)
    {
        _rates = MdCountyRateTable.Parse(json);
    }

    public LocalityId Locality => LocalityKey;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() =>
    [
        new()
        {
            Key = CountyKey,
            Label = "Maryland County",
            FieldType = StateFieldType.Picker,
            IsRequired = true,
            Options = _rates.CountyCodes
        },
        new()
        {
            Key = AdditionalWithholdingKey,
            Label = "Extra Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        }
    ];

    public IReadOnlyList<string> Validate(LocalInputValues values)
    {
        var county = values.GetValueOrDefault<string>(CountyKey, string.Empty);
        if (string.IsNullOrWhiteSpace(county))
            return ["Maryland County is required."];
        if (!_rates.TryGet(county, out _))
            return [$"Unknown Maryland county '{county}'."];
        return [];
    }

    public LocalWithholdingResult Calculate(CommonLocalWithholdingContext context, LocalInputValues values)
    {
        var county = values.GetValueOrDefault<string>(CountyKey, string.Empty);
        var additional = values.GetValueOrDefault(AdditionalWithholdingKey, 0m);

        var taxable = Math.Max(0m,
            context.Common.GrossWages - context.Common.PreTaxDeductionsReducingStateWages);

        if (!_rates.TryGet(county, out var entry) || entry == null)
        {
            return new LocalWithholdingResult
            {
                LocalityName = LocalityKey.Name,
                TaxableWages = taxable,
                Withholding = additional,
                Description = "County not recognized — no surtax applied."
            };
        }

        var withholding = Math.Round(taxable * entry.Rate, 2, MidpointRounding.AwayFromZero)
                        + additional;

        return new LocalWithholdingResult
        {
            LocalityName = entry.Name,
            TaxableWages = taxable,
            Withholding = withholding,
            Description = $"{entry.Name} county surtax {entry.Rate:P3}."
        };
    }
}

public sealed class MdCountyRateTable
{
    private readonly Dictionary<string, MdCountyEntry> _byCode;

    public IReadOnlyList<string> CountyCodes { get; }
    public int Year { get; }

    private MdCountyRateTable(int year, Dictionary<string, MdCountyEntry> byCode)
    {
        Year = year;
        _byCode = byCode;
        CountyCodes = byCode.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool TryGet(string code, out MdCountyEntry? entry)
    {
        if (!string.IsNullOrWhiteSpace(code) && _byCode.TryGetValue(code, out var found))
        {
            entry = found;
            return true;
        }
        entry = null;
        return false;
    }

    public static MdCountyRateTable Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<MdTableDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("MD county surtax JSON was empty.");

        return new MdCountyRateTable(
            dto.Year,
            dto.Counties.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase));
    }

    private sealed class MdTableDto
    {
        [JsonPropertyName("year")] public int Year { get; set; }
        [JsonPropertyName("counties")] public List<MdCountyEntry> Counties { get; set; } = new();
    }
}

public sealed class MdCountyEntry
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("rate")] public decimal Rate { get; set; }
}
