using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Arkansas;

/// <summary>
/// State module for Arkansas that wraps <see cref="ArkansasFormulaCalculator"/>
/// and exposes its inputs through the dynamic <see cref="IStateWithholdingCalculator"/> schema.
/// </summary>
public sealed class ArkansasWithholdingCalculator : IStateWithholdingCalculator
{
    private readonly ArkansasFormulaCalculator _inner;

    private static readonly IReadOnlyList<string> FilingStatusOptions =
        ["Single", "Married Filing Jointly", "Head of Household"];

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "FilingStatus",
            Label = "Filing Status",
            FieldType = StateFieldType.Picker,
            IsRequired = true,
            DefaultValue = "Single",
            Options = FilingStatusOptions
        },
        new()
        {
            Key = "Exemptions",
            Label = "# Exemptions",
            FieldType = StateFieldType.Integer,
            DefaultValue = 0
        },
        new()
        {
            Key = "AdditionalWithholding",
            Label = "Additional State Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        }
    ];

    public ArkansasWithholdingCalculator(ArkansasFormulaCalculator inner)
        => _inner = inner;

    public UsState State => UsState.AR;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!FilingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", FilingStatusOptions)}.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var exemptions = values.GetValueOrDefault("Exemptions", 0);
        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        int periods = GetPayPeriods(context.PayPeriod);
        var taxableWages = Math.Max(0m, context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        var withholding = _inner.CalculateWithholding(
            taxableWages,
            periods,
            exemptions);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding + additionalWithholding
        };
    }

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily => 260,
        PayFrequency.Weekly => 52,
        PayFrequency.Biweekly => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly => 12,
        PayFrequency.Quarterly => 4,
        PayFrequency.Semiannual => 2,
        PayFrequency.Annual => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
