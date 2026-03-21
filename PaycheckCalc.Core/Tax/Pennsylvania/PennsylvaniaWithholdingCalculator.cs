using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Pennsylvania;

/// <summary>
/// State module for Pennsylvania (flat 3.07% rate).
/// Filing status and allowances do not affect Pennsylvania withholding,
/// so the schema contains only an optional extra withholding field.
/// </summary>
public sealed class PennsylvaniaWithholdingCalculator : IStateWithholdingCalculator
{
    private const decimal FlatRate = 0.0307m;

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "AdditionalWithholding",
            Label = "Extra Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        }
    ];

    public UsState State => UsState.PA;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);
        var withholding = Math.Round(taxableWages * FlatRate, 2, MidpointRounding.AwayFromZero)
                        + values.GetValueOrDefault("AdditionalWithholding", 0m);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
