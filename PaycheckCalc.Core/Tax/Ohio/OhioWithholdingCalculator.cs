using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Ohio;

/// <summary>
/// State module for Ohio (OH) income tax withholding.
/// Implements the annualized percentage method described in the Ohio
/// Department of Taxation "Employer Withholding Tax – Optional Computer
/// Formula" (effective January 1, 2026 per HB 96's phase-in of a
/// near-flat income tax).
///
/// Ohio employees submit Form IT-4 to claim the number of withholding
/// exemptions (one for the employee, one for a spouse, and one for each
/// dependent).  Ohio does not use filing status for withholding — the
/// same bracket table and per-exemption amount apply to every employee.
///
/// Calculation steps (Optional Computer Formula):
///   1. State taxable wages per period = gross wages − pre-tax deductions
///      that reduce state wages (floored at $0).
///   2. Annualize: annual wages = taxable wages × pay periods per year.
///   3. Subtract the IT-4 exemption allowance:
///        annual exemption = $650 × number of IT-4 exemptions.
///        annual taxable income = max(0, annual wages − annual exemption).
///   4. Apply Ohio's 2026 graduated annual brackets:
///        0%    on $0       – $26,050
///        2.75% on income over $26,050
///   5. Per-period withholding = annual tax ÷ pay periods per year,
///      rounded to two decimal places.
///   6. Add any per-period extra withholding requested by the employee.
///
/// Sources:
///   • Ohio Department of Taxation, 2026 Employer and School District
///     Withholding Tax Filing Guidelines.
///   • Ohio Department of Taxation, "Employer Withholding Tax – Optional
///     Computer Formula" (revised effective October 1, 2025, continuing
///     into tax year 2026).
///   • Ohio Form IT-4, "Employee's Withholding Exemption Certificate".
///   • Ohio HB 96 (2025) – income tax rate schedule phase-in.
/// </summary>
public sealed class OhioWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>
    /// Annual exemption allowance per IT-4 exemption claimed (2026): $650.
    /// Ohio's withholding formula subtracts this flat per-exemption amount
    /// from annualized wages before applying the bracket table; it is not
    /// the same as the Ohio personal-exemption amount used on the IT 1040
    /// return, which varies with modified adjusted gross income.
    /// </summary>
    public const decimal ExemptionAllowance = 650m;

    /// <summary>
    /// Ceiling of the zero-rate bracket ($26,050).  Annual taxable income
    /// up to this amount is taxed at 0% under the 2026 Ohio withholding
    /// schedule.
    /// </summary>
    public const decimal ZeroBracketCeiling = 26_050m;

    /// <summary>Ohio withholding rate on annual taxable income over $26,050 (2.75%).</summary>
    public const decimal TopRate = 0.0275m;

    // ── Schema ───────────────────────────────────────────────────────

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "Exemptions",
            Label = "IT-4 Exemptions",
            FieldType = StateFieldType.Integer,
            DefaultValue = 0
        },
        new()
        {
            Key = "AdditionalWithholding",
            Label = "Additional Withholding",
            FieldType = StateFieldType.Decimal,
            DefaultValue = 0m
        }
    ];

    // ── IStateWithholdingCalculator ──────────────────────────────────

    public UsState State => UsState.OH;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        if (values.GetValueOrDefault("Exemptions", 0) < 0)
            errors.Add("IT-4 Exemptions cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var exemptions       = Math.Max(0, values.GetValueOrDefault("Exemptions", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract the IT-4 exemption allowance ($650 per exemption).
        var annualExemption    = exemptions * ExemptionAllowance;
        var annualTaxableIncome = Math.Max(0m, annualWages - annualExemption);

        // Step 4: Apply Ohio's 2026 graduated brackets.
        decimal annualTax = annualTaxableIncome <= ZeroBracketCeiling
            ? 0m
            : (annualTaxableIncome - ZeroBracketCeiling) * TopRate;

        // Step 5: De-annualize and round to two decimal places.
        var withholding = Math.Round(annualTax / periods, 2, MidpointRounding.AwayFromZero);

        // Step 6: Add any per-period extra withholding.
        withholding += extraWithholding;

        return new StateWithholdingResult
        {
            TaxableWages = taxableWages,
            Withholding  = withholding
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int GetPayPeriods(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily       => 260,
        PayFrequency.Weekly      => 52,
        PayFrequency.Biweekly    => 26,
        PayFrequency.Semimonthly => 24,
        PayFrequency.Monthly     => 12,
        PayFrequency.Quarterly   => 4,
        PayFrequency.Semiannual  => 2,
        PayFrequency.Annual      => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
