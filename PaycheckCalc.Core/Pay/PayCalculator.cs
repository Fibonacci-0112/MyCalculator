using System.Globalization;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Pay;

public sealed class PayCalculator
{
    private readonly StateCalculatorRegistry _stateRegistry;
    private readonly FicaCalculator _fica;
    private readonly Irs15TPercentageCalculator _fed;
    private readonly LocalCalculatorRegistry? _localRegistry;

    public PayCalculator(
        StateCalculatorRegistry stateRegistry,
        FicaCalculator fica,
        Irs15TPercentageCalculator fed,
        LocalCalculatorRegistry? localRegistry = null)
    {
        _stateRegistry = stateRegistry;
        _fica = fica;
        _fed = fed;
        _localRegistry = localRegistry;
    }

    public PaycheckResult Calculate(PaycheckInput input)
    {
        var gross = (input.RegularHours * input.HourlyRate)
                 + (input.OvertimeHours * input.HourlyRate * input.OvertimeMultiplier);

        var preTax = input.Deductions.Where(d => d.Type == DeductionType.PreTax).Sum(d => d.EffectiveAmount(gross));
        var postTax = input.Deductions.Where(d => d.Type == DeductionType.PostTax).Sum(d => d.EffectiveAmount(gross));

        var preTaxState = input.Deductions.Where(d => d.Type == DeductionType.PreTax && d.ReducesStateTaxableWages).Sum(d => d.EffectiveAmount(gross));

        var ficaWages = Math.Max(0m, gross - preTax);
        var (ss, medicare, addl) = _fica.Calculate(ficaWages, input.YtdSocialSecurityWages, input.YtdMedicareWages);

        var fedTaxable = ficaWages;
        var federal = _fed.CalculateWithholding(fedTaxable, input.Frequency, input.FederalW4);

        var calc = _stateRegistry.GetCalculator(input.State);
        var context = new CommonWithholdingContext(
            input.State,
            gross,
            input.Frequency,
            Year: 2026,
            PreTaxDeductionsReducingStateWages: preTaxState,
            FederalWithholdingPerPeriod: RoundMoney(federal));
        var stateValues = input.StateInputValues ?? new StateInputValues();
        var stateResult = calc.Calculate(context, stateValues);

        // ── Local (sub-state) withholding ─────────────────────
        var (localWithholding, localHeadTax, localTaxable, localityLabel, breakdown) =
            CalculateLocal(input, context);

        var net = gross - preTax - postTax
                - stateResult.Withholding - stateResult.DisabilityInsurance
                - ss - medicare - addl - federal
                - localWithholding - localHeadTax;

        // ── Drill-down explanations for FICA / federal / state ──
        var ficaWagesThisPeriod = ficaWages;
        var ssExplanation = BuildSocialSecurityExplanation(
            ficaWagesThisPeriod, input.YtdSocialSecurityWages, ss, _fica.SocialSecurityWageBase);
        var medicareExplanation = BuildMedicareExplanation(
            ficaWagesThisPeriod, input.YtdMedicareWages, medicare);
        var addlMedicareExplanation = addl > 0m
            ? BuildAdditionalMedicareExplanation(ficaWagesThisPeriod, input.YtdMedicareWages, addl, _fica.AdditionalMedicareEmployerThreshold)
            : null;
        var federalExplanation = BuildFederalExplanation(
            fedTaxable, input.Frequency, input.FederalW4, federal);
        var stateExplanation = stateResult.Explanation
            ?? BuildFallbackStateExplanation(context, stateResult, calc);

        return new PaycheckResult
        {
            GrossPay = RoundMoney(gross),
            PreTaxDeductions = RoundMoney(preTax),
            PostTaxDeductions = RoundMoney(postTax),
            State = input.State,
            StateTaxableWages = RoundMoney(stateResult.TaxableWages),
            StateWithholding = RoundMoney(stateResult.Withholding),
            StateDisabilityInsurance = RoundMoney(stateResult.DisabilityInsurance),
            StateDisabilityInsuranceLabel = stateResult.DisabilityInsuranceLabel,
            SocialSecurityWithholding = RoundMoney(ss),
            MedicareWithholding = RoundMoney(medicare),
            AdditionalMedicareWithholding = RoundMoney(addl),
            FederalTaxableIncome = RoundMoney(fedTaxable),
            FederalWithholding = RoundMoney(federal),
            LocalTaxableWages = RoundMoney(localTaxable),
            LocalWithholding = RoundMoney(localWithholding),
            LocalHeadTax = RoundMoney(localHeadTax),
            LocalHeadTaxLabel = breakdown.FirstOrDefault(l => l.HeadTax > 0m)?.HeadTaxLabel ?? "Local Services Tax",
            LocalityLabel = localityLabel,
            LocalBreakdown = breakdown,
            NetPay = RoundMoney(net),
            FederalExplanation = federalExplanation,
            SocialSecurityExplanation = ssExplanation,
            MedicareExplanation = medicareExplanation,
            AdditionalMedicareExplanation = addlMedicareExplanation,
            StateExplanation = stateExplanation
        };
    }

