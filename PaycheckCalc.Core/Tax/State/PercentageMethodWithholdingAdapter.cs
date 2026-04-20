using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Adapts <see cref="PercentageMethodStateTaxCalculator"/> to the
/// <see cref="IStateWithholdingCalculator"/> interface, providing
/// a standard schema for the ~39 states that use the annualized percentage method.
/// </summary>
public sealed class PercentageMethodWithholdingAdapter : IStateWithholdingCalculator
{
    private readonly PercentageMethodStateTaxCalculator _inner;

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

    public PercentageMethodWithholdingAdapter(UsState state, PercentageMethodConfig config)
    {
        _inner = new PercentageMethodStateTaxCalculator(state, config);
    }

    public UsState State => _inner.State;

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
        var filingStatusStr = values.GetValueOrDefault("FilingStatus", "Single");
        var filingStatus = filingStatusStr == "Married"
            ? FilingStatus.Married
            : FilingStatus.Single;
        var allowances = values.GetValueOrDefault("Allowances", 0);
        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        var result = _inner.CalculateWithholding(new StateTaxInput
        {
            GrossWages = context.GrossWages,
            Frequency = context.PayPeriod,
            FilingStatus = filingStatus,
            Allowances = allowances,
            AdditionalWithholding = additionalWithholding,
            PreTaxDeductionsReducingStateWages = context.PreTaxDeductionsReducingStateWages
        });

        var inputs = new List<ExplanationInput>
        {
            new("State", _inner.State.ToString()),
            new("Filing Status", filingStatus.ToString()),
            new("Pay Frequency", context.PayPeriod.ToString()),
            new("Gross Wages (period)", FormatMoney(context.GrossWages)),
            new("Pre-tax Deductions Reducing State Wages", FormatMoney(context.PreTaxDeductionsReducingStateWages)),
            new("State Taxable Wages (period)", FormatMoney(result.TaxableWages)),
            new("State Allowances", allowances.ToString()),
            new("Extra Withholding (period)", FormatMoney(additionalWithholding)),
        };

        return new StateWithholdingResult
        {
            TaxableWages = result.TaxableWages,
            Withholding = result.Withholding,
            Explanation = new LineItemExplanation(
                Title: "State Income Tax",
                Method: "Annualized percentage method (state bracket schedule)",
                Table: $"{_inner.State} {context.Year} — {filingStatus} brackets",
                Inputs: inputs)
        };
    }

    private static string FormatMoney(decimal v) =>
        v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}
