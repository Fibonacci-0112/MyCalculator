using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.SelfEmployment;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Orchestrator for the annual Form 1040 tax computation. Peer to
/// <see cref="PaycheckCalc.Core.Pay.PayCalculator"/> (per-paycheck) and
/// <see cref="SelfEmploymentCalculator"/> (self-employment focused).
///
/// Composes: multi-W-2 aggregation → optional Schedule C/SE (reusing the
/// existing SE pipeline) → Schedule 1 (additional income + adjustments) →
/// standard/itemized deduction → QBI (Form 8995/8995-A, reused) →
/// bracket tax → nonrefundable credits → Schedule 2 other taxes →
/// payments (W-2 WH + 1040-ES + excess SS credit + refundable credits) →
/// refund/owe.
///
/// This is the engine that turns the app into a year-round financial-decision
/// tool. It does NOT run the pay-stub state withholding engine; state tax is
/// an estimate done elsewhere. Per-paycheck flow in <see cref="PaycheckCalc.Core.Pay.PayCalculator"/>
/// is unchanged.
/// </summary>
public sealed class Form1040Calculator
{
    private readonly Federal1040TaxCalculator _tax;
    private readonly Schedule1Calculator _sched1;
    private readonly SelfEmploymentTaxCalculator _seTax;
    private readonly QbiDeductionCalculator _qbi;
    private readonly FicaCalculator _fica;

    public Form1040Calculator(
        Federal1040TaxCalculator tax,
        Schedule1Calculator sched1,
        SelfEmploymentTaxCalculator seTax,
        QbiDeductionCalculator qbi,
        FicaCalculator fica)
    {
        _tax = tax;
        _sched1 = sched1;
        _seTax = seTax;
        _qbi = qbi;
        _fica = fica;
    }

