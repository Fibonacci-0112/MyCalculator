using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Local.Pennsylvania;

/// <summary>
/// Immutable rate table loaded from <c>pa_eit_2026.json</c>.
/// <para>
/// PA Act 32 requires the employer to withhold the <b>higher</b> of the employee's
/// resident EIT rate and the non-resident rate of the work-location taxing district.
/// This calculator therefore needs <i>two</i> PSD entries (home + work) to apply the rule.
/// </para>
/// </summary>
public sealed class PaEitRateTable
{
    private readonly Dictionary<string, PaEitEntry> _byPsd;

    public int Year { get; }

    public IReadOnlyCollection<PaEitEntry> Entries => _byPsd.Values;

    public PaEitRateTable(string json)
    {
        var dto = JsonSerializer.Deserialize<PaEitTableDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("PA EIT rate table JSON was empty.");

        Year = dto.Year;
        _byPsd = dto.Localities.ToDictionary(l => l.Psd, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string psd, out PaEitEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(psd) && _byPsd.TryGetValue(psd, out var found))
        {
            entry = found;
            return true;
        }

        entry = default!;
        return false;
    }

    private sealed class PaEitTableDto
    {
        [JsonPropertyName("year")] public int Year { get; set; }
        [JsonPropertyName("localities")] public List<PaEitEntry> Localities { get; set; } = new();
    }
}

public sealed class PaEitEntry
{
    [JsonPropertyName("psd")] public string Psd { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("county")] public string County { get; set; } = string.Empty;
    [JsonPropertyName("residentRate")] public decimal ResidentRate { get; set; }
    [JsonPropertyName("nonResidentRate")] public decimal NonResidentRate { get; set; }
}

/// <summary>
/// Pennsylvania Earned Income Tax calculator (Act 32). Registered under a single
/// synthetic locality id <c>PA-EIT</c>; the actual PSD codes for home and work are
/// supplied as schema inputs so we do not have to register ~2,500 per-PSD calculators.
/// <para>
/// Rule: withhold the greater of (home resident rate, work non-resident rate)
/// against gross wages (post-PA-state pretax deductions). Pennsylvania state EIT
/// is not reduced by federal or FICA items.
/// </para>
/// </summary>
public sealed class PaEitCalculator : ILocalWithholdingCalculator
{
    public const string HomePsdKey = "HomePsd";
    public const string WorkPsdKey = "WorkPsd";
    public const string AdditionalWithholdingKey = "AdditionalWithholding";

    public static readonly LocalityId LocalityKey =
        new(UsState.PA, "PA-EIT", "Pennsylvania Local EIT (Act 32)");

    private readonly PaEitRateTable _rates;

    public PaEitCalculator(PaEitRateTable rates)
    {
        _rates = rates;
    }

    public LocalityId Locality => LocalityKey;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() =>
    [
        new()
        {
            Key = HomePsdKey,
            Label = "Home PSD Code",
            FieldType = StateFieldType.Text,
            IsRequired = true
        },
        new()
        {
            Key = WorkPsdKey,
            Label = "Work PSD Code",
            FieldType = StateFieldType.Text,
            IsRequired = true
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
        var errors = new List<string>();
        var homePsd = values.GetValueOrDefault<string>(HomePsdKey, string.Empty);
        var workPsd = values.GetValueOrDefault<string>(WorkPsdKey, string.Empty);

        if (string.IsNullOrWhiteSpace(homePsd) && string.IsNullOrWhiteSpace(workPsd))
            errors.Add("At least one of Home PSD Code or Work PSD Code is required for PA Act 32.");

        if (!string.IsNullOrWhiteSpace(homePsd) && !_rates.TryGet(homePsd, out _))
            errors.Add($"Home PSD code '{homePsd}' is not in the PA EIT rate table.");

        if (!string.IsNullOrWhiteSpace(workPsd) && !_rates.TryGet(workPsd, out _))
            errors.Add($"Work PSD code '{workPsd}' is not in the PA EIT rate table.");

        return errors;
    }

    public LocalWithholdingResult Calculate(CommonLocalWithholdingContext context, LocalInputValues values)
    {
        var homePsd = values.GetValueOrDefault<string>(HomePsdKey, string.Empty);
        var workPsd = values.GetValueOrDefault<string>(WorkPsdKey, string.Empty);
        var additional = values.GetValueOrDefault(AdditionalWithholdingKey, 0m);

        var taxableWages = Math.Max(0m,
            context.Common.GrossWages - context.Common.PreTaxDeductionsReducingStateWages);

        decimal residentRate = 0m;
        decimal nonResidentRate = 0m;
        string resolvedName = LocalityKey.Name;

        if (_rates.TryGet(homePsd, out var home))
        {
            residentRate = home.ResidentRate;
            resolvedName = home.Name;
        }

        if (_rates.TryGet(workPsd, out var work))
            nonResidentRate = work.NonResidentRate;

        // Act 32: employer withholds the higher of the resident rate or the work
        // location's non-resident rate. If one side is unknown, the other still governs.
        var effectiveRate = Math.Max(residentRate, nonResidentRate);
        var withholding = Math.Round(taxableWages * effectiveRate, 2, MidpointRounding.AwayFromZero)
                        + additional;

        var description = residentRate > nonResidentRate
            ? $"Applied resident rate {residentRate:P3} (PSD {homePsd})."
            : residentRate < nonResidentRate
                ? $"Applied work-location non-resident rate {nonResidentRate:P3} (PSD {workPsd})."
                : $"Resident and work non-resident rates equal at {residentRate:P3}.";

        return new LocalWithholdingResult
        {
            LocalityName = resolvedName,
            TaxableWages = taxableWages,
            Withholding = withholding,
            Description = description
        };
    }
}
