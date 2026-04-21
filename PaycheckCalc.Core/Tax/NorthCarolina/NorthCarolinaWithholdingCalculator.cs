using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.NorthCarolina;

/// <summary>
/// State module for North Carolina (NC) income tax withholding.
/// Implements the annualized percentage-method formula described in the
/// North Carolina Department of Revenue Publication NC-30 and
/// Form NC-4 (Employee's Withholding Allowance Certificate).
///
/// Calculation steps:
///   1. Compute per-period state taxable wages (gross − pre-tax deductions
///      that reduce state wages, floored at $0).
///   2. Annualize wages (× pay periods per year).
///   3. Subtract the filing-status standard deduction.
///   4. Subtract the NC-4 allowance deduction ($2,500 per allowance claimed).
///   5. Low-income exemption: floor annual taxable income at zero.
///   6. Apply North Carolina's 2026 flat income tax rate (4.5%).
///   7. De-annualize (÷ pay periods per year) and round to two decimal places.
///   8. Add any additional per-period withholding the employee requested on
///      Form NC-4.
///
/// Filing statuses (per Form NC-4):
///   • Single           — Single or Married Filing Separately.
///   • Married          — Married Filing Jointly or Qualifying Surviving Spouse.
///   • Head of Household — Head of Household.
///
/// 2026 North Carolina amounts (NC DOR Publication NC-30):
///   Standard deduction:
///     Single / MFS:          $12,750
///     Married Filing Jointly: $25,500
///     Head of Household:      $19,125
///   Per-allowance deduction (NC-4 Line 2): $2,500
///   Tax rate: flat 4.5% on all taxable income
///
/// Sources:
///   • North Carolina Department of Revenue, Publication NC-30, 2026
///     Income Tax Withholding Tables and Instructions for Employers.
///   • Form NC-4, Employee's Withholding Allowance Certificate.
///   • North Carolina Session Law 2023-134 (HB 259): rate reduction schedule.
/// </summary>
public sealed class NorthCarolinaWithholdingCalculator : IStateWithholdingCalculator
{
    // ── Standard deductions ──────────────────────────────────────────

    /// <summary>2026 NC standard deduction for Single / Married Filing Separately.</summary>
    public const decimal StandardDeductionSingle = 12_750m;

    /// <summary>2026 NC standard deduction for Married Filing Jointly / Qualifying Surviving Spouse.</summary>
    public const decimal StandardDeductionMarried = 25_500m;

    /// <summary>2026 NC standard deduction for Head of Household.</summary>
    public const decimal StandardDeductionHeadOfHousehold = 19_125m;

    // ── Per-allowance deduction ──────────────────────────────────────

    /// <summary>
    /// Annual deduction per allowance claimed on Form NC-4 Line 2.
    /// North Carolina uses $2,500 per allowance for tax year 2026.
    /// </summary>
    public const decimal AllowanceAmount = 2_500m;

    // ── Tax rate ─────────────────────────────────────────────────────

    /// <summary>North Carolina flat individual income tax rate for 2026 (4.5%).</summary>
    public const decimal TaxRate = 0.045m;

    // ── Filing status options exposed to the UI ──────────────────────

    public const string StatusSingle          = "Single";
    public const string StatusMarried         = "Married";
    public const string StatusHeadOfHousehold = "Head of Household";

    private static readonly IReadOnlyList<string> FilingStatusOptions =
        [StatusSingle, StatusMarried, StatusHeadOfHousehold];

    // ── Schema ───────────────────────────────────────────────────────

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "FilingStatus",
            Label = "NC-4 Filing Status",
            FieldType = StateFieldType.Picker,
            IsRequired = true,
            DefaultValue = StatusSingle,
            Options = FilingStatusOptions
        },
        new()
        {
            Key = "Allowances",
            Label = "NC-4 Allowances",
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

    public UsState State => UsState.NC;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();

        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!FilingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", FilingStatusOptions)}.");

        if (values.GetValueOrDefault("Allowances", 0) < 0)
            errors.Add("Allowances cannot be negative.");

        if (values.GetValueOrDefault("AdditionalWithholding", 0m) < 0m)
            errors.Add("Additional Withholding cannot be negative.");

        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatus     = values.GetValueOrDefault("FilingStatus", StatusSingle);
        var allowances       = Math.Max(0, values.GetValueOrDefault("Allowances", 0));
        var extraWithholding = Math.Max(0m, values.GetValueOrDefault("AdditionalWithholding", 0m));

        // Step 1: Per-period state taxable wages.
        var taxableWages = Math.Max(0m,
            context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        int periods = GetPayPeriods(context.PayPeriod);

        // Step 2: Annualize wages.
        var annualWages = taxableWages * periods;

        // Step 3: Subtract the filing-status standard deduction.
        var standardDeduction = filingStatus switch
        {
            StatusMarried         => StandardDeductionMarried,
            StatusHeadOfHousehold => StandardDeductionHeadOfHousehold,
            _                     => StandardDeductionSingle
        };

        // Step 4: Subtract the NC-4 allowance deduction ($2,500 per allowance).
        var allowanceDeduction = allowances * AllowanceAmount;

        // Step 5: Floor annual taxable income at zero.
        var annualTaxableIncome = Math.Max(0m,
            annualWages - standardDeduction - allowanceDeduction);

        // Step 6: Apply North Carolina's flat 4.5% rate.
        var annualTax = annualTaxableIncome * TaxRate;

        // Step 7: De-annualize and round to two decimal places.
        var periodTax   = annualTax / periods;
        var withholding = Math.Round(periodTax, 2, MidpointRounding.AwayFromZero);

        // Step 8: Add any per-period extra withholding.
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
        PayFrequency.Daily        => 260,
        PayFrequency.Weekly       => 52,
        PayFrequency.Biweekly     => 26,
        PayFrequency.Semimonthly  => 24,
        PayFrequency.Monthly      => 12,
        PayFrequency.Quarterly    => 4,
        PayFrequency.Semiannual   => 2,
        PayFrequency.Annual       => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
