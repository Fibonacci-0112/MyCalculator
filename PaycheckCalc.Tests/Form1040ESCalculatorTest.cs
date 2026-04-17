using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Regression tests for <see cref="Form1040ESCalculator"/>.
///
/// Expected dollar amounts are worked out directly from the IRS 1040-ES
/// worksheet safe-harbor rules (90% of CY, 100% PY, or 110% PY when
/// prior-year AGI &gt; $150,000) — NOT by recomputing with production
/// helpers, per the repository test instructions.
/// </summary>
public class Form1040ESCalculatorTest
{
    private readonly Form1040ESCalculator _calc = new();

    // ── Scenario 1: no prior-year info — defaults to 90% of current year
    // $20,000 total tax, $14,000 expected withholding.
    // Required annual payment = 90% × $20,000 = $18,000.
    // Net of withholding = $18,000 − $14,000 = $4,000.
    // Per quarter = $4,000 / 4 = $1,000.00.

    [Fact]
    public void NoPriorYear_Uses90PercentCurrentYear()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 20_000m,
            expectedWithholding: 14_000m,
            priorYear: null);

        Assert.Equal(SafeHarborBasis.NinetyPercentOfCurrentYear, result.SafeHarborBasis);
        Assert.Equal(18_000m, result.RequiredAnnualPayment);
        Assert.Equal(4_000m, result.TotalEstimatedPayments);
        Assert.True(result.EstimatesRequired);

        Assert.Equal(4, result.Installments.Count);
        Assert.All(result.Installments, i => Assert.Equal(1_000m, i.Amount));
        Assert.Equal(new DateOnly(2026, 4, 15), result.Installments[0].DueDate);
        Assert.Equal(new DateOnly(2026, 6, 15), result.Installments[1].DueDate);
        Assert.Equal(new DateOnly(2026, 9, 15), result.Installments[2].DueDate);
        Assert.Equal(new DateOnly(2027, 1, 15), result.Installments[3].DueDate);
    }

    // ── Scenario 2: prior-year safe harbor beats 90% CY (non-high-income)
    // Current-year projected tax $30,000 → 90% = $27,000.
    // Prior-year tax $20,000, prior AGI $100,000 (< $150k) → 100% = $20,000.
    // Required = min($27,000, $20,000) = $20,000 (prior-year safe harbor).
    // Withholding $5,000 → estimate $15,000 / 4 = $3,750 per quarter.

    [Fact]
    public void PriorYearSafeHarbor_100Percent_Wins()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 30_000m,
            expectedWithholding: 5_000m,
            priorYear: new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = 20_000m,
                PriorYearAdjustedGrossIncome = 100_000m
            });

        Assert.Equal(SafeHarborBasis.OneHundredPercentOfPriorYear, result.SafeHarborBasis);
        Assert.Equal(20_000m, result.RequiredAnnualPayment);
        Assert.Equal(15_000m, result.TotalEstimatedPayments);
        Assert.All(result.Installments, i => Assert.Equal(3_750m, i.Amount));
        Assert.Equal(15_000m, result.Installments[^1].CumulativeAmount);
    }

    // ── Scenario 3: high-income 110% prior-year safe harbor
    // Prior-year AGI $200,000 → 110% rule applies.
    // Prior-year tax $40,000 → 110% = $44,000.
    // Current-year projected tax $60,000 → 90% = $54,000.
    // Required = min($54,000, $44,000) = $44,000 (110% PY wins).

    [Fact]
    public void HighIncome_UsesOneHundredTenPercentPriorYear()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.MarriedFilingJointly,
            currentYearProjectedTax: 60_000m,
            expectedWithholding: 0m,
            priorYear: new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = 40_000m,
                PriorYearAdjustedGrossIncome = 200_000m
            });

        Assert.Equal(SafeHarborBasis.OneHundredTenPercentOfPriorYear, result.SafeHarborBasis);
        Assert.Equal(44_000m, result.RequiredAnnualPayment);
        Assert.Equal(44_000m, result.TotalEstimatedPayments);
        Assert.All(result.Installments, i => Assert.Equal(11_000m, i.Amount));
    }

    // ── Scenario 4: high-income CY safe harbor still wins when taxpayer
    // projects a much smaller current-year tax. 90% × $10,000 = $9,000
    // beats 110% × $40,000 = $44,000, so the CY rule applies.

    [Fact]
    public void CurrentYear90Percent_StillWinsWhenLower()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 10_000m,
            expectedWithholding: 0m,
            priorYear: new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = 40_000m,
                PriorYearAdjustedGrossIncome = 200_000m
            });

        Assert.Equal(SafeHarborBasis.NinetyPercentOfCurrentYear, result.SafeHarborBasis);
        Assert.Equal(9_000m, result.RequiredAnnualPayment);
        Assert.Equal(2_250m, result.Installments[0].Amount);
    }

    // ── Scenario 5: withholding already covers the safe harbor — no
    // installments required. 90% × $20,000 = $18,000, withholding $20,000.
    // TotalEstimatedPayments = max(0, $18,000 − $20,000) = $0.

    [Fact]
    public void WithholdingCoversSafeHarbor_NoEstimatesRequired()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 20_000m,
            expectedWithholding: 20_000m,
            priorYear: null);

        Assert.Equal(18_000m, result.RequiredAnnualPayment);
        Assert.Equal(0m, result.TotalEstimatedPayments);
        Assert.False(result.EstimatesRequired);
        Assert.Equal(4, result.Installments.Count);
        Assert.All(result.Installments, i => Assert.Equal(0m, i.Amount));
    }

    // ── Scenario 6: Q4 absorbs the rounding remainder.
    // Required = $1,000, withholding = $0, total = $1,000.
    // $1,000 / 4 = $250 exactly — clean split, no remainder.
    //
    // But $999.97 / 4 = $249.9925 → rounded $249.99 × 3 = $749.97;
    // Q4 = $999.97 − $749.97 = $250.00, preserving the exact total.

    [Fact]
    public void Q4AbsorbsRoundingRemainder()
    {
        // Use CY-tax path with an awkward number: 90% × $1,111.08 = $999.972
        // → rounded $999.97 → per-quarter $249.9925 → $249.99 × 3 + $250.00
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 1_111.08m,
            expectedWithholding: 0m,
            priorYear: null);

        Assert.Equal(999.97m, result.RequiredAnnualPayment);
        Assert.Equal(999.97m, result.TotalEstimatedPayments);

        Assert.Equal(249.99m, result.Installments[0].Amount);
        Assert.Equal(249.99m, result.Installments[1].Amount);
        Assert.Equal(249.99m, result.Installments[2].Amount);
        Assert.Equal(250.00m, result.Installments[3].Amount);

        // Sum of installments matches the total estimate exactly.
        var sum = result.Installments.Sum(i => i.Amount);
        Assert.Equal(999.97m, sum);
        Assert.Equal(999.97m, result.Installments[^1].CumulativeAmount);
    }

    // ── Scenario 7: short prior-year return disables the PY safe harbor.
    // Prior-year tax = $1,000 (PriorYearWasFullYear = false) — a
    // short tax year disqualifies the PY rule, so the engine must fall back
    // to 90% × $40,000 = $36,000, not the tiny $1,000.

    [Fact]
    public void ShortPriorYear_DisablesPriorYearSafeHarbor()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 40_000m,
            expectedWithholding: 0m,
            priorYear: new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = 1_000m,
                PriorYearAdjustedGrossIncome = 20_000m,
                PriorYearWasFullYear = false
            });

        Assert.Equal(SafeHarborBasis.NinetyPercentOfCurrentYear, result.SafeHarborBasis);
        Assert.Equal(36_000m, result.RequiredAnnualPayment);
    }

    // ── Scenario 8: zero CY tax produces zero installments.
    [Fact]
    public void ZeroCurrentYearTax_ProducesZeroInstallments()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 0m,
            expectedWithholding: 0m);

        Assert.Equal(0m, result.RequiredAnnualPayment);
        Assert.Equal(0m, result.TotalEstimatedPayments);
        Assert.False(result.EstimatesRequired);
        Assert.Equal(4, result.Installments.Count);
        Assert.All(result.Installments, i => Assert.Equal(0m, i.Amount));
    }

    // ── Scenario 9: PY threshold is strictly greater-than.
    // AGI exactly $150,000 must use the 100% rule, not 110%.
    [Fact]
    public void PriorYearAgi_ExactlyAtThreshold_Uses100Percent()
    {
        var result = _calc.Calculate(
            taxYear: 2026,
            filingStatus: FederalFilingStatus.SingleOrMarriedSeparately,
            currentYearProjectedTax: 100_000m,
            expectedWithholding: 0m,
            priorYear: new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = 50_000m,
                PriorYearAdjustedGrossIncome = 150_000m // exactly at threshold
            });

        Assert.Equal(SafeHarborBasis.OneHundredPercentOfPriorYear, result.SafeHarborBasis);
        Assert.Equal(50_000m, result.RequiredAnnualPayment);
    }
}
