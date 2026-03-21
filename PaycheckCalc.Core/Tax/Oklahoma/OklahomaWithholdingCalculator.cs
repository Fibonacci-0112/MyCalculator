using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Oklahoma;

/// <summary>
/// State module for Oklahoma that wraps the OW-2 percentage calculator
/// and exposes its inputs through the dynamic schema.
/// </summary>
public sealed class OklahomaWithholdingCalculator : IStateWithholdingCalculator
{
    private readonly OklahomaOw2PercentageCalculator _inner;

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "FilingStatus",
            Label = "Filing Status",
            FieldType = StateFieldType.Picker,
            IsRequired = true,
            DefaultValue = "Single",
            Options = ["Single", "Married"]
        },
        new()
        {
            Key = "Allowances",
            Label = "State Allowances",
            FieldType = StateFieldType.Integer,
            DefaultValue = 0
        },
        new()
        {
            Key = "AdditionalWithholding",
            Label = "Extra Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        }
    ];

    public OklahomaWithholdingCalculator(OklahomaOw2PercentageCalculator inner)
        => _inner = inner;

    public UsState State => UsState.OK;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (status != "Single" && status != "Married")
            errors.Add("Filing Status must be 'Single' or 'Married'.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus = values.GetValueOrDefault("FilingStatus", "Single") == "Married"
            ? FilingStatus.Married
            : FilingStatus.Single;
        var allowances = values.GetValueOrDefault("Allowances", 0);
        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        var allowancesTotal = allowances * _inner.GetAllowanceAmount(context.PayPeriod);
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages - allowancesTotal);
        var withholding = _inner.CalculateWithholding(taxableWages, context.PayPeriod, filingStatus)
                        + additionalWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
