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
            NetPay = RoundMoney(net)
        };
    }

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
