using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Tests for <see cref="Form8863EducationCreditsCalculator"/>. Verifies AOTC
/// per-student $2,500 cap with 100%/25% tier mechanics, 40% refundable split,
/// LLC 20%-of-up-to-$10k household cap, and MAGI phase-outs.
/// </summary>
public class Form8863EducationCreditsCalculatorTest
{
    private readonly Form8863EducationCreditsCalculator _calc = new();

    [Fact]
    public void Aotc_FullExpenses_YieldsMaxCredit()
    {
        // Student with $5,000 of expenses → AOTC = 100% × $2,000 + 25% × $2,000 = $2,500.
        // 60% nonrefundable = $1,500; 40% refundable = $1,000.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput
                {
                    QualifiedExpenses = 5_000m,
                    ClaimAmericanOpportunityCredit = true
                }
            }
        };

        var result = _calc.Calculate(
            input,
            FederalFilingStatus.SingleOrMarriedSeparately,
            adjustedGrossIncome: 50_000m);

        Assert.Equal(2_500m, result.RawAotcBeforePhaseout);
        Assert.Equal(1_500m, result.AotcNonrefundable);
        Assert.Equal(1_000m, result.AotcRefundable);
        Assert.Equal(0m, result.LifetimeLearningCredit);
    }

    [Fact]
    public void Aotc_PartialExpenses_UsesTierFormula()
    {
        // $1,500 expenses → 100% × $1,500 = $1,500.
        // 60% = $900, 40% = $600.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput
                {
                    QualifiedExpenses = 1_500m,
                    ClaimAmericanOpportunityCredit = true
                }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 50_000m);

        Assert.Equal(1_500m, result.RawAotcBeforePhaseout);
        Assert.Equal(900m, result.AotcNonrefundable);
        Assert.Equal(600m, result.AotcRefundable);
    }

    [Fact]
    public void Aotc_MultipleStudents_Stack()
    {
        // Two students each with $4,000 expenses → each yields $2,500.
        // Total $5,000 → $3,000 nonrefundable, $2,000 refundable.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 4_000m, ClaimAmericanOpportunityCredit = true },
                new EducationStudentInput { QualifiedExpenses = 4_000m, ClaimAmericanOpportunityCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 50_000m);

        Assert.Equal(5_000m, result.RawAotcBeforePhaseout);
        Assert.Equal(3_000m, result.AotcNonrefundable);
        Assert.Equal(2_000m, result.AotcRefundable);
    }

    [Fact]
    public void Llc_AppliesHouseholdCap()
    {
        // One student with $15,000 LLC expenses → LLC expenses capped at $10,000.
        // Credit = 20% × $10,000 = $2,000 (fully nonrefundable).
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 15_000m, ClaimLifetimeLearningCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 50_000m);

        Assert.Equal(2_000m, result.LifetimeLearningCredit);
        Assert.Equal(0m, result.AotcNonrefundable);
        Assert.Equal(0m, result.AotcRefundable);
    }

    [Fact]
    public void Llc_PartialExpenses()
    {
        // $6,000 expenses → 20% × $6,000 = $1,200.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 6_000m, ClaimLifetimeLearningCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 50_000m);

        Assert.Equal(1_200m, result.LifetimeLearningCredit);
    }

    [Fact]
    public void Single_MagiAboveCeiling_EliminatesCredit()
    {
        // Single: phase-out band $80k–$90k. MAGI $90k → factor = 0.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimAmericanOpportunityCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 90_000m);

        Assert.Equal(0m, result.AotcNonrefundable);
        Assert.Equal(0m, result.AotcRefundable);
    }

    [Fact]
    public void Single_MagiInPhaseoutRange_ScalesLinearly()
    {
        // Single: $80k–$90k band. MAGI $85k → factor = (90k−85k)/10k = 0.5.
        // Raw AOTC $2,500 → after phase-out = $1,250. 60%/40% split: $750/$500.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimAmericanOpportunityCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 85_000m);

        Assert.Equal(750m, result.AotcNonrefundable);
        Assert.Equal(500m, result.AotcRefundable);
    }

    [Fact]
    public void Mfj_UsesWiderPhaseoutBand()
    {
        // MFJ: $160k–$180k band. MAGI $170k → factor = 0.5.
        // $2,500 → $1,250 → $750 + $500.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimAmericanOpportunityCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 170_000m);

        Assert.Equal(750m, result.AotcNonrefundable);
        Assert.Equal(500m, result.AotcRefundable);
    }

    [Fact]
    public void Mfj_BelowPhaseoutFloor_FullCredit()
    {
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimAmericanOpportunityCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.MarriedFilingJointly, 150_000m);

        Assert.Equal(1_500m, result.AotcNonrefundable);
        Assert.Equal(1_000m, result.AotcRefundable);
    }

    [Fact]
    public void NoStudents_ProducesZero()
    {
        var result = _calc.Calculate(new EducationCreditsInput(), FederalFilingStatus.SingleOrMarriedSeparately, 50_000m);
        Assert.Equal(0m, result.AotcNonrefundable);
        Assert.Equal(0m, result.LifetimeLearningCredit);
    }

    [Fact]
    public void AotcAndLlc_CanStackAcrossDifferentStudents()
    {
        // Student A: AOTC $5,000 expenses → $2,500 credit ($1,500 NR + $1,000 R).
        // Student B: LLC $5,000 expenses → 20% × $5,000 = $1,000 LLC.
        var input = new EducationCreditsInput
        {
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimAmericanOpportunityCredit = true },
                new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimLifetimeLearningCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 50_000m);

        Assert.Equal(1_500m, result.AotcNonrefundable);
        Assert.Equal(1_000m, result.AotcRefundable);
        Assert.Equal(1_000m, result.LifetimeLearningCredit);
        Assert.Equal(2_500m, result.TotalNonrefundable); // $1,500 + $1,000
        Assert.Equal(1_000m, result.TotalRefundable);
    }

    [Fact]
    public void ModifiedAgiOverride_PrefersInputValueOverEngineAgi()
    {
        // Engine AGI in phase-out; override well below.
        var input = new EducationCreditsInput
        {
            ModifiedAgiOverride = 50_000m,
            Students = new[]
            {
                new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimAmericanOpportunityCredit = true }
            }
        };

        var result = _calc.Calculate(input, FederalFilingStatus.SingleOrMarriedSeparately, 85_000m);

        // Full credit because override MAGI $50k is below floor $80k.
        Assert.Equal(1_500m, result.AotcNonrefundable);
        Assert.Equal(1_000m, result.AotcRefundable);
    }
}
