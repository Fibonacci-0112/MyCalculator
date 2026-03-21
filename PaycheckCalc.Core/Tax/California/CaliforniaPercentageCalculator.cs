using System.Text.Json;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.California;

/// <summary>
/// Calculates California state income tax withholding using Method B
/// from EDD Publication DE 44 (26methb.pdf).
/// <para>
/// Algorithm (Method B) — per-period calculation:
/// 1. Low-income exemption test (Table 1): if gross pay ≤ threshold, withhold $0.
/// 2. Estimated deduction adjustment (Table 2): subtract estimated-deduction allowance amount.
/// 3. Standard deduction subtraction (Table 3): subtract standard deduction for filing status and pay period.
/// 4. Compute tax on taxable income using per-period graduated brackets for the filing status.
/// 5. Subtract exemption allowance credit (Table 4): look up credit from table.
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

        var periodKey = PeriodKey(frequency);
        var thresholdStatusKey = GetThresholdStatusKey(filingStatus, regularAllowances);
        var rateTableStatusKey = GetRateTableStatusKey(filingStatus);

        // Step 1: Low-income exemption test (Table 1)
        decimal threshold = _data.LowIncomeExemptionThresholds[periodKey][thresholdStatusKey];
        if (grossPay <= threshold) return 0m;

        // Step 2: Estimated deduction adjustment (Table 2)
        decimal estimatedDeduction = _data.EstimatedDeductionAllowances.GetAmount(periodKey, estimatedDeductionAllowances);

        // Step 3: Standard deduction subtraction (Table 3)
        decimal standardDeduction = _data.StandardDeductions[periodKey][thresholdStatusKey];

        decimal taxableIncome = Math.Max(0m, grossPay - estimatedDeduction - standardDeduction);
        if (taxableIncome <= 0m) return 0m;

        // Step 4: Compute tax using per-period brackets directly (round cents down)
        var brackets = _data.TaxRateTables[periodKey][rateTableStatusKey];
        decimal tax = FloorToTwoDecimals(ComputeTaxFromBrackets(taxableIncome, brackets));

        // Step 5: Subtract exemption allowance credit (Table 4)
        decimal exemptionCredit = _data.ExemptionAllowanceCredits.GetAmount(periodKey, regularAllowances);

        decimal withholding = Math.Max(0m, tax - exemptionCredit);

        return Math.Round(withholding, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ComputeTaxFromBrackets(decimal taxableIncome, IReadOnlyList<CaliforniaBracket> brackets)
    {
        for (int i = brackets.Count - 1; i >= 0; i--)
        {
            var bracket = brackets[i];
            if (taxableIncome > bracket.Over)
                return bracket.Plus + bracket.Rate * (taxableIncome - bracket.AmountOver);
        }
        return 0m;
    }

    private static decimal FloorToTwoDecimals(decimal value)
        => Math.Floor(value * 100m) / 100m;

    /// <summary>
    /// Determines the threshold/deduction status key based on filing status and regular allowances.
    /// Four categories per Table 1 and Table 3 of the PDF.
    /// </summary>
    private static string GetThresholdStatusKey(CaliforniaFilingStatus filingStatus, int regularAllowances) =>
        filingStatus switch
        {
            CaliforniaFilingStatus.Single => "SingleOrDualIncomeMarriedOrMultipleEmployers",
            CaliforniaFilingStatus.HeadOfHousehold => "UnmarriedHeadOfHousehold",
            CaliforniaFilingStatus.Married when regularAllowances >= 2 => "MarriedTwoOrMoreAllowances",
            CaliforniaFilingStatus.Married => "MarriedZeroOrOneAllowance",
            _ => throw new ArgumentOutOfRangeException(nameof(filingStatus), filingStatus, "Unsupported filing status")
        };

    /// <summary>
    /// Determines the rate table status key based on filing status.
    /// Three categories per Tables 5–28 of the PDF.
    /// </summary>
    private static string GetRateTableStatusKey(CaliforniaFilingStatus filingStatus) =>
        filingStatus switch
        {
            CaliforniaFilingStatus.Single => "SingleOrDualIncomeMarriedOrMultipleEmployers",
            CaliforniaFilingStatus.Married => "Married",
            CaliforniaFilingStatus.HeadOfHousehold => "UnmarriedHeadOfHousehold",
            _ => throw new ArgumentOutOfRangeException(nameof(filingStatus), filingStatus, "Unsupported filing status")
        };

    private static string PeriodKey(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Daily => "DailyMiscellaneous",
        PayFrequency.Weekly => "Weekly",
        PayFrequency.Biweekly => "Biweekly",
        PayFrequency.Semimonthly => "Semimonthly",
        PayFrequency.Monthly => "Monthly",
        PayFrequency.Quarterly => "Quarterly",
        PayFrequency.Semiannual => "Semiannual",
        PayFrequency.Annual => "Annual",
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
