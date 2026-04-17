using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Local.Pennsylvania;

/// <summary>
/// Pennsylvania Local Services Tax. Flat dollar amount per pay period, capped at
/// $52 per employee per calendar year. Localities charging more than $10/year
/// must prorate across pay periods; localities charging $10 or less are deducted
/// in a single lump. This calculator prorates evenly across the employee's pay periods.
/// <para>
/// Statutory exemption: employees whose total earned income in the municipality
/// is below $12,000/year may claim exemption via form LST-E. The schema exposes
/// a simple Exempt toggle for that case.
/// </para>
/// </summary>
public sealed class PaLstCalculator : ILocalWithholdingCalculator
{
    public const string AnnualAmountKey = "AnnualAmount";
    public const string ExemptKey = "Exempt";

    public static readonly LocalityId LocalityKey =
        new(UsState.PA, "PA-LST", "Pennsylvania Local Services Tax");

    /// <summary>Statutory maximum annual LST per employee per PA Act 7.</summary>
    public const decimal AnnualCap = 52m;

    public LocalityId Locality => LocalityKey;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() =>
    [
        new()
        {
            Key = AnnualAmountKey,
            Label = "Annual LST Amount",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 52m
        },
        new()
        {
            Key = ExemptKey,
            Label = "LST Exempt (LST-E on file)",
            FieldType = StateFieldType.Toggle,
            DefaultValue = false
        }
    ];

    public IReadOnlyList<string> Validate(LocalInputValues values)
    {
        var errors = new List<string>();
        var amount = values.GetValueOrDefault(AnnualAmountKey, 0m);

        if (amount < 0m)
            errors.Add("Annual LST amount cannot be negative.");
        if (amount > AnnualCap)
            errors.Add($"Annual LST amount cannot exceed the statutory cap of ${AnnualCap:F0}.");

        return errors;
    }

    public LocalWithholdingResult Calculate(CommonLocalWithholdingContext context, LocalInputValues values)
    {
        if (values.GetValueOrDefault(ExemptKey, false))
        {
            return new LocalWithholdingResult
            {
                LocalityName = LocalityKey.Name,
                HeadTax = 0m,
                HeadTaxLabel = "Local Services Tax",
                Description = "Exempt via LST-E."
            };
        }

        var annual = Math.Clamp(values.GetValueOrDefault(AnnualAmountKey, 0m), 0m, AnnualCap);
        var periods = PayPeriodsPerYear(context.Common.PayPeriod);
        var perPeriod = periods > 0
            ? Math.Round(annual / periods, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new LocalWithholdingResult
        {
            LocalityName = LocalityKey.Name,
            HeadTax = perPeriod,
            HeadTaxLabel = "Local Services Tax",
            Description = $"${annual:F2} annual LST prorated across {periods} pay periods."
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
