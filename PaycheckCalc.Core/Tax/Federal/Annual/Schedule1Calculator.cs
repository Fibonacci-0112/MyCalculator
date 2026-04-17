using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Schedule 1 (Form 1040) calculator.
///
/// Part I — Additional Income: sums taxable interest, dividends, capital gains,
/// unemployment, taxable state/local refunds, taxable Social Security, and
/// any other additional income items.
///
/// Part II — Adjustments to Income: sums above-the-line adjustments (student
/// loan interest, HSA, educator expenses, SE health insurance, SE retirement,
/// traditional IRA, and other adjustments). The deductible half of SE tax is
/// NOT entered here — the Form 1040 orchestrator adds it separately from the
/// Schedule SE result so it is never double-entered.
///
/// All amounts are treated as already-validated annual dollars. Per the plan,
/// phase-outs on individual adjustments (e.g. the $2,500 student loan interest
/// cap and MAGI phase-out) will land in dedicated calculators in a later phase;
/// this class is a straightforward aggregator.
/// </summary>
public sealed class Schedule1Calculator
{
    public Schedule1Result Calculate(OtherIncomeInput income, AdjustmentsInput adjustments)
    {
        // Part I — Additional income. Treated per Form 1040 line 9:
        // Interest, ordinary dividends, and capital gains appear directly on
        // Form 1040 (not Schedule 1), but we bundle them here for a single
        // "additional income beyond W-2 and SE" figure used by the engine.
        var additionalIncome =
              income.TaxableInterest
            + income.OrdinaryDividends
            + income.CapitalGainOrLoss
            + income.UnemploymentCompensation
            + income.TaxableStateLocalRefunds
            + income.TaxableSocialSecurity
            + income.OtherAdditionalIncome;

        // Part II — Adjustments. Negative inputs are treated as 0 to avoid
        // pathological results; real validation belongs in form-specific
        // calculators (e.g. the student loan $2,500 cap is enforced by a
        // future dedicated calculator, not this aggregator).
        var totalAdjustments =
              NonNeg(adjustments.StudentLoanInterest)
            + NonNeg(adjustments.HsaDeduction)
            + NonNeg(adjustments.EducatorExpenses)
            + NonNeg(adjustments.SelfEmployedHealthInsurance)
            + NonNeg(adjustments.SelfEmployedRetirement)
            + NonNeg(adjustments.TraditionalIraDeduction)
            + NonNeg(adjustments.OtherAdjustments);

        return new Schedule1Result
        {
            AdditionalIncome = R(additionalIncome),
            AdjustmentsExcludingSeTax = R(totalAdjustments)
        };
    }

    private static decimal NonNeg(decimal v) => v < 0m ? 0m : v;
    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Output of <see cref="Schedule1Calculator"/>.</summary>
public sealed class Schedule1Result
{
    /// <summary>Part I total (additional income).</summary>
    public decimal AdditionalIncome { get; init; }

    /// <summary>
    /// Part II total EXCLUDING the deductible half of SE tax. The orchestrator
    /// adds that amount separately from the Schedule SE result.
    /// </summary>
    public decimal AdjustmentsExcludingSeTax { get; init; }
}
