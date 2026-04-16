using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Illinois;

/// <summary>
/// State module for Illinois.  Flat 4.95% income tax with allowance-based
/// exemptions from the IL-W-4.
///
/// Calculation steps:
///   1. Compute annual exemption = (Basic Allowances × $2,925) + (Additional Allowances × $1,000).
///   2. Per-period exemption = annual exemption ÷ number of pay periods per year.
///   3. Taxable wages per period = gross wages − pre-tax deductions − per-period exemption (floored at 0).
///   4. Withholding = taxable wages × 4.95%, rounded to two decimal places.
///   5. Add any extra withholding the employee requested.
/// </summary>
public sealed class IllinoisWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>Illinois flat income tax rate (4.95%).</summary>
    private const decimal FlatRate = 0.0495m;

    /// <summary>Annual exemption per basic allowance (IL-W-4 Line 1).</summary>
    private const decimal BasicAllowanceAmount = 2_925m;

    /// <summary>Annual exemption per additional allowance (IL-W-4 Line 2).</summary>
    private const decimal AdditionalAllowanceAmount = 1_000m;

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "BasicAllowances",
            Label = "Basic Allowances",
            FieldType = StateFieldType.Integer,
            DefaultValue = 0
        },
        new()
        {
            Key = "AdditionalAllowances",
            Label = "Additional Allowances",
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

    public UsState State => UsState.IL;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var basicAllowances = values.GetValueOrDefault("BasicAllowances", 0);
        var additionalAllowances = values.GetValueOrDefault("AdditionalAllowances", 0);
        var extraWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 1: Annual exemption from IL-W-4 allowances
        decimal annualExemption = (basicAllowances * BasicAllowanceAmount)
                                + (additionalAllowances * AdditionalAllowanceAmount);

        // Step 2: Per-period exemption
        decimal exemptionPerPeriod = annualExemption / periods;

        // Step 3: Taxable amount after exemptions (floored at zero)
        decimal taxableAmount = Math.Max(0m, taxableWages - exemptionPerPeriod);

        // Step 4: Flat 4.95% withholding
        decimal withholding = Math.Round(taxableAmount * FlatRate, 2, MidpointRounding.AwayFromZero);

        // Step 5: Add extra withholding
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
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
