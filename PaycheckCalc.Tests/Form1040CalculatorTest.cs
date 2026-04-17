using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.SelfEmployment;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// End-to-end tests for the annual <see cref="Form1040Calculator"/> engine.
/// Verifies the full pipeline: W-2 aggregation → Schedule C/SE → Schedule 1 →
/// std deduction → QBI → bracket tax → credits → Schedule 2 → payments →
/// refund/owe.
///
/// Expected dollar amounts are derived by hand from IRS Rev. Proc. 2025-32
/// 2026 brackets/standard deductions and the existing SE/QBI logic — NOT by
/// calling production helpers — per the test instructions.
/// </summary>
public class Form1040CalculatorTest
{
    private readonly Form1040Calculator _calc;

    public Form1040CalculatorTest()
    {
        var bracketsJson = File.ReadAllText("federal_1040_brackets_2026.json");
        var fed = new Federal1040TaxCalculator(bracketsJson);
        var fica = new FicaCalculator();
        var seTax = new SelfEmploymentTaxCalculator(fica);
        var qbi = new QbiDeductionCalculator();
        var sched1 = new Schedule1Calculator();

        _calc = new Form1040Calculator(fed, sched1, seTax, qbi, fica);
    }

    // ── Scenario 1: single W-2 taxpayer, no other income, single filer ──
    // Wages $80,000, $9,000 fed WH. Expected:
    //   Total income = $80,000
    //   AGI          = $80,000 (no adjustments)
    //   Taxable      = $80,000 − $16,100 = $63,900
    //   Tax          = $5,800 + ($63,900 − $50,400) × 22% = $5,800 + $2,970 = $8,770
    //   Payments     = $9,000
    //   Refund       = $9,000 − $8,770 = $230

    [Fact]
    public void Single_W2Only_ProducesSmallRefund()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    Name = "Day job",
                    WagesBox1 = 80_000m,
                    FederalWithholdingBox2 = 9_000m,
                    SocialSecurityWagesBox3 = 80_000m,
                    SocialSecurityTaxBox4 = 4_960m,
                    MedicareWagesBox5 = 80_000m,
                    MedicareTaxBox6 = 1_160m,
                    StateWithholdingBox17 = 2_500m
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(80_000m, result.TotalW2Wages);
        Assert.Equal(0m, result.ScheduleCNetProfit);
        Assert.Equal(0m, result.AdditionalIncome);
        Assert.Equal(0m, result.TotalAdjustments);
        Assert.Equal(80_000m, result.TotalIncome);
        Assert.Equal(80_000m, result.AdjustedGrossIncome);

        Assert.Equal(16_100m, result.StandardDeduction);
        Assert.Equal(0m, result.QbiDeduction);
        Assert.Equal(63_900m, result.TaxableIncome);

        Assert.Equal(8_770.00m, result.IncomeTaxBeforeCredits);
        Assert.Equal(8_770.00m, result.IncomeTaxAfterCredits);
        Assert.Equal(0m, result.SelfEmploymentTax);

        Assert.Equal(8_770.00m, result.TotalTax);
        Assert.Equal(9_000.00m, result.FederalWithholdingFromW2s);
        Assert.Equal(0m, result.ExcessSocialSecurityCredit);
        Assert.Equal(9_000.00m, result.TotalPayments);

