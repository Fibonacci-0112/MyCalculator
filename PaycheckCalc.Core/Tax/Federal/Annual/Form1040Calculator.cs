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
    private readonly ChildTaxCreditCalculator _ctc;
    private readonly Form8863EducationCreditsCalculator _education;
    private readonly Form8880SaversCreditCalculator _savers;
    private readonly Form8960NiitCalculator _niit;

    public Form1040Calculator(
        Federal1040TaxCalculator tax,
        Schedule1Calculator sched1,
        SelfEmploymentTaxCalculator seTax,
        QbiDeductionCalculator qbi,
        FicaCalculator fica,
        ChildTaxCreditCalculator? ctc = null,
        Form8863EducationCreditsCalculator? education = null,
        Form8880SaversCreditCalculator? savers = null,
        Form8960NiitCalculator? niit = null)
    {
        _tax = tax;
        _sched1 = sched1;
        _seTax = seTax;
        _qbi = qbi;
        _fica = fica;
        _ctc = ctc ?? new ChildTaxCreditCalculator();
        _education = education ?? new Form8863EducationCreditsCalculator();
        _savers = savers ?? new Form8880SaversCreditCalculator();
        _niit = niit ?? new Form8960NiitCalculator();
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
        // Credits stack in the following order against income tax:
        //   1. Structured CTC/ODC (Form 8812 / OBBBA rules)
        //   2. Structured education credits (Form 8863 — AOTC 60% + LLC)
        //   3. Structured Saver's Credit (Form 8880)
        //   4. Legacy pre-computed nonrefundable lump sum + pre-computed CTC
        // The combined nonrefundable total is capped at the income tax.
        var credits = profile.Credits;

        // CTC needs to be computed against tax; use the pre-credit income tax
        // as the ceiling for the nonrefundable portion per Form 8812 mechanics.
        var ctcResult = credits.ChildTaxCreditInput is not null
            ? _ctc.Calculate(credits.ChildTaxCreditInput, profile.FilingStatus, agi, incomeTaxBefore)
            : ChildTaxCreditResult.Zero;

        var educationResult = credits.EducationCredits is not null
            ? _education.Calculate(credits.EducationCredits, profile.FilingStatus, agi)
            : EducationCreditsResult.Zero;

        var saversResult = credits.SaversCredit is not null
            ? _savers.Calculate(credits.SaversCredit, profile.FilingStatus, agi)
            : SaversCreditResult.Zero;

        var precomputedCtc = Math.Max(0m, credits.PrecomputedChildTaxCredit);
        var legacyOtherNonrefundable = Math.Max(0m, credits.NonrefundableCredits);

        var totalNonrefundableRequested = R(
              ctcResult.NonrefundableApplied
            + educationResult.TotalNonrefundable
            + saversResult.Credit
            + precomputedCtc
            + legacyOtherNonrefundable);

        var nonrefundableApplied = Math.Min(incomeTaxBefore, totalNonrefundableRequested);
        var incomeTaxAfterCredits = R(incomeTaxBefore - nonrefundableApplied);

        // Reported CTC amount: the structured calculator's nonrefundable applied
        // plus whatever slice of the legacy pre-computed CTC can still fit under
        // the remaining tax room. Kept additive for back-compat.
        var taxRoomForPrecomputedCtc = Math.Max(0m,
            incomeTaxBefore - ctcResult.NonrefundableApplied - educationResult.TotalNonrefundable
              - saversResult.Credit - legacyOtherNonrefundable);
        var reportedCtc = R(ctcResult.NonrefundableApplied + Math.Min(precomputedCtc, taxRoomForPrecomputedCtc));

        // ── Step 11: Schedule 2 other taxes ──────────────────
        var niit = Math.Max(0m, profile.OtherTaxes.NetInvestmentIncomeTax);
        if (profile.OtherTaxes.NetInvestmentIncome is not null)
        {
            niit += _niit.Calculate(profile.OtherTaxes.NetInvestmentIncome, profile.FilingStatus, agi);
        }
        niit = R(niit);
        var otherSch2 = Math.Max(0m, profile.OtherTaxes.OtherSchedule2Taxes);
        var seTaxTotal = seResult.TotalSeTax;

        var totalTax = R(incomeTaxAfterCredits + seTaxTotal + niit + otherSch2);

        // ── Step 12: Payments ─────────────────────────────────
        // Excess SS credit: Schedule 3 line 11. When a single taxpayer has
        // wages from two or more employers that together withheld SS tax on
        // wages above the annual SS wage base, the excess becomes a credit.
        //
        // IMPORTANT: the excess-SS test is applied per taxpayer, not per
        // return. On a joint return each spouse gets their own test — one
        // spouse working two jobs can generate a credit even if the other
        // spouse's SS withholding is below the base. We therefore group by
        // <see cref="W2JobInput.Holder"/> for MFJ, and treat all jobs as the
        // single filer's for every other status.
        var maxSsTax = R(_fica.SocialSecurityWageBase * FicaCalculator.SocialSecurityRate);
        var excessSs = CalculateExcessSocialSecurityCredit(
            profile.W2Jobs, profile.FilingStatus, maxSsTax);

        // Refundable credits = legacy pre-computed + refundable slice of AOTC
        // + refundable ACTC. EITC etc. still flow through the legacy field.
        var refundable = R(
              Math.Max(0m, credits.RefundableCredits)
            + educationResult.TotalRefundable
            + ctcResult.RefundableActc);
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
            ChildTaxCredit = reportedCtc,
            EducationCreditsNonrefundable = R(educationResult.TotalNonrefundable),
            SaversCredit = R(saversResult.Credit),
            IncomeTaxAfterCredits = incomeTaxAfterCredits,

            SelfEmploymentTax = R(seTaxTotal),
            NetInvestmentIncomeTax = R(niit),
            OtherSchedule2Taxes = R(otherSch2),
            TotalTax = totalTax,

            FederalWithholdingFromW2s = R(w2FedWH),
            EstimatedTaxPayments = R(estimatedPayments),
            ExcessSocialSecurityCredit = excessSs,
            RefundableCredits = R(refundable),
            RefundableEducationCredit = R(educationResult.TotalRefundable),
            RefundableAdditionalChildTaxCredit = R(ctcResult.RefundableActc),
            TotalPayments = totalPayments,

            RefundOrOwe = refundOrOwe,
            EffectiveTaxRate = effectiveRate,
            MarginalTaxRate = marginalRate
        };
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Excess Social Security tax credit (Schedule 3 line 11). Applied per
    /// taxpayer: each spouse on a joint return is tested independently so
    /// that one spouse with two employers can generate a credit even if the
    /// other spouse has only one job. For single/MFS/HoH returns all jobs
    /// are assigned to a single taxpayer bucket.
    /// </summary>
    private static decimal CalculateExcessSocialSecurityCredit(
        IReadOnlyList<W2JobInput> jobs,
        FederalFilingStatus status,
        decimal maxSsTaxPerTaxpayer)
    {
        if (jobs.Count == 0) return 0m;

        decimal total = 0m;

        if (status == FederalFilingStatus.MarriedFilingJointly)
        {
            // Per-spouse test on joint returns.
            foreach (var holder in new[] { W2JobHolder.Taxpayer, W2JobHolder.Spouse })
            {
                var holderJobs = jobs.Where(j => j.Holder == holder).ToList();
                if (holderJobs.Count < 2) continue; // one employer = no credit
                var holderSsTax = holderJobs.Sum(j => j.SocialSecurityTaxBox4);
                if (holderSsTax > maxSsTaxPerTaxpayer)
                {
                    total += R(holderSsTax - maxSsTaxPerTaxpayer);
                }
            }
            return total;
        }

        // Non-joint: a single-filer Spouse flag is nonsensical, but we still
        // treat every job as the one taxpayer's job to match IRS mechanics.
        if (jobs.Count < 2) return 0m;
        var ssTax = jobs.Sum(j => j.SocialSecurityTaxBox4);
        return ssTax > maxSsTaxPerTaxpayer ? R(ssTax - maxSsTaxPerTaxpayer) : 0m;
    }
}
