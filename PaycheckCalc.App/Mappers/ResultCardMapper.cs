using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps a domain <see cref="PaycheckResult"/> to a
/// <see cref="ResultCardModel"/> presentation model for the UI.
/// </summary>
public static class ResultCardMapper
{
    public static ResultCardModel Map(PaycheckResult result)
    {
        return new ResultCardModel
        {
            GrossPay = result.GrossPay,
            FederalTaxableIncome = result.FederalTaxableIncome,
            StateTaxableWages = result.StateTaxableWages,
            FederalWithholding = result.FederalWithholding,
            SocialSecurityWithholding = result.SocialSecurityWithholding,
            MedicareWithholding = result.MedicareWithholding,
            AdditionalMedicareWithholding = result.AdditionalMedicareWithholding,
            StateWithholding = result.StateWithholding,
            StateDisabilityInsurance = result.StateDisabilityInsurance,
            PreTaxDeductions = result.PreTaxDeductions,
            PostTaxDeductions = result.PostTaxDeductions,
            TotalTaxes = result.TotalTaxes,
            NetPay = result.NetPay,
            StateName = EnumDisplay.UsStateName(result.State.ToString())
        };
    }
}
