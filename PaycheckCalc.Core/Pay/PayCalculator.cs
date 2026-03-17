using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Pay;

public sealed class PayCalculator
{
    private readonly StateTaxCalculatorFactory _stateFactory;
    private readonly FicaCalculator _fica;
    private readonly Irs15TPercentageCalculator _fed;

     public PayCalculator(StateTaxCalculatorFactory stateFactory, FicaCalculator fica, Irs15TPercentageCalculator fed)
    {
        _stateFactory = stateFactory;
        _fica = fica;
        _fed = fed;
    }

    public PaycheckResult Calculate(PaycheckInput input)
    {
        var gross = (input.RegularHours * input.HourlyRate)
                 + (input.OvertimeHours * input.HourlyRate * input.OvertimeMultiplier);

        var preTax = input.Deductions.Where(d => d.Type == DeductionType.PreTax).Sum(d => d.Amount);
        var postTax = input.Deductions.Where(d => d.Type == DeductionType.PostTax).Sum(d => d.Amount);

        var preTaxState = input.Deductions.Where(d => d.Type == DeductionType.PreTax && d.ReducesStateTaxableWages).Sum(d => d.Amount);

        var stateTax = _stateFactory.GetCalculator(input.State);
        var stateResult = stateTax.CalculateWithholding(new StateTaxInput
        {
            GrossWages = gross,
            Frequency = input.Frequency,
            FilingStatus = input.FilingStatus,
            Allowances = input.StateAllowances,
            AdditionalWithholding = input.StateAdditionalWithholding,
            PreTaxDeductionsReducingStateWages = preTaxState
        });

        var ficaWages = Math.Max(0m, gross - preTax);
        var (ss, medicare, addl) = _fica.Calculate(ficaWages, input.YtdSocialSecurityWages, input.YtdMedicareWages);

        var fedTaxable = Math.Max(0m, gross - preTax);
        var federal = _fed.CalculateWithholding(fedTaxable, input.Frequency, input.FederalW4);

          var net = gross - preTax - postTax - stateResult.Withholding - ss - medicare - addl - federal;

        return new PaycheckResult
        {
            GrossPay = RoundMoney(gross),
            PreTaxDeductions = RoundMoney(preTax),
            PostTaxDeductions = RoundMoney(postTax),
            State = input.State,
            StateTaxableWages = RoundMoney(stateResult.TaxableWages),
            StateWithholding = RoundMoney(stateResult.Withholding),
            SocialSecurityWithholding = RoundMoney(ss),
            MedicareWithholding = RoundMoney(medicare),
            AdditionalMedicareWithholding = RoundMoney(addl),
            FederalWithholding = RoundMoney(federal),
            NetPay = RoundMoney(net)
        };
    }

    private static decimal RoundMoney(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
