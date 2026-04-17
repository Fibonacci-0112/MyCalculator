using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="Schedule1Calculator"/>. These verify the aggregation
/// math only — individual form phase-outs (e.g. student loan interest MAGI
/// phase-out) live in dedicated calculators in a future phase.
/// </summary>
public class Schedule1CalculatorTest
{
    private readonly Schedule1Calculator _calc = new();

    [Fact]
    public void Empty_Input_ProducesZeroResults()
    {
        var result = _calc.Calculate(new OtherIncomeInput(), new AdjustmentsInput());

        Assert.Equal(0m, result.AdditionalIncome);
        Assert.Equal(0m, result.AdjustmentsExcludingSeTax);
    }

    [Fact]
    public void AdditionalIncome_SumsAllFields()
    {
        // Straight-sum expected: 500 + 1,200 + 3,000 + 4,000 + 250 + 7,500 + 100 = 16,550
        var income = new OtherIncomeInput
        {
            TaxableInterest = 500m,
            OrdinaryDividends = 1_200m,
            // QualifiedDividends is a breakdown of OrdinaryDividends and must NOT be double-counted
            QualifiedDividends = 800m,
            CapitalGainOrLoss = 3_000m,
            UnemploymentCompensation = 4_000m,
            TaxableStateLocalRefunds = 250m,
            TaxableSocialSecurity = 7_500m,
            OtherAdditionalIncome = 100m
        };

        var result = _calc.Calculate(income, new AdjustmentsInput());

        Assert.Equal(16_550.00m, result.AdditionalIncome);
    }

    [Fact]
    public void CapitalLoss_ReducesAdditionalIncome()
    {
        // Negative capital loss allowed (engine relies on taxpayer input caps)
        var income = new OtherIncomeInput
        {
            TaxableInterest = 1_000m,
            CapitalGainOrLoss = -3_000m
        };

        var result = _calc.Calculate(income, new AdjustmentsInput());

        Assert.Equal(-2_000.00m, result.AdditionalIncome);
    }

    [Fact]
    public void Adjustments_SumAllPositiveFields()
    {
        // Sum: 2,500 + 4,300 + 300 + 6,000 + 7,000 + 2,000 + 150 = 22,250
        var adj = new AdjustmentsInput
        {
            StudentLoanInterest = 2_500m,
            HsaDeduction = 4_300m,
            EducatorExpenses = 300m,
            SelfEmployedHealthInsurance = 6_000m,
            SelfEmployedRetirement = 7_000m,
            TraditionalIraDeduction = 2_000m,
            OtherAdjustments = 150m
        };

        var result = _calc.Calculate(new OtherIncomeInput(), adj);

        Assert.Equal(22_250.00m, result.AdjustmentsExcludingSeTax);
    }

    [Fact]
    public void Adjustments_NegativeInputs_AreTreatedAsZero()
    {
        // Aggregator guards against pathological negative adjustments; validation
        // of allowed ranges belongs to dedicated form calculators.
        var adj = new AdjustmentsInput
        {
            StudentLoanInterest = -500m, // clamped to 0
            HsaDeduction = 1_000m
        };

        var result = _calc.Calculate(new OtherIncomeInput(), adj);

        Assert.Equal(1_000.00m, result.AdjustmentsExcludingSeTax);
    }
}
