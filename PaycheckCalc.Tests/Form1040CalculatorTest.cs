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

        // Form 1040-ES: no prior-year info supplied → 90% CY safe harbor.
        // 90% × $8,770 = $7,893 required; $9,000 withholding already covers
        // it, so no quarterly estimates are required.
        Assert.NotNull(result.QuarterlyEstimates);
        Assert.Equal(SafeHarborBasis.NinetyPercentOfCurrentYear,
            result.QuarterlyEstimates!.SafeHarborBasis);
        Assert.Equal(7_893.00m, result.QuarterlyEstimates.RequiredAnnualPayment);
        Assert.Equal(0m, result.QuarterlyEstimates.TotalEstimatedPayments);
        Assert.False(result.QuarterlyEstimates.EstimatesRequired);
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

    // ── Standard-deduction edge tests (Phase 9 priority) ──────────
    //
    // Single 2026 std deduction = $16,100. One dollar below → taxable = $0,
    // tax = $0. One dollar above → taxable = $1, first-bracket tax = $0.10.

    [Fact]
    public void Single_WagesOneDollarBelowStdDeduction_ZeroTax()
    {
        // $16,099 AGI − $16,100 std ded → taxable floors at 0 → tax = $0.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 16_099m,
                    FederalWithholdingBox2 = 0m,
                    SocialSecurityWagesBox3 = 16_099m,
                    MedicareWagesBox5 = 16_099m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(16_099m, result.AdjustedGrossIncome);
        Assert.Equal(16_100m, result.StandardDeduction);
        Assert.Equal(0m, result.TaxableIncome);
        Assert.Equal(0m, result.IncomeTaxBeforeCredits);
    }

    [Fact]
    public void Single_WagesExactlyAtStdDeduction_ZeroTaxableIncome()
    {
        // $16,100 AGI − $16,100 std ded → taxable = $0 → tax = $0.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 16_100m,
                    SocialSecurityWagesBox3 = 16_100m,
                    MedicareWagesBox5 = 16_100m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(0m, result.TaxableIncome);
        Assert.Equal(0m, result.IncomeTaxBeforeCredits);
    }

    [Fact]
    public void Single_WagesOneDollarAboveStdDeduction_TaxableIsOneDollar()
    {
        // $16,101 AGI − $16,100 std ded → taxable = $1 → tax = 10% × $1 = $0.10.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 16_101m,
                    SocialSecurityWagesBox3 = 16_101m,
                    MedicareWagesBox5 = 16_101m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(1m, result.TaxableIncome);
        Assert.Equal(0.10m, result.IncomeTaxBeforeCredits);
    }

    [Fact]
    public void Mfj_WagesExactlyAtStdDeduction_ZeroTaxableIncome()
    {
        // MFJ std deduction = $32,200. $32,200 wages → taxable = $0.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 32_200m,
                    SocialSecurityWagesBox3 = 32_200m,
                    MedicareWagesBox5 = 32_200m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(32_200m, result.StandardDeduction);
        Assert.Equal(0m, result.TaxableIncome);
        Assert.Equal(0m, result.IncomeTaxBeforeCredits);
    }

    [Fact]
    public void HoH_WagesOneDollarAboveStdDeduction_TaxableIsOneDollar()
    {
        // HoH std deduction = $24,150. $24,151 wages → taxable = $1, tax = $0.10.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.HeadOfHousehold,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 24_151m,
                    SocialSecurityWagesBox3 = 24_151m,
                    MedicareWagesBox5 = 24_151m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(24_150m, result.StandardDeduction);
        Assert.Equal(1m, result.TaxableIncome);
        Assert.Equal(0.10m, result.IncomeTaxBeforeCredits);
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

    // ── Phase 4: per-spouse excess Social Security credit ──────────
    //
    // On a joint return the excess-SS test is applied per taxpayer. One spouse
    // with two jobs over the SS wage base should generate a credit even when
    // the other spouse has only one W-2 with moderate SS withholding.

    [Fact]
    public void Mfj_OneSpouseTwoJobsOverBase_OtherSpouseSingleJob_CreditsOnlyFirstSpouse()
    {
        // Spouse A: two employers, $120k SS wages each → $7,440 × 2 = $14,880 SS tax withheld.
        //   Max per-taxpayer = $184,500 × 6.2% = $11,439. Excess = $3,441.
        // Spouse B: one employer, $90k SS wages → $5,580 SS tax withheld. No credit.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    Name = "Spouse A Employer 1",
                    Holder = W2JobHolder.Taxpayer,
                    WagesBox1 = 120_000m,
                    FederalWithholdingBox2 = 18_000m,
                    SocialSecurityWagesBox3 = 120_000m,
                    SocialSecurityTaxBox4 = 7_440m,
                    MedicareWagesBox5 = 120_000m,
                },
                new W2JobInput
                {
                    Name = "Spouse A Employer 2",
                    Holder = W2JobHolder.Taxpayer,
                    WagesBox1 = 120_000m,
                    FederalWithholdingBox2 = 18_000m,
                    SocialSecurityWagesBox3 = 120_000m,
                    SocialSecurityTaxBox4 = 7_440m,
                    MedicareWagesBox5 = 120_000m,
                },
                new W2JobInput
                {
                    Name = "Spouse B Only Employer",
                    Holder = W2JobHolder.Spouse,
                    WagesBox1 = 90_000m,
                    FederalWithholdingBox2 = 12_000m,
                    SocialSecurityWagesBox3 = 90_000m,
                    SocialSecurityTaxBox4 = 5_580m,
                    MedicareWagesBox5 = 90_000m,
                }
            }
        };

        var result = _calc.Calculate(profile);

        // Only Spouse A's excess ($3,441) counts.
        Assert.Equal(3_441m, result.ExcessSocialSecurityCredit);
    }

    [Fact]
    public void Mfj_EachSpouseTwoJobsOverBase_CreditsBothSpouses()
    {
        // Each spouse has two jobs of $120k each SS wages.
        //   Per-spouse excess = $14,880 − $11,439 = $3,441.
        //   Total credit = $6,882.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            W2Jobs = new[]
            {
                new W2JobInput { Holder = W2JobHolder.Taxpayer, WagesBox1 = 120_000m, SocialSecurityWagesBox3 = 120_000m, SocialSecurityTaxBox4 = 7_440m, MedicareWagesBox5 = 120_000m },
                new W2JobInput { Holder = W2JobHolder.Taxpayer, WagesBox1 = 120_000m, SocialSecurityWagesBox3 = 120_000m, SocialSecurityTaxBox4 = 7_440m, MedicareWagesBox5 = 120_000m },
                new W2JobInput { Holder = W2JobHolder.Spouse,   WagesBox1 = 120_000m, SocialSecurityWagesBox3 = 120_000m, SocialSecurityTaxBox4 = 7_440m, MedicareWagesBox5 = 120_000m },
                new W2JobInput { Holder = W2JobHolder.Spouse,   WagesBox1 = 120_000m, SocialSecurityWagesBox3 = 120_000m, SocialSecurityTaxBox4 = 7_440m, MedicareWagesBox5 = 120_000m },
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(6_882m, result.ExcessSocialSecurityCredit);
    }

    [Fact]
    public void Mfj_CombinedHouseholdOverBase_ButEachSpouseUnder_NoCredit()
    {
        // Both spouses each have one job at $120k → combined $240k SS wages,
        // but neither spouse individually has two employers. No credit.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            W2Jobs = new[]
            {
                new W2JobInput { Holder = W2JobHolder.Taxpayer, WagesBox1 = 120_000m, SocialSecurityWagesBox3 = 120_000m, SocialSecurityTaxBox4 = 7_440m, MedicareWagesBox5 = 120_000m },
                new W2JobInput { Holder = W2JobHolder.Spouse,   WagesBox1 = 120_000m, SocialSecurityWagesBox3 = 120_000m, SocialSecurityTaxBox4 = 7_440m, MedicareWagesBox5 = 120_000m },
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(0m, result.ExcessSocialSecurityCredit);
    }

    // ── Phase 3: end-to-end credit computations wired through the orchestrator ──

    [Fact]
    public void ChildTaxCreditInput_FlowsThroughToReportedCtc()
    {
        // Single, $80k wages → tax before credits = $8,770 (from Scenario 1).
        // 2 qualifying children × $2,200 = $4,400 nonrefundable CTC.
        // After credits: $8,770 − $4,400 = $4,370.
        // Payments = $9,000 → refund = $4,630.
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
                ChildTaxCreditInput = new ChildTaxCreditInput
                {
                    QualifyingChildren = 2,
                    EarnedIncome = 80_000m
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(8_770m, result.IncomeTaxBeforeCredits);
        Assert.Equal(4_400m, result.ChildTaxCredit);
        Assert.Equal(4_400m, result.NonrefundableCredits);
        Assert.Equal(4_370m, result.IncomeTaxAfterCredits);
        Assert.Equal(4_630m, result.RefundOrOwe);
        // No refundable ACTC because tax fully absorbed nonrefundable CTC.
        Assert.Equal(0m, result.RefundableAdditionalChildTaxCredit);
    }

    [Fact]
    public void LowIncomeFamily_Actc_AppearsAsRefundablePayment()
    {
        // $20,000 wages → tax before std ded = ($20k − $16,100) × 10% = $390.
        // 2 QC → $4,400 nonrefundable CTC (capped at $390 → applied = $390).
        // Unused CTC = $4,400 − $390 = $4,010.
        // ACTC = min(15% × ($20k − $2,500), 2 × $1,700, $4,010)
        //      = min($2,625, $3,400, $4,010) = $2,625.
        // Payments = $0 WH + $2,625 ACTC = $2,625. Tax after credits = $0.
        // Refund = $2,625.
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
                ChildTaxCreditInput = new ChildTaxCreditInput
                {
                    QualifyingChildren = 2,
                    EarnedIncome = 20_000m
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(390m, result.IncomeTaxBeforeCredits);
        Assert.Equal(390m, result.NonrefundableCredits);
        Assert.Equal(0m, result.IncomeTaxAfterCredits);
        Assert.Equal(2_625m, result.RefundableAdditionalChildTaxCredit);
        Assert.Equal(2_625m, result.RefundableCredits);
        Assert.Equal(2_625m, result.TotalPayments);
        Assert.Equal(2_625m, result.RefundOrOwe);
    }

    [Fact]
    public void EducationCredits_FlowThroughToNonrefundableAndRefundable()
    {
        // Single, $80k wages → tax $8,770.
        // One AOTC student with $5,000 expenses → $2,500 credit = $1,500 NR + $1,000 R.
        // Nonrefundable applied = $1,500; tax after = $7,270. Total tax = $7,270.
        // Payments = $9,000 WH + $1,000 refundable AOTC = $10,000.
        // Refund = $10,000 − $7,270 = $2,730.
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
                EducationCredits = new EducationCreditsInput
                {
                    Students = new[]
                    {
                        new EducationStudentInput
                        {
                            QualifiedExpenses = 5_000m,
                            ClaimAmericanOpportunityCredit = true
                        }
                    }
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(1_500m, result.EducationCreditsNonrefundable);
        Assert.Equal(1_000m, result.RefundableEducationCredit);
        Assert.Equal(1_500m, result.NonrefundableCredits);
        Assert.Equal(7_270m, result.IncomeTaxAfterCredits);
        Assert.Equal(7_270m, result.TotalTax);
        Assert.Equal(10_000m, result.TotalPayments);
        Assert.Equal(2_730m, result.RefundOrOwe);
    }

    [Fact]
    public void SaversCredit_FlowsThroughAsNonrefundable()
    {
        // MFJ $45k wages → taxable = $45k − $32,200 = $12,800. Tax = $12,800 × 10% = $1,280.
        // SaversCredit: $2,000 taxpayer + $2,000 spouse = $4,000 eligible, AGI $45k → 50% band.
        // Credit = 50% × $4,000 = $2,000 (exceeds tax, capped at $1,280).
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 45_000m,
                    FederalWithholdingBox2 = 500m,
                    SocialSecurityWagesBox3 = 45_000m,
                    MedicareWagesBox5 = 45_000m,
                }
            },
            Credits = new CreditsInput
            {
                SaversCredit = new SaversCreditInput
                {
                    TaxpayerContributions = 2_000m,
                    SpouseContributions = 2_000m
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(12_800m, result.TaxableIncome);
        Assert.Equal(1_280m, result.IncomeTaxBeforeCredits);
        Assert.Equal(2_000m, result.SaversCredit); // raw calculator output
        Assert.Equal(1_280m, result.NonrefundableCredits); // capped at tax
        Assert.Equal(0m, result.IncomeTaxAfterCredits);
    }

    [Fact]
    public void Niit_StructuredInputAddsToTotalTax()
    {
        // Single, $250k wages + $20k dividends → AGI $270k.
        // Taxable = $270k − $16,100 std ded = $253,900.
        // Bracket for $253,900 single: $201,775–$256,225 at 32%
        //   = $41,024 + ($253,900 − $201,775) × 32%
        //   = $41,024 + $16,680 = $57,704.
        // NIIT: excess MAGI over $200k threshold = $70k; min(NII $20k, $70k) = $20k.
        //   NIIT = 3.8% × $20,000 = $760.
        // Total tax = $57,704 + $760 = $58,464.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 250_000m,
                    FederalWithholdingBox2 = 50_000m,
                    SocialSecurityWagesBox3 = 184_500m,
                    MedicareWagesBox5 = 250_000m,
                }
            },
            OtherIncome = new OtherIncomeInput
            {
                OrdinaryDividends = 20_000m
            },
            OtherTaxes = new OtherTaxesInput
            {
                NetInvestmentIncome = new NetInvestmentIncomeInput
                {
                    NetInvestmentIncome = 20_000m
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(270_000m, result.AdjustedGrossIncome);
        Assert.Equal(57_704m, result.IncomeTaxBeforeCredits);
        Assert.Equal(760m, result.NetInvestmentIncomeTax);
        Assert.Equal(58_464m, result.TotalTax);
    }

    [Fact]
    public void Niit_StructuredAndLegacyInputs_AreAdditive()
    {
        // Legacy pre-computed NIIT $500 + structured $380 = $880 total NIIT.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 210_000m,
                    FederalWithholdingBox2 = 20_000m,
                    SocialSecurityWagesBox3 = 184_500m,
                    MedicareWagesBox5 = 210_000m,
                }
            },
            OtherTaxes = new OtherTaxesInput
            {
                NetInvestmentIncomeTax = 500m, // legacy pre-computed
                NetInvestmentIncome = new NetInvestmentIncomeInput
                {
                    NetInvestmentIncome = 10_000m // AGI $210k → excess $10k → min($10k, $10k) = $10k → $380 NIIT
                }
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(880m, result.NetInvestmentIncomeTax);
    }

    [Fact]
    public void LegacyChildTaxCredit_StillSupported()
    {
        // Back-compat: callers setting Credits.ChildTaxCredit directly continue to work.
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
                ChildTaxCredit = 2_000m // legacy lump-sum path
            }
        };

        var result = _calc.Calculate(profile);

        Assert.Equal(2_000m, result.ChildTaxCredit);
        Assert.Equal(2_000m, result.NonrefundableCredits);
        Assert.Equal(6_770m, result.IncomeTaxAfterCredits);
    }

    [Fact]
    public void AllCreditsCombined_Stack_CappedAtIncomeTax()
    {
        // Stress test: all four structured credits + legacy nonrefundable all
        // supplied together. Total requested > tax → cap at tax.
        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 45_000m,
                    FederalWithholdingBox2 = 500m,
                    SocialSecurityWagesBox3 = 45_000m,
                    MedicareWagesBox5 = 45_000m,
                }
            },
            Credits = new CreditsInput
            {
                NonrefundableCredits = 5_000m, // legacy
                ChildTaxCreditInput = new ChildTaxCreditInput { QualifyingChildren = 2, EarnedIncome = 45_000m },
                EducationCredits = new EducationCreditsInput
                {
                    Students = new[]
                    {
                        new EducationStudentInput { QualifiedExpenses = 5_000m, ClaimAmericanOpportunityCredit = true }
                    }
                },
                SaversCredit = new SaversCreditInput { TaxpayerContributions = 2_000m }
            }
        };

        var result = _calc.Calculate(profile);

        // Taxable = $45k − $32,200 = $12,800. Tax = $1,280.
        Assert.Equal(1_280m, result.IncomeTaxBeforeCredits);
        Assert.Equal(1_280m, result.NonrefundableCredits); // capped
        Assert.Equal(0m, result.IncomeTaxAfterCredits);
    }
}
