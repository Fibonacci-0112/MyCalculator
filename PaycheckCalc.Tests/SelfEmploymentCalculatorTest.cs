using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.SelfEmployment;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// End-to-end tests for <see cref="SelfEmploymentCalculator"/> verifying the
/// full pipeline: Schedule C → SE Tax → AGI → QBI → Federal → State → Summary.
/// </summary>
public class SelfEmploymentCalculatorTest
{
    private readonly SelfEmploymentCalculator _calc;

    public SelfEmploymentCalculatorTest()
    {
        var fica = new FicaCalculator();
        var seTax = new SelfEmploymentTaxCalculator(fica);
        var qbi = new QbiDeductionCalculator();

        var json = File.ReadAllText("us_irs_15t_2026_percentage_automated.json");
        var fed = new Irs15TPercentageCalculator(json);

        // Register a no-income-tax state (TX) for testing
        var registry = new StateCalculatorRegistry();
        registry.Register(new NoIncomeTaxWithholdingAdapter(UsState.TX));

        // Register Oklahoma for state tax testing
        var okJson = File.ReadAllText("ok_ow2_2026_percentage.json");
        var okCalc = new OklahomaOw2PercentageCalculator(okJson);
        registry.Register(new OklahomaWithholdingCalculator(okCalc));

        _calc = new SelfEmploymentCalculator(seTax, qbi, fed, registry);
    }

    [Fact]
    public void ModerateFreelancerInTexas_NoStateTax()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 120_000m,
            CostOfGoodsSold = 0m,
            TotalBusinessExpenses = 20_000m,
            OtherIncome = 0m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        // Schedule C: net profit = $120K − $20K = $100K
        Assert.Equal(100_000m, result.NetProfit);

        // SE tax: $100K × 0.9235 = $92,350 taxable
        Assert.Equal(92_350.00m, result.SeTaxableEarnings);

        // SS = $92,350 × 0.124 = $11,451.40
        Assert.Equal(11_451.40m, result.SocialSecurityTax);

        // Medicare = $92,350 × 0.029 = $2,678.15
        Assert.Equal(2_678.15m, result.MedicareTax);

        // No Additional Medicare (below $200K)
        Assert.Equal(0m, result.AdditionalMedicareTax);

        // Total SE tax = $14,129.55; deductible half = $7,064.78
        Assert.Equal(14_129.55m, result.TotalSeTax);
        Assert.Equal(7_064.78m, result.DeductibleHalfOfSeTax);

        // AGI = $0 + $100,000 − $7,064.78 = $92,935.22
        Assert.Equal(92_935.22m, result.AdjustedGrossIncome);

        // Standard deduction (Single 2026) = $15,700
        Assert.Equal(15_700m, result.StandardDeduction);

        // Taxable before QBI = $92,935.22 − $15,700 = $77,235.22
        // QBI = 20% of $100K = $20,000; 20% of taxable before QBI = $15,447.04
        // QBI deduction = min($20,000, $15,447.04) = $15,447.04
        Assert.Equal(15_447.04m, result.QbiDeduction);

        // Taxable income = $77,235.22 − $15,447.04 = $61,788.18
        Assert.Equal(61_788.18m, result.TaxableIncome);

        // Federal income tax computed via IRS 15-T annual tables (approximate)
        Assert.True(result.FederalIncomeTax > 0m, "Federal tax should be positive");

        // Texas: no state income tax
        Assert.Equal(0m, result.StateIncomeTax);

