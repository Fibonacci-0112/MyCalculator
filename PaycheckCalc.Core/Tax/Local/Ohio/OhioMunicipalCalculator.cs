using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Local.Ohio;

/// <summary>
/// Shared implementation for Ohio municipal income tax collected through a regional
/// agency (RITA or CCA). Each member municipality declares:
/// <list type="bullet">
///   <item><b>Rate</b> — the municipality's own income tax rate.</item>
///   <item><b>CreditRate</b> — the rate to credit a resident for taxes paid to their work municipality.</item>
///   <item><b>CreditCapRate</b> — cap, as a fraction of the work tax paid, on the allowable credit.</item>
/// </list>
/// <para>
/// Rules implemented:
/// <list type="number">
///   <item>Non-resident working in a member muni: pay the work muni's rate on wages. No credit math.</item>
///   <item>Resident working outside the member muni (or at home): pay resident muni's rate with no credit.</item>
///   <item>Resident working in a second member muni: pay resident muni's rate on wages, then subtract min(CreditRate × wages, CreditCapRate × work tax).</item>
/// </list>
/// </para>
/// </summary>
public abstract class OhioMunicipalCalculator : ILocalWithholdingCalculator
{
    public const string WorkMuniKey = "WorkMuni";
    public const string ResidentMuniKey = "ResidentMuni";
    public const string AdditionalWithholdingKey = "AdditionalWithholding";

    protected readonly OhioMuniRateTable Rates;

    protected OhioMunicipalCalculator(string json)
    {
        Rates = OhioMuniRateTable.Parse(json);
    }

    public abstract LocalityId Locality { get; }

    /// <summary>Human-readable agency name, e.g. "RITA" or "CCA". Used in descriptions.</summary>
    protected abstract string Agency { get; }

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() =>
    [
        new()
        {
            Key = ResidentMuniKey,
            Label = $"Resident {Agency} Municipality",
            FieldType = StateFieldType.Picker,
            Options = Rates.MuniCodes
        },
        new()
        {
            Key = WorkMuniKey,
            Label = $"Work {Agency} Municipality",
            FieldType = StateFieldType.Picker,
            Options = Rates.MuniCodes
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
        var resident = values.GetValueOrDefault<string>(ResidentMuniKey, string.Empty);
        var work = values.GetValueOrDefault<string>(WorkMuniKey, string.Empty);

        if (string.IsNullOrWhiteSpace(resident) && string.IsNullOrWhiteSpace(work))
            errors.Add($"At least one of Resident or Work {Agency} municipality is required.");

        if (!string.IsNullOrWhiteSpace(resident) && !Rates.TryGet(resident, out _))
            errors.Add($"Resident municipality '{resident}' is not a {Agency} member.");

        if (!string.IsNullOrWhiteSpace(work) && !Rates.TryGet(work, out _))
            errors.Add($"Work municipality '{work}' is not a {Agency} member.");

        return errors;
    }

    public LocalWithholdingResult Calculate(CommonLocalWithholdingContext context, LocalInputValues values)
    {
        var residentCode = values.GetValueOrDefault<string>(ResidentMuniKey, string.Empty);
        var workCode = values.GetValueOrDefault<string>(WorkMuniKey, string.Empty);
        var additional = values.GetValueOrDefault(AdditionalWithholdingKey, 0m);

        var taxable = Math.Max(0m,
            context.Common.GrossWages - context.Common.PreTaxDeductionsReducingStateWages);

        Rates.TryGet(residentCode, out var resident);
        Rates.TryGet(workCode, out var work);

        decimal withholding;
        string description;
        string localityName;

        if (work != null && (resident == null || string.Equals(residentCode, workCode, StringComparison.OrdinalIgnoreCase)))
        {
            // Pure work-muni withholding. Either the employee is not a resident of any
            // member muni, or they work where they live — no credit math either way.
            withholding = taxable * work.Rate;
            description = $"{Agency} work-muni {work.Name}: {work.Rate:P3} on wages.";
            localityName = work.Name;
        }
        else if (resident != null && work == null)
        {
            // Working outside any member muni (or at home). Resident muni taxes wages fully.
            withholding = taxable * resident.Rate;
            description = $"{Agency} resident {resident.Name}: {resident.Rate:P3} on wages (no work-muni credit).";
            localityName = resident.Name;
        }
        else if (resident != null && work != null)
        {
            // Resident in one member muni, working in a different member muni → credit rule.
            var workTax = taxable * work.Rate;
            var residentTax = taxable * resident.Rate;
            var credit = Math.Min(taxable * resident.CreditRate, workTax * resident.CreditCapRate);
            withholding = Math.Max(0m, residentTax - credit) + workTax;
            description =
                $"{Agency}: work {work.Name} {work.Rate:P3}, resident {resident.Name} {resident.Rate:P3}, credit {credit:C}.";
            localityName = $"{resident.Name} / {work.Name}";
        }
        else
        {
            // Neither muni recognized — nothing to withhold.
            return new LocalWithholdingResult
            {
                LocalityName = Locality.Name,
                TaxableWages = taxable,
                Withholding = 0m,
                Description = $"No {Agency} municipality supplied."
            };
        }

        return new LocalWithholdingResult
        {
            LocalityName = localityName,
            TaxableWages = taxable,
            Withholding = Math.Round(withholding, 2, MidpointRounding.AwayFromZero) + additional,
            Description = description
        };
    }
}

public sealed class OhRitaCalculator : OhioMunicipalCalculator
{
    public static readonly LocalityId LocalityKey =
        new(UsState.OH, "OH-RITA", "Ohio RITA Municipalities");

    public OhRitaCalculator(string json) : base(json) { }

    public override LocalityId Locality => LocalityKey;
    protected override string Agency => "RITA";
}

public sealed class OhCcaCalculator : OhioMunicipalCalculator
{
    public static readonly LocalityId LocalityKey =
        new(UsState.OH, "OH-CCA", "Ohio CCA Municipalities");

    public OhCcaCalculator(string json) : base(json) { }

    public override LocalityId Locality => LocalityKey;
    protected override string Agency => "CCA";
}

public sealed class OhioMuniRateTable
{
    private readonly Dictionary<string, OhioMuniEntry> _byCode;

    public IReadOnlyList<string> MuniCodes { get; }

    public int Year { get; }

    private OhioMuniRateTable(int year, Dictionary<string, OhioMuniEntry> byCode)
    {
        Year = year;
        _byCode = byCode;
        MuniCodes = byCode.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool TryGet(string code, out OhioMuniEntry? entry)
    {
        if (!string.IsNullOrWhiteSpace(code) && _byCode.TryGetValue(code, out var found))
        {
            entry = found;
            return true;
        }
        entry = null;
        return false;
    }

    public static OhioMuniRateTable Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<OhioTableDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Ohio municipal rate table JSON was empty.");

        var map = dto.Munis.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        return new OhioMuniRateTable(dto.Year, map);
    }

    private sealed class OhioTableDto
    {
        [JsonPropertyName("year")] public int Year { get; set; }
        [JsonPropertyName("munis")] public List<OhioMuniEntry> Munis { get; set; } = new();
    }
}

public sealed class OhioMuniEntry
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("rate")] public decimal Rate { get; set; }
    [JsonPropertyName("creditRate")] public decimal CreditRate { get; set; }
    [JsonPropertyName("creditCapRate")] public decimal CreditCapRate { get; set; } = 1m;
}