    // ── Explanation builders ───────────────────────────────────
    // These surface the method/table and key inputs behind each tax line item
    // without leaking raw domain structures into the UI. They are built from
    // information the orchestrator already has.

    private static LineItemExplanation BuildSocialSecurityExplanation(
        decimal medicareWagesThisPeriod, decimal ytdSsWages, decimal ssThisPeriod, decimal wageBase)
    {
        var remaining = Math.Max(0m, wageBase - ytdSsWages);
        var taxable = Math.Min(medicareWagesThisPeriod, remaining);

        var inputs = new List<ExplanationInput>
        {
            new("Rate", (FicaCalculator.SocialSecurityRate).ToString("P1", CultureInfo.InvariantCulture)),
            new("Annual Wage Base", FormatMoney(wageBase)),
            new("YTD Social Security Wages", FormatMoney(ytdSsWages)),
            new("Remaining Wage Base", FormatMoney(remaining)),
            new("FICA Wages This Period", FormatMoney(medicareWagesThisPeriod)),
            new("Taxable This Period", FormatMoney(taxable)),
            new("Withholding This Period", FormatMoney(RoundMoney(ssThisPeriod))),
        };

        return new LineItemExplanation(
            Title: "Social Security Tax (FICA)",
            Method: "FICA Social Security — 6.2% of FICA wages up to the annual wage base",
            Table: $"OASDI wage base {FormatMoney(wageBase)}",
            Inputs: inputs,
            Note: remaining <= 0m ? "YTD wages have reached the annual wage base; no further SS tax is withheld." : null);
    }

    private static LineItemExplanation BuildMedicareExplanation(
        decimal medicareWagesThisPeriod, decimal ytdMedicareWages, decimal medicareThisPeriod)
    {
        var inputs = new List<ExplanationInput>
        {
            new("Rate", FicaCalculator.MedicareRate.ToString("P2", CultureInfo.InvariantCulture)),
            new("YTD Medicare Wages", FormatMoney(ytdMedicareWages)),
            new("Medicare Wages This Period", FormatMoney(medicareWagesThisPeriod)),
            new("Withholding This Period", FormatMoney(RoundMoney(medicareThisPeriod))),
        };
        return new LineItemExplanation(
            Title: "Medicare Tax (FICA)",
            Method: "FICA Medicare — 1.45% of Medicare wages (no cap)",
            Table: null,
            Inputs: inputs);
    }

    private static LineItemExplanation BuildAdditionalMedicareExplanation(
        decimal medicareWagesThisPeriod, decimal ytdMedicareWages, decimal addlThisPeriod, decimal threshold)
    {
        var inputs = new List<ExplanationInput>
        {
            new("Rate", FicaCalculator.AdditionalMedicareRate.ToString("P1", CultureInfo.InvariantCulture)),
            new("Employer Withholding Threshold", FormatMoney(threshold)),
            new("YTD Medicare Wages (before this period)", FormatMoney(ytdMedicareWages)),
            new("Medicare Wages This Period", FormatMoney(medicareWagesThisPeriod)),
            new("Withholding This Period", FormatMoney(RoundMoney(addlThisPeriod))),
        };
        return new LineItemExplanation(
            Title: "Additional Medicare Tax",
            Method: $"Additional Medicare — 0.9% on wages over the {FormatMoney(threshold)} employer withholding threshold",
            Table: null,
            Inputs: inputs,
            Note: "Employers withhold an additional 0.9% on Medicare wages paid to an employee in excess of the threshold in a calendar year, without regard to filing status.");
    }

    private static LineItemExplanation BuildFederalExplanation(
        decimal taxableWagesThisPeriod, PayFrequency frequency, FederalW4Input w4, decimal federalThisPeriod)
    {
        var table = w4.Step2Checked
            ? $"Annual Percentage Method — Form W-4 Step 2 checked ({FilingStatusDisplay(w4.FilingStatus)})"
            : $"Annual Percentage Method — Standard ({FilingStatusDisplay(w4.FilingStatus)})";

        var inputs = new List<ExplanationInput>
        {
            new("Filing Status", FilingStatusDisplay(w4.FilingStatus)),
            new("Pay Frequency", frequency.ToString()),
            new("W-4 Step 2 (Two Jobs) Checked", w4.Step2Checked ? "Yes" : "No"),
            new("W-4 Step 3 Annual Tax Credits", FormatMoney(w4.Step3TaxCredits)),
            new("W-4 Step 4(a) Other Annual Income", FormatMoney(w4.Step4aOtherIncome)),
            new("W-4 Step 4(b) Annual Deductions", FormatMoney(w4.Step4bDeductions)),
            new("W-4 Step 4(c) Extra Withholding (period)", FormatMoney(w4.Step4cExtraWithholding)),
            new("Federal Taxable Wages (period)", FormatMoney(taxableWagesThisPeriod)),
            new("Withholding This Period", FormatMoney(RoundMoney(federalThisPeriod))),
        };

        return new LineItemExplanation(
            Title: "Federal Income Tax",
            Method: "IRS Publication 15-T (2026), Section 1 — Worksheet 1A (Automated Payroll Systems)",
            Table: table,
            Inputs: inputs);
    }

