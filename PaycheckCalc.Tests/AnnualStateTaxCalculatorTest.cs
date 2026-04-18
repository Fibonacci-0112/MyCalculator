using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Georgia;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.SelfEmployment;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.State.Annual;
using Xunit;

namespace PaycheckCalc.Tests;

/// <summary>
/// Unit tests for <see cref="AnnualStateTaxCalculator"/> — the annual
/// state/local income tax projection that sits above
/// <see cref="StateCalculatorRegistry"/>.
///
/// Expected dollar amounts are derived by hand from each state's published
/// rate / bracket / allowance values at annual frequency, NOT by calling
/// production helpers (per the repo's testing instructions).
/// </summary>
public class AnnualStateTaxCalculatorTest
{
    private static StateCalculatorRegistry BuildRegistry()
    {
        var registry = new StateCalculatorRegistry();

        // No-income-tax states
        foreach (var s in new[] { UsState.TX, UsState.FL, UsState.WA })
            registry.Register(new NoIncomeTaxWithholdingAdapter(s));

        // Pennsylvania — flat 3.07%
        registry.Register(new PennsylvaniaWithholdingCalculator());

        // Percentage-method configs (annualized). Utah (flat 4.65%, no std
        // deduction/allowances) gives a clean hand-computable scenario.
        foreach (var (state, config) in StateTaxConfigs2026.Configs)
        {
            if (state == UsState.UT)
                registry.Register(new PercentageMethodWithholdingAdapter(state, config));
        }

        // Georgia — dedicated calculator (flat 5.19%, $12,000 / $24,000 std
        // deductions, $4,000 dependent allowance) per the 2026 Employer's
        // Tax Guide.
        registry.Register(new GeorgiaWithholdingCalculator());

        return registry;
    }

    // ── No-income-tax state ─────────────────────────────────
    // TX resident with $5,000 Box 17 withheld (nonsensical but tests the
    // passthrough): liability = $0, refund = withheld.
    [Fact]
    public void NoIncomeTaxState_ReturnsZeroLiability()
    {
        var calc = new AnnualStateTaxCalculator(BuildRegistry());

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.TX,
            W2Jobs = new[]
            {
                new W2JobInput { WagesBox1 = 80_000m, StateWithholdingBox17 = 5_000m }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 0m);

        Assert.Equal(UsState.TX, result.State);
        Assert.True(result.IsNoIncomeTaxState);
        Assert.Equal(0m, result.StateIncomeTax);
        Assert.Equal(5_000m, result.StateTaxWithheld);
        Assert.Equal(5_000m, result.StateRefundOrOwe);
        Assert.Equal("No state income tax", result.Description);
    }