    public AnnualTaxResult Calculate(TaxYearProfile profile)
    {
        // ── Step 1: Aggregate W-2 jobs ───────────────────────
        var w2Wages = profile.W2Jobs.Sum(j => j.WagesBox1);
        var w2FedWH = profile.W2Jobs.Sum(j => j.FederalWithholdingBox2);
        var w2SsWages = profile.W2Jobs.Sum(j => j.SocialSecurityWagesBox3);
        var w2SsTax = profile.W2Jobs.Sum(j => j.SocialSecurityTaxBox4);
        var w2MedicareWages = profile.W2Jobs.Sum(j => j.MedicareWagesBox5);

        // ── Step 2: Schedule C + SE tax (reuse existing pipeline) ──
        decimal scheduleCNetProfit = 0m;
        SelfEmploymentTaxResult seResult = SelfEmploymentTaxResult.Zero;
        SelfEmploymentInput? se = profile.SelfEmployment;

        if (se is not null)
        {
            // Schedule C line 31: net profit or loss.
            scheduleCNetProfit = R(se.GrossRevenue - se.CostOfGoodsSold - se.TotalBusinessExpenses);

            // Schedule SE — coordinate SS wage base and Additional Medicare
            // threshold with the taxpayer's aggregated W-2 wages.
            seResult = _seTax.Calculate(
                netSelfEmploymentEarnings: scheduleCNetProfit,
                w2SocialSecurityWages: w2SsWages,
                w2MedicareWages: w2MedicareWages);
        }

        // ── Step 3: Schedule 1 (additional income + adjustments) ──
        var s1 = _sched1.Calculate(profile.OtherIncome, profile.Adjustments);

        // ── Step 4: Total income (Form 1040 line 9) ──────────
        // Schedule C net profit is included only when positive; a business
        // loss flows through Schedule 1 in real 1040 mechanics but we already
        // guard SE tax against negative profit, and for the engine a loss
        // reduces total income through Schedule 1's existing aggregation.
        var scheduleCIncomeForAgi = scheduleCNetProfit; // negative allowed
        var totalIncome = R(w2Wages + scheduleCIncomeForAgi + s1.AdditionalIncome);

        // ── Step 5: AGI (Form 1040 line 11) ───────────────────
        // Total adjustments = Schedule 1 Part II total + deductible half of SE tax
        var totalAdjustments = R(s1.AdjustmentsExcludingSeTax + seResult.DeductibleHalfOfSeTax);
        var agi = R(totalIncome - totalAdjustments);

        // ── Step 6: Deductions ───────────────────────────────
        var standardDeduction = _tax.GetStandardDeduction(profile.FilingStatus);
        var itemizedOver = Math.Max(0m, profile.ItemizedDeductionsOverStandard);
        var totalDeduction = R(standardDeduction + itemizedOver);

        var taxableBeforeQbi = Math.Max(0m, R(agi - totalDeduction));

        // ── Step 7: QBI deduction (Form 8995/8995-A) ─────────
        decimal qbiDeduction = 0m;
        if (se is not null)
        {
            qbiDeduction = _qbi.Calculate(
                qualifiedBusinessIncome: Math.Max(0m, scheduleCNetProfit),
                taxableIncomeBeforeQbi: taxableBeforeQbi,
                filingStatus: profile.FilingStatus,
                isSstb: se.IsSpecifiedServiceBusiness,
                w2Wages: se.QualifiedBusinessW2Wages,
                ubia: se.QualifiedPropertyUbia);
        }

        // ── Step 8: Taxable income (Form 1040 line 15) ───────
        var taxableIncome = Math.Max(0m, R(taxableBeforeQbi - qbiDeduction));

        // ── Step 9: Income tax before credits (line 16) ──────
        var incomeTaxBefore = _tax.CalculateTax(taxableIncome, profile.FilingStatus);
        var marginalRate = _tax.GetMarginalRate(taxableIncome, profile.FilingStatus);

        // ── Step 10: Nonrefundable credits (Schedule 3 + CTC) ──
        // Simplified — CTC is capped at its nonrefundable ceiling of
        // $2,200/child per OBBBA for 2026, but the engine currently accepts
        // a pre-computed amount. A dedicated CTC calculator will land later.
        var ctc = Math.Max(0m, profile.Credits.ChildTaxCredit);
        var otherNonrefundable = Math.Max(0m, profile.Credits.NonrefundableCredits);
        var totalNonrefundable = R(ctc + otherNonrefundable);
        var nonrefundableApplied = Math.Min(incomeTaxBefore, totalNonrefundable);

        var incomeTaxAfterCredits = R(incomeTaxBefore - nonrefundableApplied);

        // ── Step 11: Schedule 2 other taxes ──────────────────
        var niit = Math.Max(0m, profile.OtherTaxes.NetInvestmentIncomeTax);
        var otherSch2 = Math.Max(0m, profile.OtherTaxes.OtherSchedule2Taxes);
        var seTaxTotal = seResult.TotalSeTax;

        var totalTax = R(incomeTaxAfterCredits + seTaxTotal + niit + otherSch2);

        // ── Step 12: Payments ─────────────────────────────────
        // Excess SS credit: Schedule 3 line 11. When an employee has multiple
        // employers that together withheld SS tax on wages above the annual
        // SS wage base, the excess becomes a credit. We compute this from
        // reported W-2 Box 4 vs. the statutory max (wage base × 6.2%).
        var maxSsTax = R(_fica.SocialSecurityWageBase * FicaCalculator.SocialSecurityRate);
        var excessSs = profile.W2Jobs.Count >= 2 && w2SsTax > maxSsTax
            ? R(w2SsTax - maxSsTax)
            : 0m;

        var refundable = Math.Max(0m, profile.Credits.RefundableCredits);
        var estimatedPayments = Math.Max(0m, profile.EstimatedTaxPayments);

        var totalPayments = R(w2FedWH + estimatedPayments + excessSs + refundable);

        // ── Step 13: Refund or owe ──────────────────────────
        var refundOrOwe = R(totalPayments - totalTax);

        var effectiveRate = totalIncome > 0m
            ? R(totalTax / totalIncome * 100m)
            : 0m;

        return new AnnualTaxResult
        {
            TaxYear = profile.TaxYear,
            FilingStatus = profile.FilingStatus,

            TotalW2Wages = R(w2Wages),
            ScheduleCNetProfit = R(scheduleCNetProfit),
            AdditionalIncome = s1.AdditionalIncome,
            TotalAdjustments = totalAdjustments,
            TotalIncome = totalIncome,
            AdjustedGrossIncome = agi,

            StandardDeduction = standardDeduction,
            ItemizedDeductionsOverStandard = R(itemizedOver),
            QbiDeduction = qbiDeduction,
            TaxableIncome = taxableIncome,

            IncomeTaxBeforeCredits = incomeTaxBefore,
            NonrefundableCredits = R(nonrefundableApplied),
            ChildTaxCredit = R(Math.Min(ctc, Math.Max(0m, incomeTaxBefore - otherNonrefundable))),
            IncomeTaxAfterCredits = incomeTaxAfterCredits,

            SelfEmploymentTax = R(seTaxTotal),
            NetInvestmentIncomeTax = R(niit),
            OtherSchedule2Taxes = R(otherSch2),
            TotalTax = totalTax,

            FederalWithholdingFromW2s = R(w2FedWH),
            EstimatedTaxPayments = R(estimatedPayments),
            ExcessSocialSecurityCredit = excessSs,
            RefundableCredits = R(refundable),
            TotalPayments = totalPayments,

            RefundOrOwe = refundOrOwe,
            EffectiveTaxRate = effectiveRate,
            MarginalTaxRate = marginalRate
        };
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
