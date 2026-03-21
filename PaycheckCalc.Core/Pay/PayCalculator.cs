using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Pay;

public sealed class PayCalculator
{
    private readonly StateCalculatorRegistry _stateRegistry;
    private readonly FicaCalculator _fica;
    private readonly Irs15TPercentageCalculator _fed;

    public PayCalculator(StateCalculatorRegistry stateRegistry, FicaCalculator fica, Irs15TPercentageCalculator fed)
    {
        _stateRegistry = stateRegistry;
        _fica = fica;
        _fed = fed;
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

        var net = gross - preTax - postTax - stateResult.Withholding - stateResult.DisabilityInsurance - ss - medicare - addl - federal;

        return new PaycheckResult
        {
            GrossPay = RoundMoney(gross),
            PreTaxDeductions = RoundMoney(preTax),
            PostTaxDeductions = RoundMoney(postTax),
            State = input.State,
            StateTaxableWages = RoundMoney(stateResult.TaxableWages),
            StateWithholding = RoundMoney(stateResult.Withholding),
            StateDisabilityInsurance = RoundMoney(stateResult.DisabilityInsurance),
            SocialSecurityWithholding = RoundMoney(ss),
            MedicareWithholding = RoundMoney(medicare),
            AdditionalMedicareWithholding = RoundMoney(addl),
            FederalTaxableIncome = RoundMoney(fedTaxable),
            FederalWithholding = RoundMoney(federal),
            NetPay = RoundMoney(net)
        };
    }

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