    private static LineItemExplanation BuildFallbackStateExplanation(
        CommonWithholdingContext context,
        StateWithholdingResult stateResult,
        IStateWithholdingCalculator calculator)
    {
        var calcName = calculator.GetType().Name;
        // e.g. "DelawareWithholdingCalculator" → "Delaware Withholding"
        var friendlyMethod = calcName.EndsWith("Calculator", StringComparison.Ordinal)
            ? calcName[..^"Calculator".Length]
            : calcName;

        var inputs = new List<ExplanationInput>
        {
            new("State", context.State.ToString()),
            new("Pay Frequency", context.PayPeriod.ToString()),
            new("Gross Wages (period)", FormatMoney(context.GrossWages)),
            new("Pre-tax Deductions Reducing State Wages", FormatMoney(context.PreTaxDeductionsReducingStateWages)),
            new("State Taxable Wages (period)", FormatMoney(stateResult.TaxableWages)),
            new("Withholding This Period", FormatMoney(RoundMoney(stateResult.Withholding))),
        };

        return new LineItemExplanation(
            Title: "State Income Tax",
            Method: $"{friendlyMethod} (state-specific rules)",
            Table: $"{context.State} {context.Year} state withholding",
            Inputs: inputs,
            Note: stateResult.Description);
    }

    private static string FilingStatusDisplay(FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => "Married Filing Jointly",
        FederalFilingStatus.HeadOfHousehold => "Head of Household",
        _ => "Single or Married Filing Separately"
    };

    private static string FormatMoney(decimal v) =>
        v.ToString("C", CultureInfo.GetCultureInfo("en-US"));

    private (decimal Withholding, decimal HeadTax, decimal TaxableWages, string Label, IReadOnlyList<LocalWithholdingLine> Breakdown)
        CalculateLocal(PaycheckInput input, CommonWithholdingContext common)
    {
        if (_localRegistry is null)
            return (0m, 0m, 0m, string.Empty, Array.Empty<LocalWithholdingLine>());

        // Collect distinct non-empty locality codes (home + work). Resolving to the same
        // calculator on both sides means we only invoke it once; the calculator's schema
        // (e.g., PA EIT) carries both PSDs in its own inputs.
        var codes = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.HomeLocalityCode)) codes.Add(input.HomeLocalityCode!);
        if (!string.IsNullOrWhiteSpace(input.WorkLocalityCode)
            && !string.Equals(input.HomeLocalityCode, input.WorkLocalityCode, StringComparison.OrdinalIgnoreCase))
            codes.Add(input.WorkLocalityCode!);

        if (codes.Count == 0)
            return (0m, 0m, 0m, string.Empty, Array.Empty<LocalWithholdingLine>());

        var values = input.LocalInputValues ?? new LocalInputValues();
        var breakdown = new List<LocalWithholdingLine>();
        decimal totalWithholding = 0m;
        decimal totalHeadTax = 0m;
        decimal aggregateTaxable = 0m;

        foreach (var code in codes)
        {
            if (!_localRegistry.TryGetCalculator(code, out var calculator) || calculator is null)
                continue;

            LocalityId? homeLoc = ResolveLocality(input.HomeLocalityCode, calculator);
            LocalityId? workLoc = ResolveLocality(input.WorkLocalityCode, calculator);
            var isResident = string.Equals(input.HomeLocalityCode, code, StringComparison.OrdinalIgnoreCase);

            var ctx = new CommonLocalWithholdingContext(
                Common: common,
                HomeLocality: homeLoc,
                WorkLocality: workLoc,
                IsResident: isResident,
                CurrentLocality: calculator.Locality);

            var result = calculator.Calculate(ctx, values);
            breakdown.Add(new LocalWithholdingLine(
                calculator.Locality.Code,
                string.IsNullOrEmpty(result.LocalityName) ? calculator.Locality.Name : result.LocalityName,
                result.TaxableWages,
                result.Withholding,
                result.HeadTax,
                result.HeadTaxLabel,
                result.Description));

            totalWithholding += result.Withholding;
            totalHeadTax += result.HeadTax;
            if (result.TaxableWages > aggregateTaxable)
                aggregateTaxable = result.TaxableWages;
        }

        var label = string.Join(" + ", breakdown.Select(l => l.LocalityName).Distinct(StringComparer.OrdinalIgnoreCase));
        return (totalWithholding, totalHeadTax, aggregateTaxable, label, breakdown);
    }

    private LocalityId? ResolveLocality(string? code, ILocalWithholdingCalculator calculator)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (_localRegistry is null) return null;
        if (_localRegistry.TryGetCalculator(code, out var found) && found is not null)
            return found.Locality;
        // Code supplied but no calculator — keep the raw code for context display.
        return new LocalityId(calculator.Locality.State, code!, code!);
    }

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
