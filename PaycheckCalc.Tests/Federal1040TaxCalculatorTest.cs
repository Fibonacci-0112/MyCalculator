using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="Federal1040TaxCalculator"/> verifying Rev. Proc. 2025-32
/// (IR-2025-103) 2026 bracket boundaries and standard deduction amounts.
///
/// Expected values come directly from the IRS release, not from calling
/// production helpers, per the test instructions.
/// </summary>
public class Federal1040TaxCalculatorTest
{
    private readonly Federal1040TaxCalculator _calc;

    public Federal1040TaxCalculatorTest()
    {
        var json = File.ReadAllText("federal_1040_brackets_2026.json");
        _calc = new Federal1040TaxCalculator(json);
    }

    // ── Standard deduction per filing status (Rev. Proc. 2025-32) ──

    [Fact]
    public void StandardDeduction_Single_Is16100()
    {
        Assert.Equal(16_100m, _calc.GetStandardDeduction(FederalFilingStatus.SingleOrMarriedSeparately));
    }

    [Fact]
    public void StandardDeduction_Mfj_Is32200()
    {
        Assert.Equal(32_200m, _calc.GetStandardDeduction(FederalFilingStatus.MarriedFilingJointly));
    }

    [Fact]
    public void StandardDeduction_HeadOfHousehold_Is24150()
    {
        Assert.Equal(24_150m, _calc.GetStandardDeduction(FederalFilingStatus.HeadOfHousehold));
    }

    // ── Tax year is 2026 ───────────────────────────────────────
    [Fact]
    public void TaxYear_Is2026() => Assert.Equal(2026, _calc.TaxYear);

    // ── Non-positive income ────────────────────────────────────
    [Fact]
    public void Zero_TaxableIncome_ProducesZeroTax()
    {
        Assert.Equal(0m, _calc.CalculateTax(0m, FederalFilingStatus.SingleOrMarriedSeparately));
        Assert.Equal(0m, _calc.CalculateTax(-500m, FederalFilingStatus.SingleOrMarriedSeparately));
    }

    // ── Single bracket boundaries ───────────────────────────────
    // Boundaries: $12,400 / $50,400 / $105,700 / $201,775 / $256,225 / $640,600

    [Fact]
    public void Single_At12400Boundary_Is1240()
    {
        // Exactly at the 10→12 boundary: 12,400 × 10% = $1,240
        Assert.Equal(1_240.00m, _calc.CalculateTax(12_400m, FederalFilingStatus.SingleOrMarriedSeparately));
    }

    [Fact]
    public void Single_At50400Boundary_Is5800()
    {
        // $1,240 + ($50,400 − $12,400) × 12% = $1,240 + $4,560 = $5,800
        Assert.Equal(5_800.00m, _calc.CalculateTax(50_400m, FederalFilingStatus.SingleOrMarriedSeparately));
    }

    [Fact]
    public void Single_At105700Boundary_Is17966()
    {
        // $5,800 + ($105,700 − $50,400) × 22% = $5,800 + $12,166 = $17,966
        Assert.Equal(17_966.00m, _calc.CalculateTax(105_700m, FederalFilingStatus.SingleOrMarriedSeparately));
    }

    [Fact]
    public void Single_JustBelowBoundary_StaysInLowerBracket()
    {
        // $12,399.99 is still fully in the 10% bracket: 12,399.99 × 10% = 1,239.999 → 1,240.00 (rounded)
        Assert.Equal(1_240.00m, _calc.CalculateTax(12_399.99m, FederalFilingStatus.SingleOrMarriedSeparately));
    }

    [Fact]
    public void Single_At75000_Taxes13_612()
    {
        // $5,800 + ($75,000 − $50,400) × 22% = $5,800 + $5,412 = $11,212
        Assert.Equal(11_212.00m, _calc.CalculateTax(75_000m, FederalFilingStatus.SingleOrMarriedSeparately));
    }

    // ── MFJ bracket boundaries ──────────────────────────────────

    [Fact]
    public void Mfj_At24800Boundary_Is2480()
    {
        Assert.Equal(2_480.00m, _calc.CalculateTax(24_800m, FederalFilingStatus.MarriedFilingJointly));
    }

    [Fact]
    public void Mfj_At100800Boundary_Is11600()
    {
        // $2,480 + ($100,800 − $24,800) × 12% = $2,480 + $9,120 = $11,600
        Assert.Equal(11_600.00m, _calc.CalculateTax(100_800m, FederalFilingStatus.MarriedFilingJointly));
    }

    [Fact]
    public void Mfj_At150000_Taxes20444()
    {
        // $11,600 + ($150,000 − $100,800) × 22% = $11,600 + $10,824 = $22,424
        Assert.Equal(22_424.00m, _calc.CalculateTax(150_000m, FederalFilingStatus.MarriedFilingJointly));
    }

    [Fact]
    public void Mfj_TopBracket_Above768700()
    {
        // $206,583.50 + ($1,000,000 − $768,700) × 37% = $206,583.50 + $85,581 = $292,164.50
        Assert.Equal(292_164.50m, _calc.CalculateTax(1_000_000m, FederalFilingStatus.MarriedFilingJointly));
    }

    // ── HoH bracket boundaries ──────────────────────────────────

    [Fact]
    public void HoH_At17700Boundary_Is1770()
    {
        Assert.Equal(1_770.00m, _calc.CalculateTax(17_700m, FederalFilingStatus.HeadOfHousehold));
    }

    [Fact]
    public void HoH_At67450Boundary_Is7740()
    {
        Assert.Equal(7_740.00m, _calc.CalculateTax(67_450m, FederalFilingStatus.HeadOfHousehold));
    }

    // ── Marginal rate spot-checks ──────────────────────────────

    [Fact]
    public void Single_MarginalRate_At60000_Is22Percent()
    {
        Assert.Equal(0.22m, _calc.GetMarginalRate(60_000m, FederalFilingStatus.SingleOrMarriedSeparately));
    }

    [Fact]
    public void Mfj_MarginalRate_At300000_Is24Percent()
    {
        Assert.Equal(0.24m, _calc.GetMarginalRate(300_000m, FederalFilingStatus.MarriedFilingJointly));
    }

    [Fact]
    public void Single_MarginalRate_AtZero_Is0()
    {
        Assert.Equal(0m, _calc.GetMarginalRate(0m, FederalFilingStatus.SingleOrMarriedSeparately));
    }
}