    // ── Pennsylvania flat 3.07% ─────────────────────────────
    // Wages $60,000, Box 17 $2,000. Liability = 60,000 × 3.07% = $1,842.
    // Refund = 2,000 − 1,842 = $158.
    [Fact]
    public void Pennsylvania_FlatRate_ProducesRefund()
    {
        var calc = new AnnualStateTaxCalculator(BuildRegistry());

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.PA,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 60_000m,
                    StateWagesBox16 = 60_000m,
                    StateWithholdingBox17 = 2_000m
                }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 5_000m);

        Assert.Equal(UsState.PA, result.State);
        Assert.False(result.IsNoIncomeTaxState);
        Assert.Equal(60_000m, result.StateWages);
        Assert.Equal(1_842.00m, result.StateIncomeTax);
        Assert.Equal(2_000m, result.StateTaxWithheld);
        Assert.Equal(158.00m, result.StateRefundOrOwe);
    }

    // ── Utah flat 4.65%, no std deduction ───────────────────
    // Wages $100,000, no Box 17. Liability = 100,000 × 4.65% = $4,650.
    // Refund = 0 − 4,650 = −$4,650 (owe).
    [Fact]
    public void Utah_FlatRate_NoWithholding_ProducesBalanceDue()
    {
        var calc = new AnnualStateTaxCalculator(BuildRegistry());

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.UT,
            W2Jobs = new[]
            {
                new W2JobInput { WagesBox1 = 100_000m }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 0m);

        Assert.Equal(4_650.00m, result.StateIncomeTax);
        Assert.Equal(0m, result.StateTaxWithheld);
        Assert.Equal(-4_650.00m, result.StateRefundOrOwe);
    }

    // ── Georgia flat 5.19% with $12,000 single std deduction ─
    // Wages $70,000, Box 17 $3,700. G-4 defaults to status A (Single).
    // Annual taxable = 70,000 − 12,000 = $58,000.
    // Liability = 58,000 × 5.19% = $3,010.20.
    // Refund = 3,700 − 3,010.20 = $689.80.
    [Fact]
    public void Georgia_AppliesSingleStandardDeduction()
    {
        var calc = new AnnualStateTaxCalculator(BuildRegistry());

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.GA,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 70_000m,
                    StateWagesBox16 = 70_000m,
                    StateWithholdingBox17 = 3_700m
                }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 6_000m);

        Assert.Equal(3_010.20m, result.StateIncomeTax);
        Assert.Equal(3_700m, result.StateTaxWithheld);
        Assert.Equal(689.80m, result.StateRefundOrOwe);
    }

    // ── Fallback from Box 1 when Box 16 is missing ─────────
    [Fact]
    public void FallsBackToBox1_WhenBox16NotProvided()
    {
        var calc = new AnnualStateTaxCalculator(BuildRegistry());

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.PA,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 40_000m,
                    // StateWagesBox16 deliberately omitted (defaults to 0)
                    StateWithholdingBox17 = 1_228m
                }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 0m);

        // State wages should fall back to Box 1 = 40,000
        Assert.Equal(40_000m, result.StateWages);
        // PA flat 3.07% on $40,000 = $1,228
        Assert.Equal(1_228.00m, result.StateIncomeTax);
        Assert.Equal(0m, result.StateRefundOrOwe);
    }

    // ── Multi-W-2 aggregation ───────────────────────────────
    [Fact]
    public void MultiW2_AggregatesWagesAndWithholding()
    {
        var calc = new AnnualStateTaxCalculator(BuildRegistry());

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.PA,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 40_000m, StateWagesBox16 = 40_000m,
                    StateWithholdingBox17 = 1_500m
                },
                new W2JobInput
                {
                    WagesBox1 = 20_000m, StateWagesBox16 = 20_000m,
                    StateWithholdingBox17 = 800m
                }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 0m);

        // Combined state wages = $60,000; PA liability = 60,000 × 3.07% = $1,842
        // Withheld = 1,500 + 800 = $2,300 → refund $458
        Assert.Equal(60_000m, result.StateWages);
        Assert.Equal(1_842.00m, result.StateIncomeTax);
        Assert.Equal(2_300m, result.StateTaxWithheld);
        Assert.Equal(458.00m, result.StateRefundOrOwe);
    }

    // ── Zero-wage guard ─────────────────────────────────────
    [Fact]
    public void NoWages_ReturnsZeroLiability_ButPassesThroughWithholding()
    {
        var calc = new AnnualStateTaxCalculator(BuildRegistry());

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.PA,
            W2Jobs = new[]
            {
                new W2JobInput { WagesBox1 = 0m, StateWithholdingBox17 = 50m }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 0m);

        Assert.Equal(0m, result.StateWages);
        Assert.Equal(0m, result.StateIncomeTax);
        Assert.Equal(50m, result.StateTaxWithheld);
        Assert.Equal(50m, result.StateRefundOrOwe);
    }

    // ── Unregistered state — no crash, conservative passthrough ──
    [Fact]
    public void UnregisteredState_DoesNotThrow_PassesThroughWithholding()
    {
        var registry = new StateCalculatorRegistry(); // empty
        var calc = new AnnualStateTaxCalculator(registry);

        var profile = new TaxYearProfile
        {
            ResidenceState = UsState.CA,
            W2Jobs = new[]
            {
                new W2JobInput { WagesBox1 = 50_000m, StateWithholdingBox17 = 1_000m }
            }
        };

        var result = calc.Calculate(profile, federalTaxAnnual: 0m);

        Assert.Equal(UsState.CA, result.State);
        Assert.Equal(0m, result.StateIncomeTax);
        Assert.Equal(1_000m, result.StateTaxWithheld);
        Assert.Equal(1_000m, result.StateRefundOrOwe);
        Assert.NotNull(result.Description);
    }

    // ── End-to-end wiring through Form1040Calculator ────────
    // When AnnualStateTaxCalculator is passed to Form1040Calculator the
    // result's StateTax property should be populated.
    [Fact]
    public void Form1040Calculator_PopulatesStateTax_WhenStateCalcWired()
    {
        var registry = BuildRegistry();
        var stateCalc = new AnnualStateTaxCalculator(registry);

        var bracketsJson = File.ReadAllText("federal_1040_brackets_2026.json");
        var fed = new Federal1040TaxCalculator(bracketsJson);
        var fica = new FicaCalculator();
        var seTax = new SelfEmploymentTaxCalculator(fica);
        var qbi = new QbiDeductionCalculator();
        var sched1 = new Schedule1Calculator();

        var calc = new Form1040Calculator(fed, sched1, seTax, qbi, fica,
            stateTax: stateCalc);

        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            ResidenceState = UsState.PA,
            W2Jobs = new[]
            {
                new W2JobInput
                {
                    WagesBox1 = 60_000m,
                    StateWagesBox16 = 60_000m,
                    StateWithholdingBox17 = 2_000m,
                    FederalWithholdingBox2 = 6_000m,
                    SocialSecurityWagesBox3 = 60_000m,
                    SocialSecurityTaxBox4 = 3_720m,
                    MedicareWagesBox5 = 60_000m,
                    MedicareTaxBox6 = 870m
                }
            }
        };

        var result = calc.Calculate(profile);

        Assert.NotNull(result.StateTax);
        Assert.Equal(UsState.PA, result.StateTax!.State);
        Assert.Equal(1_842.00m, result.StateTax.StateIncomeTax);
        Assert.Equal(158.00m, result.StateTax.StateRefundOrOwe);
    }

    // Back-compat: without state calc, StateTax is null.
    [Fact]
    public void Form1040Calculator_LeavesStateTaxNull_WhenNotWired()
    {
        var bracketsJson = File.ReadAllText("federal_1040_brackets_2026.json");
        var fed = new Federal1040TaxCalculator(bracketsJson);
        var fica = new FicaCalculator();
        var seTax = new SelfEmploymentTaxCalculator(fica);
        var qbi = new QbiDeductionCalculator();
        var sched1 = new Schedule1Calculator();

        var calc = new Form1040Calculator(fed, sched1, seTax, qbi, fica);

        var profile = new TaxYearProfile
        {
            FilingStatus = FederalFilingStatus.SingleOrMarriedSeparately,
            W2Jobs = new[] { new W2JobInput { WagesBox1 = 50_000m } }
        };

        var result = calc.Calculate(profile);
        Assert.Null(result.StateTax);
    }
}
