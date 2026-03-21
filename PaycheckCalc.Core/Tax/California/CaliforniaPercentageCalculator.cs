using System.Text.Json;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.California;

/// <summary>
/// Calculates California state income tax withholding using Method B
/// from EDD Publication DE 44 (26methb.pdf).
/// <para>
/// Algorithm (Method B):
/// 1. Low-income exemption test (Table 1): if gross pay ≤ threshold, withhold $0.
/// 2. Estimated deduction adjustment (Table 2): subtract estimated-deduction allowances × per-period amount.
/// 3. Standard deduction subtraction (Table 3): subtract standard deduction for filing status and pay period.
/// 4. Compute tax on taxable income using graduated brackets for the filing status.
/// 5. Subtract exemption allowance credit (Table 4): regular allowances × per-period credit.
/// 6. Withholding = max(0, computed tax − exemption credit), rounded to 2 decimal places.
/// </para>
/// </summary>
public sealed class CaliforniaPercentageCalculator
{
    private readonly CaliforniaMethodBData _data;

    public CaliforniaPercentageCalculator(string json)
    {
        _data = JsonSerializer.Deserialize<CaliforniaMethodBData>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to load California Method B JSON data.");
    }

    /// <summary>
    /// Computes California PIT withholding for one pay period using Method B.
    /// </summary>
    /// <param name="grossPay">Gross wages for the payroll period (after pre-tax deductions that reduce state wages).</param>
    /// <param name="frequency">Pay frequency (weekly, biweekly, etc.).</param>
    /// <param name="filingStatus">California filing status from DE 4.</param>
    /// <param name="regularAllowances">Regular withholding allowances (DE 4 Line 1).</param>
    /// <param name="estimatedDeductionAllowances">Additional withholding allowances for estimated deductions (DE 4 Line 2).</param>
    /// <returns>Withholding amount for the pay period, rounded to 2 decimal places.</returns>
    public decimal CalculateWithholding(
        decimal grossPay,
        PayFrequency frequency,
        CaliforniaFilingStatus filingStatus,
        int regularAllowances,
        int estimatedDeductionAllowances)
    {
        if (grossPay <= 0m) return 0m;

        var freqKey = FrequencyKey(frequency);
        int periods = GetPayPeriods(frequency);

        // Determine whether to use the "high" threshold/deduction
        // High applies to: Head of Household always, or Married with 2+ regular allowances
        bool useHigh = filingStatus == CaliforniaFilingStatus.HeadOfHousehold
                    || (filingStatus == CaliforniaFilingStatus.Married && regularAllowances >= 2);

        // Step 1: Low-income exemption test (Table 1)
        var exemption = _data.LowIncomeExemption[freqKey];
        decimal threshold = useHigh ? exemption.High : exemption.Low;
        if (grossPay <= threshold) return 0m;

        // Step 2: Estimated deduction adjustment (Table 2)
        decimal deductionPerAllowance = _data.EstimatedDeductionPerAllowance[freqKey];
        decimal estimatedDeduction = estimatedDeductionAllowances * deductionPerAllowance;

        // Step 3: Standard deduction subtraction (Table 3)
        var stdDed = _data.StandardDeduction[freqKey];
        decimal standardDeduction = useHigh ? stdDed.High : stdDed.Low;

        decimal taxableIncome = Math.Max(0m, grossPay - estimatedDeduction - standardDeduction);
        if (taxableIncome <= 0m) return 0m;

        // Step 4: Compute tax using annualize/de-annualize approach
        decimal annualTaxable = taxableIncome * periods;
        var brackets = GetBrackets(filingStatus);
        decimal annualTax = ComputeTaxFromBrackets(annualTaxable, brackets);
        decimal perPeriodTax = annualTax / periods;

        // Step 5: Subtract exemption allowance credit (Table 4)
        decimal creditPerAllowance = _data.ExemptionAllowanceCreditPerAllowance[freqKey];
        decimal exemptionCredit = regularAllowances * creditPerAllowance;

        decimal withholding = Math.Max(0m, perPeriodTax - exemptionCredit);

        return Math.Round(withholding, 2, MidpointRounding.AwayFromZero);
    }

    private IReadOnlyList<CaliforniaBracket> GetBrackets(CaliforniaFilingStatus status) => status switch
    {
        CaliforniaFilingStatus.Single => _data.AnnualBrackets.Single,
        CaliforniaFilingStatus.Married => _data.AnnualBrackets.Married,
        CaliforniaFilingStatus.HeadOfHousehold => _data.AnnualBrackets.HeadOfHousehold,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported filing status")
    };

    private static decimal ComputeTaxFromBrackets(decimal taxableIncome, IReadOnlyList<CaliforniaBracket> brackets)
    {
        decimal tax = 0m;
        foreach (var bracket in brackets)
        {
            if (taxableIncome <= bracket.Over) break;
            decimal ceiling = bracket.NotOver ?? decimal.MaxValue;
            decimal taxableInBracket = Math.Min(taxableIncome, ceiling) - bracket.Over;
            tax += taxableInBracket * bracket.Rate;
        }
        return tax;
    }

    private static string FrequencyKey(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily => "daily",
        PayFrequency.Weekly => "weekly",
        PayFrequency.Biweekly => "biweekly",
        PayFrequency.Semimonthly => "semimonthly",
        PayFrequency.Monthly => "monthly",
        PayFrequency.Quarterly => "quarterly",
        PayFrequency.Semiannual => "semiannual",
        PayFrequency.Annual => "annual",
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };

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

/// <summary>
/// California DE 4 filing status for withholding purposes.
/// </summary>
public enum CaliforniaFilingStatus
{
    /// <summary>Single, or Dual-income married, or Married with multiple employers.</summary>
    Single,

    /// <summary>Married (one income).</summary>
    Married,

    /// <summary>Unmarried Head of Household.</summary>
    HeadOfHousehold
}