        // Summary
        Assert.Equal(result.FederalIncomeTax + result.TotalSeTax, result.TotalFederalTax);
        Assert.Equal(result.TotalFederalTax + result.TotalStateTax, result.TotalTax);
        Assert.True(result.EffectiveTaxRate > 0m && result.EffectiveTaxRate < 100m);
        Assert.Equal(Math.Round(result.TotalTax / 4m, 2, MidpointRounding.AwayFromZero), result.EstimatedQuarterlyPayment);
    }

    [Fact]
    public void ZeroRevenue_ReturnsZeroTax()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 0m,
            TotalBusinessExpenses = 0m,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        Assert.Equal(0m, result.NetProfit);
        Assert.Equal(0m, result.TotalSeTax);
        Assert.Equal(0m, result.FederalIncomeTax);
        Assert.Equal(0m, result.TotalTax);
        Assert.Equal(0m, result.EffectiveTaxRate);
    }

    [Fact]
    public void BusinessLoss_NoSeTaxButOtherIncomeTaxed()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 30_000m,
            TotalBusinessExpenses = 50_000m, // net loss of $20K
            OtherIncome = 80_000m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        Assert.Equal(-20_000m, result.NetProfit);
        Assert.Equal(0m, result.TotalSeTax); // No SE tax on a loss

        // AGI = $80,000 + max(0, -$20,000) − $0 = $80,000
        Assert.Equal(80_000m, result.AdjustedGrossIncome);

        // Federal tax should be based on $80K − standard deduction − QBI
        Assert.True(result.FederalIncomeTax > 0m);
    }

    [Fact]
    public void EstimatedPayments_OverUnderCalculation()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 80_000m,
            TotalBusinessExpenses = 10_000m,
            State = UsState.TX,
            EstimatedTaxPayments = 25_000m // Overpaying
        };

        var result = _calc.Calculate(input);

        // OverUnder = payments − total tax
        Assert.Equal(25_000m - result.TotalTax, result.OverUnderPayment);
        Assert.True(result.OverUnderPayment > 0m, "Should be overpaying (refund expected)");
    }

    [Fact]
    public void MFJ_UsesCorrectStandardDeduction()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 200_000m,
            TotalBusinessExpenses = 20_000m,
            FilingStatus = FederalFilingStatus.MarriedFilingJointly,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        // MFJ standard deduction = $31,400
        Assert.Equal(31_400m, result.StandardDeduction);
    }

    [Fact]
    public void HeadOfHousehold_UsesCorrectStandardDeduction()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 150_000m,
            TotalBusinessExpenses = 10_000m,
            FilingStatus = FederalFilingStatus.HeadOfHousehold,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        // HoH standard deduction = $23,550
        Assert.Equal(23_550m, result.StandardDeduction);
    }

    [Fact]
    public void ItemizedDeductionOverStandard_IncreasesDeduction()
    {
        var baseInput = new SelfEmploymentInput
        {
            GrossRevenue = 150_000m,
            TotalBusinessExpenses = 10_000m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX,
            ItemizedDeductionsOverStandard = 0m
        };

        var itemizedInput = new SelfEmploymentInput
        {
            GrossRevenue = 150_000m,
            TotalBusinessExpenses = 10_000m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX,
            ItemizedDeductionsOverStandard = 5_000m
        };

        var baseResult = _calc.Calculate(baseInput);
        var itemizedResult = _calc.Calculate(itemizedInput);

        // Itemized deduction adds $5K to the standard deduction
        Assert.Equal(baseResult.StandardDeduction + 5_000m, itemizedResult.StandardDeduction);

        // Taxable income should be lower with itemized deductions
        Assert.True(itemizedResult.TaxableIncome < baseResult.TaxableIncome);
    }

    [Fact]
    public void WithStateTax_OklahomaComputesStateTax()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 120_000m,
            TotalBusinessExpenses = 20_000m,
            OtherIncome = 0m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.OK,
            StateInputValues = new StateInputValues
            {
                ["FilingStatus"] = "Single",
                ["Allowances"] = 1,
                ["AdditionalWithholding"] = 0m
            }
        };

        var result = _calc.Calculate(input);

        // Oklahoma should produce non-zero state tax on $100K net profit
        Assert.True(result.StateIncomeTax > 0m, "Oklahoma state tax should be positive");
        Assert.Equal(result.StateIncomeTax, result.TotalStateTax);
        Assert.Equal(result.TotalFederalTax + result.TotalStateTax, result.TotalTax);
    }

    [Fact]
    public void HighIncome_QbiPhaseOutApplies()
    {
        // Single filer with $350K net profit — above QBI threshold
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 400_000m,
            TotalBusinessExpenses = 50_000m,
            OtherIncome = 0m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX,
            IsSpecifiedServiceBusiness = true // SSTB → QBI phases out
        };

        var result = _calc.Calculate(input);

        Assert.Equal(350_000m, result.NetProfit);

        // At high income, SSTB QBI deduction should be reduced or zero
        // (exact amount depends on taxable income after deductions)
        Assert.True(result.QbiDeduction < 350_000m * 0.20m,
            "SSTB QBI deduction should be less than the full 20%");
    }

    [Fact]
    public void EffectiveTaxRate_ComputedCorrectly()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 100_000m,
            TotalBusinessExpenses = 0m,
            OtherIncome = 50_000m,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        // Effective rate = TotalTax / (GrossRevenue + OtherIncome) × 100
        var expectedRate = Math.Round(result.TotalTax / 150_000m * 100m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedRate, result.EffectiveTaxRate);
    }

    [Fact]
    public void QuarterlyPayment_IsTotalDividedByFour()
    {
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 100_000m,
            TotalBusinessExpenses = 10_000m,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        var expectedQuarterly = Math.Round(result.TotalTax / 4m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedQuarterly, result.EstimatedQuarterlyPayment);
    }

    [Fact]
    public void W2PlusSelfEmployed_SsWageBaseCoordinated()
    {
        // Scenario: freelancer with $100K SE net profit who also has a W-2 job
        // paying $150K (W-2 SS wages = $150K, W-2 Medicare wages = $150K).
        // The SS wage base ($184,500) is shared — only $34,500 of SE taxable
        // earnings should be subject to SE Social Security tax.
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 120_000m,
            TotalBusinessExpenses = 20_000m,
            OtherIncome = 150_000m, // W-2 wages for income tax bracket
            W2SocialSecurityWages = 150_000m, // W-2 Box 3
            W2MedicareWages = 150_000m, // W-2 Box 5
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        // Net profit = $120K − $20K = $100K
        Assert.Equal(100_000m, result.NetProfit);

        // SE taxable = $100K × 0.9235 = $92,350
        Assert.Equal(92_350.00m, result.SeTaxableEarnings);

        // Remaining SS base = $184,500 − $150,000 = $34,500
        // SS tax = $34,500 × 0.124 = $4,278.00 (reduced from $11,451.40 without W-2)
        Assert.Equal(4_278.00m, result.SocialSecurityTax);

        // Medicare is not capped: $92,350 × 0.029 = $2,678.15
        Assert.Equal(2_678.15m, result.MedicareTax);

        // Additional Medicare: threshold reduced by W-2 Medicare wages
        // Reduced threshold = max(0, $200K − $150K) = $50K
        // Additional Medicare = max(0, $92,350 − $50,000) × 0.009 = $42,350 × 0.009 = $381.15
        Assert.Equal(381.15m, result.AdditionalMedicareTax);

        // W-2 wages recorded in result
        Assert.Equal(150_000m, result.W2SocialSecurityWages);
        Assert.Equal(150_000m, result.W2MedicareWages);

        // Total SE tax should be lower than without W-2 coordination
        var totalSeTax = 4_278.00m + 2_678.15m + 381.15m;
        Assert.Equal(totalSeTax, result.TotalSeTax);
    }

    [Fact]
    public void W2PlusSelfEmployed_HighW2Wages_ZeroSeSocialSecurity()
    {
        // Scenario: W-2 wages exceed the SS wage base entirely.
        // No SE Social Security tax owed at all.
        var input = new SelfEmploymentInput
        {
            GrossRevenue = 80_000m,
            TotalBusinessExpenses = 10_000m,
            OtherIncome = 200_000m,
            W2SocialSecurityWages = 190_000m, // exceeds $184,500 cap
            W2MedicareWages = 200_000m, // at the threshold
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX
        };

        var result = _calc.Calculate(input);

        // Net profit = $70K, SE taxable = $70K × 0.9235 = $64,645
        Assert.Equal(70_000m, result.NetProfit);
        Assert.Equal(64_645.00m, result.SeTaxableEarnings);

        // SS tax = $0 (W-2 wages exceed wage base)
        Assert.Equal(0m, result.SocialSecurityTax);

        // Additional Medicare: threshold reduced by W-2 Medicare wages
        // Reduced threshold = max(0, $200K − $200K) = $0
        // All SE taxable earnings get Additional Medicare: $64,645 × 0.009 = $581.81
        Assert.Equal(581.81m, result.AdditionalMedicareTax);
    }

    [Fact]
    public void W2PlusSelfEmployed_NoW2Wages_SameAsBaseline()
    {
        // Providing zero W-2 wages should produce the same result as not providing them
        var baseInput = new SelfEmploymentInput
        {
            GrossRevenue = 120_000m,
            TotalBusinessExpenses = 20_000m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX
        };

        var w2Input = new SelfEmploymentInput
        {
            GrossRevenue = 120_000m,
            TotalBusinessExpenses = 20_000m,
            W2SocialSecurityWages = 0m,
            W2MedicareWages = 0m,
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            State = UsState.TX
        };

        var baseResult = _calc.Calculate(baseInput);
        var w2Result = _calc.Calculate(w2Input);

        Assert.Equal(baseResult.SocialSecurityTax, w2Result.SocialSecurityTax);
        Assert.Equal(baseResult.MedicareTax, w2Result.MedicareTax);
        Assert.Equal(baseResult.AdditionalMedicareTax, w2Result.AdditionalMedicareTax);
        Assert.Equal(baseResult.TotalSeTax, w2Result.TotalSeTax);
        Assert.Equal(baseResult.TotalTax, w2Result.TotalTax);
    }
}