        Assert.Equal(230.00m, result.RefundOrOwe);
        Assert.Equal(0.22m, result.MarginalTaxRate);
    }

    // ── Scenario 2: MFJ with both spouses working ─────────────────
    // Job A wages $120k/WH $12k; Job B wages $90k/WH $8k.
    // Total W-2 wages = $210,000. No SE. MFJ std deduction = $32,200.
    //   Total income = $210,000
    //   AGI          = $210,000
    //   Taxable      = $210,000 − $32,200 = $177,800
    //   Tax: MFJ 22% bracket (100,800–211,400)
    //     = $11,600 + ($177,800 − $100,800) × 22%
    //     = $11,600 + $16,940 = $28,540
    //   Payments     = $12,000 + $8,000 = $20,000
    //   Owe          = $28,540 − $20,000 = $8,540 → RefundOrOwe = −$8,540
    //
    // Note: the engine currently aggregates all W-2 jobs as one taxpayer's
    // jobs. For MFJ, excess SS credit should really be computed per spouse;
    // that refinement is deferred to the Phase-4 Holder-aware model. This
    // test keeps combined SS wages under the SS wage base so the excess-SS
    // path is not inadvertently triggered.

    [Fact]
    public void Mfj_TwoW2Jobs_OwesBalance()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    Name = "Spouse A",
                    WagesBox1 = 120_000m,
                    FederalWithholdingBox2 = 12_000m,
                    SocialSecurityWagesBox3 = 90_000m,
                    SocialSecurityTaxBox4 = 5_580m,
                    MedicareWagesBox5 = 120_000m,
                },
                new W2JobInput
                {
                    Name = "Spouse B",
                    WagesBox1 = 90_000m,
                    FederalWithholdingBox2 = 8_000m,
                    SocialSecurityWagesBox3 = 90_000m,
                    SocialSecurityTaxBox4 = 5_580m,
                    MedicareWagesBox5 = 90_000m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(210_000m, result.TotalW2Wages);
        Assert.Equal(177_800m, result.TaxableIncome);
        Assert.Equal(28_540.00m, result.IncomeTaxBeforeCredits);
        Assert.Equal(28_540.00m, result.TotalTax);
        Assert.Equal(20_000.00m, result.TotalPayments);
        Assert.Equal(-8_540.00m, result.RefundOrOwe);
        Assert.Equal(0.22m, result.MarginalTaxRate);
        // Combined SS wages $180k < $184,500 base → no excess SS credit
        Assert.Equal(0m, result.ExcessSocialSecurityCredit);
    }

    // ── Scenario 3: Excess Social Security credit from multi-W-2 ──
    // Two jobs each paying $120k SS wages. SS withheld per job = $7,440, total $14,880.
    // SS wage base 2026 = $184,500 → max SS tax = $184,500 × 6.2% = $11,439.
    // Excess SS credit = $14,880 − $11,439 = $3,441.

    [Fact]
    public void MultiW2_ExcessSocialSecurity_AppearsAsCredit()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    Name = "Employer A",
                    WagesBox1 = 120_000m,
                    FederalWithholdingBox2 = 20_000m,
                    SocialSecurityWagesBox3 = 120_000m,
                    SocialSecurityTaxBox4 = 7_440m, // 120,000 × 6.2%
                    MedicareWagesBox5 = 120_000m,
                },
                new W2JobInput
                {
                    Name = "Employer B",
                    WagesBox1 = 120_000m,
                    FederalWithholdingBox2 = 20_000m,
                    SocialSecurityWagesBox3 = 120_000m,
                    SocialSecurityTaxBox4 = 7_440m,
                    MedicareWagesBox5 = 120_000m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        // Max SS tax 2026 = $184,500 × 0.062 = $11,439
        // Excess = $14,880 − $11,439 = $3,441
        Assert.Equal(3_441.00m, result.ExcessSocialSecurityCredit);

        // Total payments = $40,000 fed WH + $3,441 excess SS = $43,441
        Assert.Equal(43_441.00m, result.TotalPayments);
    }

    [Fact]
    public void SingleW2_WithSsAboveBase_NoExcessCredit()
    {
        // A single employer over-withheld is an employer error, not an excess-SS-credit
        // situation (taxpayer must seek refund from employer). Engine only credits
        // the multi-job scenario.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 200_000m,
                    FederalWithholdingBox2 = 30_000m,
                    SocialSecurityWagesBox3 = 184_500m, // capped by employer
                    SocialSecurityTaxBox4 = 11_439m,    // max
                    MedicareWagesBox5 = 200_000m,
                }
            }
        };

        var result = _calc.Calculate(profile);
        Assert.Equal(0m, result.ExcessSocialSecurityCredit);
    }

    // ── Scenario 4: W-2 + self-employment (owe, SE tax deduction, QBI) ─
    // Single filer. W-2: $60k wages, $6k fed WH.
    // SE: $40k net profit (gross $40k, no COGS, no expenses), Texas (no state tax).
    //   SE taxable earnings = $40,000 × 0.9235 = $36,940
    //   SS tax = $36,940 × 12.4% = $4,580.56
    //   Medicare = $36,940 × 2.9% = $1,071.26
    //   Total SE tax = $5,651.82
    //   Deductible half = $2,825.91
    //   Total income = $60,000 W-2 + $40,000 SE = $100,000
    //   AGI = $100,000 − $2,825.91 = $97,174.09
    //   Taxable before QBI = $97,174.09 − $16,100 = $81,074.09
    //   QBI: 20% of $40,000 = $8,000; 20% of $81,074.09 = $16,214.82
    //        Deduction = min = $8,000
    //   Taxable income = $81,074.09 − $8,000 = $73,074.09
    //   Tax = $5,800 + ($73,074.09 − $50,400) × 22%
    //       = $5,800 + $4,988.30 = $10,788.30
    //   Total tax = $10,788.30 + $5,651.82 = $16,440.12
    //   Payments = $6,000
    //   RefundOrOwe = $6,000 − $16,440.12 = −$10,440.12

    [Fact]
    public void W2PlusSelfEmployment_OwesBalance_WithSeTaxDeductionAndQbi()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            ResidenceState = UsState.TX,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 60_000m,
                    FederalWithholdingBox2 = 6_000m,
                    SocialSecurityWagesBox3 = 60_000m,
                    SocialSecurityTaxBox4 = 3_720m,
                    MedicareWagesBox5 = 60_000m,
                }
            },
            SelfEmployment = new SelfEmploymentInput
            {
                GrossRevenue = 40_000m,
                CostOfGoodsSold = 0m,
                TotalBusinessExpenses = 0m,
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                State = UsState.TX
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(40_000m, result.ScheduleCNetProfit);
        Assert.Equal(100_000m, result.TotalIncome);

        // SE tax: $40,000 × 0.9235 = $36,940; × 15.3% = $5,651.82
        Assert.Equal(5_651.82m, result.SelfEmploymentTax);

        // Total adjustments = deductible half of SE tax only (no other adjustments)
        Assert.Equal(2_825.91m, result.TotalAdjustments);

        // AGI = $100,000 − $2,825.91 = $97,174.09
        Assert.Equal(97_174.09m, result.AdjustedGrossIncome);

        // Taxable before QBI = $97,174.09 − $16,100 = $81,074.09
        // QBI = min(20% of $40,000, 20% of $81,074.09) = $8,000
        Assert.Equal(8_000.00m, result.QbiDeduction);
        Assert.Equal(73_074.09m, result.TaxableIncome);

        // Bracket tax on $73,074.09 (Single):
        // $5,800 + ($73,074.09 − $50,400) × 22% = $5,800 + $4,988.30 = $10,788.30
        Assert.Equal(10_788.30m, result.IncomeTaxBeforeCredits);

        // Total tax = income tax + SE tax
        Assert.Equal(16_440.12m, result.TotalTax);

        Assert.Equal(6_000.00m, result.TotalPayments);
        Assert.Equal(-10_440.12m, result.RefundOrOwe);
    }

    // ── Scenario 5: credits applied ───────────────────────────────
    // Single filer, $80k wages, $9k fed WH, $2,000 nonrefundable credit.
    // Income tax before credits = $8,770 (from Scenario 1).
    // After credits = $8,770 − $2,000 = $6,770.
    // Payments = $9,000 → refund = $2,230.

    [Fact]
    public void NonrefundableCredits_ReduceTaxButNotBelowZero()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 80_000m,
                    FederalWithholdingBox2 = 9_000m,
                    SocialSecurityWagesBox3 = 80_000m,
                    MedicareWagesBox5 = 80_000m,
                }
            },
            Credits = new CreditsInput
            {
                NonrefundableCredits = 2_000m
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(8_770.00m, result.IncomeTaxBeforeCredits);
        Assert.Equal(2_000.00m, result.NonrefundableCredits);
        Assert.Equal(6_770.00m, result.IncomeTaxAfterCredits);
        Assert.Equal(6_770.00m, result.TotalTax);
        Assert.Equal(2_230.00m, result.RefundOrOwe);
    }

    [Fact]
    public void NonrefundableCredits_DoNotReduceTaxBelowZero()
    {
        // Small income → tiny tax; huge credit capped at tax
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 20_000m,
                    FederalWithholdingBox2 = 0m,
                    SocialSecurityWagesBox3 = 20_000m,
                    MedicareWagesBox5 = 20_000m,
                }
            },
            Credits = new CreditsInput
            {
                NonrefundableCredits = 50_000m // absurdly high
            }
        };

        var result = _calc.Calculate(profile);

        // Taxable = 20,000 − 16,100 = 3,900; tax = 3,900 × 10% = 390
        Assert.Equal(390.00m, result.IncomeTaxBeforeCredits);
        Assert.Equal(390.00m, result.NonrefundableCredits); // capped at the tax
        Assert.Equal(0m, result.IncomeTaxAfterCredits);
    }

    [Fact]
    public void RefundableCredits_IncreasePaymentsRegardlessOfTax()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 20_000m,
                    FederalWithholdingBox2 = 200m,
                    SocialSecurityWagesBox3 = 20_000m,
                    MedicareWagesBox5 = 20_000m,
                }
            },
            Credits = new CreditsInput
            {
                RefundableCredits = 1_500m
            }
        };

        var result = _calc.Calculate(profile);

        // Tax = $390; Payments = $200 WH + $1,500 refundable = $1,700
        // Refund = $1,700 − $390 = $1,310
        Assert.Equal(390.00m, result.TotalTax);
        Assert.Equal(1_700.00m, result.TotalPayments);
        Assert.Equal(1_310.00m, result.RefundOrOwe);
    }

    // ── Scenario 6: Adjustments + additional income ────────────────
    // Single filer, $70k wages. $1,500 taxable interest, $3,000 HSA deduction.
    //   Total income = $70,000 + $1,500 = $71,500
    //   Adjustments  = $3,000
    //   AGI          = $71,500 − $3,000 = $68,500
    //   Taxable      = $68,500 − $16,100 = $52,400
    //   Tax          = $5,800 + ($52,400 − $50,400) × 22% = $5,800 + $440 = $6,240

    [Fact]
    public void AdjustmentsAndAdditionalIncome_FlowThroughToTaxableIncome()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 70_000m,
                    FederalWithholdingBox2 = 7_000m,
                    SocialSecurityWagesBox3 = 70_000m,
                    MedicareWagesBox5 = 70_000m,
                }
            },
            OtherIncome = new OtherIncomeInput { TaxableInterest = 1_500m },
            Adjustments = new AdjustmentsInput { HsaDeduction = 3_000m }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(71_500m, result.TotalIncome);
        Assert.Equal(3_000.00m, result.TotalAdjustments);
        Assert.Equal(68_500.00m, result.AdjustedGrossIncome);
        Assert.Equal(52_400.00m, result.TaxableIncome);
        Assert.Equal(6_240.00m, result.IncomeTaxBeforeCredits);
    }

    // ── Edge cases ─────────────────────────────────────────────────

    [Fact]
    public void EmptyProfile_ProducesZeroEverything()
    {
        var result = _calc.Calculate(new TaxYearProfile());

        Assert.Equal(0m, result.TotalIncome);
        Assert.Equal(0m, result.AdjustedGrossIncome);
        Assert.Equal(0m, result.TaxableIncome);
        Assert.Equal(0m, result.TotalTax);
        Assert.Equal(0m, result.TotalPayments);
        Assert.Equal(0m, result.RefundOrOwe);
    }

    [Fact]
    public void IncomeBelowStandardDeduction_ProducesZeroTax()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 10_000m,
                    FederalWithholdingBox2 = 500m,
                    SocialSecurityWagesBox3 = 10_000m,
                    MedicareWagesBox5 = 10_000m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        // $10,000 < $16,100 std deduction → taxable income = 0
        Assert.Equal(0m, result.TaxableIncome);
        Assert.Equal(0m, result.IncomeTaxBeforeCredits);
        Assert.Equal(500.00m, result.RefundOrOwe); // full WH refunded
    }

    [Fact]
    public void EstimatedTaxPayments_CountAsPayments()
    {
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 80_000m,
                    FederalWithholdingBox2 = 5_000m,
                    SocialSecurityWagesBox3 = 80_000m,
                    MedicareWagesBox5 = 80_000m,
                }
            },
            EstimatedTaxPayments = 4_000m
        };

        var result = _calc.Calculate(profile);

        // Tax = $8,770 (Scenario 1 basis). Payments = $5,000 + $4,000 = $9,000.
        // Refund = $230.
        Assert.Equal(8_770.00m, result.TotalTax);
        Assert.Equal(9_000.00m, result.TotalPayments);
        Assert.Equal(230.00m, result.RefundOrOwe);
    }

    [Fact]
    public void SelfEmploymentOnly_NoW2()
    {
        // Pure self-employed filer — no W-2, but QBI and SE tax apply.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            ResidenceState = UsState.TX,
            SelfEmployment = new SelfEmploymentInput
            {
                GrossRevenue = 100_000m,
                TotalBusinessExpenses = 20_000m,
                FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
                State = UsState.TX
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(0m, result.TotalW2Wages);
        Assert.Equal(80_000m, result.ScheduleCNetProfit);

        // SE taxable earnings = 80,000 × 0.9235 = $73,880
        // Total SE tax = $73,880 × 15.3% = $11,303.64
        Assert.Equal(11_303.64m, result.SelfEmploymentTax);

        // Positive total tax, zero fed WH → owes balance
        Assert.True(result.TotalTax > 0m);
        Assert.Equal(0m, result.FederalWithholdingFromW2s);
        Assert.True(result.RefundOrOwe < 0m);
    }
}
