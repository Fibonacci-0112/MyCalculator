using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.SelfEmployment;

/// <summary>
/// Orchestrates the full self-employment tax estimation, composing
/// Schedule C net profit, SE tax, QBI deduction, federal income tax,
/// and state income tax into a single <see cref="SelfEmploymentResult"/>.
/// This is a peer to <see cref="PaycheckCalc.Core.Pay.PayCalculator"/>
/// for the self-employed / contractor use case.
/// </summary>
public sealed class SelfEmploymentCalculator
{
    private readonly SelfEmploymentTaxCalculator _seTax;
    private readonly QbiDeductionCalculator _qbi;
    private readonly Irs15TPercentageCalculator _fed;
    private readonly StateCalculatorRegistry _stateRegistry;

    // ── 2026 projected standard deduction amounts ───────────
    // Indexed from 2025 IRS values ($15,000 Single → ~$15,700 projected)
    private static readonly Dictionary<FederalFilingStatus, decimal> StandardDeductions2026 = new()
    {
        [FederalFilingStatus.SingleOrMarriedSeparately] = 15_700m,
        [FederalFilingStatus.MarriedFilingJointly] = 31_400m,
        [FederalFilingStatus.HeadOfHousehold] = 23_550m
    };

    public SelfEmploymentCalculator(
        SelfEmploymentTaxCalculator seTax,
        QbiDeductionCalculator qbi,
        Irs15TPercentageCalculator fed,
        StateCalculatorRegistry stateRegistry)
    {
        _seTax = seTax;
        _qbi = qbi;
        _fed = fed;
        _stateRegistry = stateRegistry;
    }

    public SelfEmploymentResult Calculate(SelfEmploymentInput input)
    {
        // ── Step 1: Schedule C net profit ────────────────────
        var netProfit = R(input.GrossRevenue - input.CostOfGoodsSold - input.TotalBusinessExpenses);

        // ── Step 2: Self-Employment Tax ─────────────────────
        // Coordinate FICA with W-2 wages to respect the shared SS wage
        // base cap and Additional Medicare threshold.
        var seResult = _seTax.Calculate(netProfit, input.W2SocialSecurityWages, input.W2MedicareWages);

        // ── Step 3: Adjusted Gross Income ───────────────────
        // AGI = other income + net profit − deductible half of SE tax
        var agi = R(input.OtherIncome + Math.Max(0m, netProfit) - seResult.DeductibleHalfOfSeTax);

        // ── Step 4: Standard deduction ──────────────────────
        var standardDeduction = StandardDeductions2026.GetValueOrDefault(input.FilingStatus, 15_700m);

        // If itemized deductions exceed standard, use the higher amount
        var totalDeduction = R(standardDeduction + Math.Max(0m, input.ItemizedDeductionsOverStandard));

        // ── Step 5: QBI deduction ───────────────────────────
        // Taxable income before QBI = AGI − deductions
        var taxableBeforeQbi = R(Math.Max(0m, agi - totalDeduction));

        var qbiDeduction = _qbi.Calculate(
            qualifiedBusinessIncome: Math.Max(0m, netProfit),
            taxableIncomeBeforeQbi: taxableBeforeQbi,
            filingStatus: input.FilingStatus,
            isSstb: input.IsSpecifiedServiceBusiness,
            w2Wages: input.QualifiedBusinessW2Wages,
            ubia: input.QualifiedPropertyUbia);

        // ── Step 6: Taxable income ──────────────────────────
        var taxableIncome = R(Math.Max(0m, taxableBeforeQbi - qbiDeduction));

        // ── Step 7: Federal income tax ──────────────────────
        // Use the IRS 15-T annual tables as an approximation.
        // We pass taxableIncome directly with Annual frequency (1 period)
        // and a minimal W-4 input (no credits/extra withholding).
        var federalW4 = new FederalW4Input
        {
            FilingStatus = input.FilingStatus,
            Step2Checked = true // Use the Step 2 schedule for single-earner SE accuracy
        };
        var federalTax = R(_fed.CalculateWithholding(taxableIncome, PayFrequency.Annual, federalW4));

        // ── Step 8: State income tax ────────────────────────
        var stateTax = CalculateStateTax(input, netProfit, seResult.DeductibleHalfOfSeTax, federalTax);

        // ── Step 9: Summary ─────────────────────────────────
        var totalFederal = R(federalTax + seResult.TotalSeTax);
        var totalTax = R(totalFederal + stateTax);

        var totalIncome = R(input.GrossRevenue + input.OtherIncome);
        var effectiveRate = totalIncome > 0m ? R(totalTax / totalIncome * 100m) : 0m;

        var quarterlyPayment = R(totalTax / 4m);
        var overUnder = R(input.EstimatedTaxPayments - totalTax);

        return new SelfEmploymentResult
        {
            // Schedule C
            GrossRevenue = R(input.GrossRevenue),
            CostOfGoodsSold = R(input.CostOfGoodsSold),
            TotalExpenses = R(input.TotalBusinessExpenses),
            NetProfit = R(netProfit),

            // W-2 FICA coordination
            W2SocialSecurityWages = R(Math.Max(0m, input.W2SocialSecurityWages)),
            W2MedicareWages = R(Math.Max(0m, input.W2MedicareWages)),

            // SE Tax
            SeTaxableEarnings = seResult.SeTaxableEarnings,
            SocialSecurityTax = seResult.SocialSecurityTax,
            MedicareTax = seResult.MedicareTax,
            AdditionalMedicareTax = seResult.AdditionalMedicareTax,
            TotalSeTax = seResult.TotalSeTax,
            DeductibleHalfOfSeTax = seResult.DeductibleHalfOfSeTax,

            // Income Tax
            OtherIncome = R(input.OtherIncome),
            AdjustedGrossIncome = R(agi),
            StandardDeduction = R(totalDeduction),
            QbiDeduction = qbiDeduction,
            TaxableIncome = taxableIncome,
            FederalIncomeTax = federalTax,
            State = input.State,
            StateIncomeTax = stateTax,

            // Summary
            TotalFederalTax = totalFederal,
            TotalStateTax = stateTax,
            TotalTax = totalTax,
            EffectiveTaxRate = effectiveRate,
            EstimatedQuarterlyPayment = quarterlyPayment,
            OverUnderPayment = overUnder
        };
    }

    /// <summary>
    /// Reuses the existing state calculator registry to estimate state income tax.
    /// Passes net SE income as annual gross wages through the state withholding engine.
    /// </summary>
    private decimal CalculateStateTax(
        SelfEmploymentInput input,
        decimal netProfit,
        decimal deductibleHalfSeTax,
        decimal federalTax)
    {
        if (!_stateRegistry.IsSupported(input.State))
            return 0m;

        // Self-employment state taxable income = net profit + other income
        // (adjusted similarly to how a W-2 annual wage would be treated)
        var stateGross = R(Math.Max(0m, netProfit) + input.OtherIncome);
        if (stateGross <= 0m)
            return 0m;

        var calc = _stateRegistry.GetCalculator(input.State);
        var context = new CommonWithholdingContext(
            input.State,
            stateGross,
            PayFrequency.Annual,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: 0m,
            FederalWithholdingPerPeriod: federalTax);

        var stateValues = input.StateInputValues ?? new StateInputValues();
        var stateResult = calc.Calculate(context, stateValues);

        return R(stateResult.Withholding);
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
