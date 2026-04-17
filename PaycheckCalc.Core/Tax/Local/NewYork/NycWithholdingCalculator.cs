using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Local.NewYork;

/// <summary>
/// New York City resident-only income-tax withholding.
/// <para>
/// NYC tax is charged solely on residents (statutory rule — commuters do not owe NYC tax).
/// Non-residents get zero withholding. Taxable wages are annualized, a marginal
/// bracket calculation is performed, then the annual tax is deannualized back to the
/// current pay period. Source: Publication NYS-50-T-NYC (2026 release).
/// </para>
/// </summary>
public sealed class NycWithholdingCalculator : ILocalWithholdingCalculator
{
    public const string FilingStatusKey = "FilingStatus";
    public const string AdditionalWithholdingKey = "AdditionalWithholding";

    public const string StatusSingle = "Single";
    public const string StatusMarried = "MarriedFilingJointly";
    public const string StatusHoh = "HeadOfHousehold";

    private static readonly IReadOnlyList<string> StatusOptions =
        [StatusSingle, StatusMarried, StatusHoh];

    public static readonly LocalityId LocalityKey =
        new(UsState.NY, "NY-NYC", "New York City");

    private readonly NycRateTable _rates;

    public NycWithholdingCalculator(string json)
    {
        _rates = NycRateTable.Parse(json);
    }

    public LocalityId Locality => LocalityKey;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() =>
    [
        new()
        {
            Key = FilingStatusKey,
            Label = "NYC Filing Status",
            FieldType = StateFieldType.Picker,
            IsRequired = true,
            DefaultValue = StatusSingle,
            Options = StatusOptions
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
        var status = values.GetValueOrDefault<string>(FilingStatusKey, StatusSingle);
        if (!StatusOptions.Contains(status))
            return [$"Unknown NYC filing status '{status}'."];
        return [];
    }

    public LocalWithholdingResult Calculate(CommonLocalWithholdingContext context, LocalInputValues values)
    {
        // Statutory rule: NYC income tax applies to residents only.
        if (!context.IsResident)
        {
            return new LocalWithholdingResult
            {
                LocalityName = LocalityKey.Name,
                TaxableWages = 0m,
                Withholding = 0m,
                Description = "Non-resident — NYC income tax applies to residents only."
            };
        }

        var status = values.GetValueOrDefault<string>(FilingStatusKey, StatusSingle);
        var additional = values.GetValueOrDefault(AdditionalWithholdingKey, 0m);

        var taxable = Math.Max(0m,
            context.Common.GrossWages - context.Common.PreTaxDeductionsReducingStateWages);

        var periods = PayPeriodsPerYear(context.Common.PayPeriod);
        var annualized = taxable * periods;
        var annualTax = _rates.AnnualTax(status, annualized);
        var perPeriod = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);

        return new LocalWithholdingResult
        {
            LocalityName = LocalityKey.Name,
            TaxableWages = taxable,
            Withholding = perPeriod + additional
        };
    }

    private static int PayPeriodsPerYear(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        PayFrequency.Daily => 260,
        _ => 26
    };
}

internal sealed class NycRateTable
{
    private readonly Dictionary<string, List<NycBracket>> _byStatus;

    private NycRateTable(Dictionary<string, List<NycBracket>> byStatus)
    {
        _byStatus = byStatus;
    }

    public static NycRateTable Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<NycTableDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("NYC withholding JSON was empty.");

        var map = new Dictionary<string, List<NycBracket>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (status, data) in dto.Statuses)
        {
            map[status] = data.Brackets
                .OrderBy(b => b.Min)
                .ToList();
        }

        return new NycRateTable(map);
    }

    public decimal AnnualTax(string status, decimal annualized)
    {
        if (annualized <= 0m) return 0m;
        if (!_byStatus.TryGetValue(status, out var brackets) || brackets.Count == 0)
            return 0m;

        decimal tax = 0m;
        for (int i = 0; i < brackets.Count; i++)
        {
            var lower = brackets[i].Min;
            var upper = i + 1 < brackets.Count ? brackets[i + 1].Min : decimal.MaxValue;
            if (annualized <= lower) break;

            var taxableInBracket = Math.Min(annualized, upper) - lower;
            tax += taxableInBracket * brackets[i].Rate;
        }

        return tax;
    }

    private sealed class NycTableDto
    {
        [JsonPropertyName("statuses")] public Dictionary<string, NycStatusDto> Statuses { get; set; } = new();
    }

    private sealed class NycStatusDto
    {
        [JsonPropertyName("brackets")] public List<NycBracket> Brackets { get; set; } = new();
    }
}

internal sealed class NycBracket
{
    [JsonPropertyName("min")] public decimal Min { get; set; }
    [JsonPropertyName("rate")] public decimal Rate { get; set; }
}
