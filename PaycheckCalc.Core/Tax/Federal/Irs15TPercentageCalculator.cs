using System.Text.Json;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal;

/// <summary>
/// IRS Publication 15-T (2026) - Section 1 (Automated Payroll Systems), Worksheet 1A + Annual Percentage Method tables.
/// </summary>
public sealed class Irs15TPercentageCalculator
{
    private readonly Irs15TRoot _data;

    public Irs15TPercentageCalculator(string json)
    {
        json = json.Replace("None", "null");
        _data = JsonSerializer.Deserialize<Irs15TRoot>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to load IRS 15-T JSON data.");
    }

    public decimal CalculateWithholding(
        decimal taxableWagesThisPeriod,
        PayFrequency frequency,
        FederalW4Input w4)
    {
        if (taxableWagesThisPeriod <= 0m) return 0m;

        var payPeriods = PayPeriodsPerYear(frequency);

        // Worksheet 1A, Step 1 (annualize wages)
        var annualWage = taxableWagesThisPeriod * payPeriods;

        // Worksheet 1A (2020+ W-4 branch): 1e
        var line1e = annualWage + w4.Step4aOtherIncome;

        // Worksheet 1A (2020+ W-4 branch): 1d, 1f, 1g
        var line1g = w4.Step2Checked
            ? 0m
            : (w4.FilingStatus == FederalFilingStatus.MarriedFilingJointly ? _data.WorksheetConstants.Line1G.Mfj : _data.WorksheetConstants.Line1G.Other);
        
        var line1h = w4.Step4bDeductions + line1g;

        var adjustedAnnualWage = Math.Max(0m, line1e - line1h);
        if (adjustedAnnualWage <= 0m) return 0m;

        // Worksheet 1A, Step 2 (Annual % method table)
        var schedule = w4.Step2Checked ? _data.AnnualTables.Step2Checked : _data.AnnualTables.Standard;
        var brackets = GetBrackets(schedule, w4.FilingStatus);
        var b = FindBracket(brackets, adjustedAnnualWage);

        var tentativeAnnual = b.Base + (adjustedAnnualWage - b.ExcessOver) * b.Rate;
        var tentativePerPeriod = tentativeAnnual / payPeriods;

        // Worksheet 1A, Step 3 (tax credits)
        var creditsPerPeriod = w4.Step3TaxCredits / payPeriods;
        var afterCredits = Math.Max(0m, tentativePerPeriod - creditsPerPeriod);

        // Worksheet 1A, Step 4 (extra withholding per pay period)
        var final = afterCredits + w4.Step4cExtraWithholding;

        return RoundMoney(final);
    }

    private static List<AnnualBracket> GetBrackets(AnnualSchedule schedule, FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => schedule.MarriedFilingJointly,
        FederalFilingStatus.HeadOfHousehold => schedule.HeadOfHousehold,
        _ => schedule.SingleOrMfs
    };

    private static AnnualBracket FindBracket(List<AnnualBracket> brackets, decimal wages)
    {
        foreach (var b in brackets)
        {
            var underOk = b.Under is null || wages < b.Under.Value;
            if (wages >= b.Over && underOk) return b;
        }
        return brackets.Last();
    }

    // Pub 15-T rounding convention (nearest whole dollar; <50c down, >=50c up)
    private static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    
    private static decimal PayPeriodsPerYear(PayFrequency frequency) => frequency switch
    {
        PayFrequency.Weekly => 52m,
        PayFrequency.Biweekly => 26m,
        PayFrequency.Semimonthly => 24m,
        PayFrequency.Monthly => 12m,
        PayFrequency.Quarterly => 4m,
        PayFrequency.Semiannual => 2m,
        PayFrequency.Annual => 1m,
        PayFrequency.Daily => 260m,
        _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported pay frequency")
    };
}
