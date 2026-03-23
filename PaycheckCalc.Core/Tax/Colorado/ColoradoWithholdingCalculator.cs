using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Colorado;

/// <summary>
/// State module for Colorado.  Flat 4.4% income tax with an annual withholding
/// allowance that reduces taxable income, plus Family and Medical Leave Insurance
/// (FMLI) at 0.044% of gross wages.
/// </summary>
public sealed class ColoradoWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Colorado flat income tax rate (4.4%).</summary>
    private const decimal FlatRate = 0.044m;

    /// <summary>Colorado FMLI premium rate (0.044%).</summary>
    private const decimal FmliRate = 0.00044m;

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "AnnualWithholdingAllowance",
            Label = "Annual Withholding Allowance Amount",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        },
        new()
        {
            Key = "AdditionalWithholding",
            Label = "Extra Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        }
    ];

    public UsState State => UsState.CO;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Annualize, subtract the withholding allowance, apply flat rate, de-annualize
        var annualWages = taxableWages * periods;
        var allowance = values.GetValueOrDefault("AnnualWithholdingAllowance", 0m);
        annualWages = Math.Max(0m, annualWages - allowance);

        var annualTax = annualWages * FlatRate;
        var periodTax = annualTax / periods;

        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero)
                        + values.GetValueOrDefault("AdditionalWithholding", 0m);

        // FMLI: 0.044% of ALL gross wages (no wage cap)
        var fmli = Math.Round(Math.Max(0m, context.GrossWages) * FmliRate, 2, MidpointRounding.AwayFromZero);

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding,
            DisabilityInsurance = fmli
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
