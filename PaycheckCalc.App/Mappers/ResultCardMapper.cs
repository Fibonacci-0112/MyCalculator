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
            StateDisabilityInsuranceLabel = result.StateDisabilityInsuranceLabel,
            PreTaxDeductions = result.PreTaxDeductions,
            PostTaxDeductions = result.PostTaxDeductions,
            TotalTaxes = result.TotalTaxes,
            NetPay = result.NetPay,
            StateName = EnumDisplay.UsStateName(result.State.ToString()),
            FederalExplanation = MapExplanation(result.FederalExplanation),
            SocialSecurityExplanation = MapExplanation(result.SocialSecurityExplanation),
            MedicareExplanation = MapExplanation(result.MedicareExplanation),
            AdditionalMedicareExplanation = MapExplanation(result.AdditionalMedicareExplanation),
            StateExplanation = MapExplanation(result.StateExplanation),
        };
    }

    private static LineItemExplanationModel? MapExplanation(LineItemExplanation? e)
    {
        if (e is null) return null;
        var inputs = new List<ExplanationInputModel>(e.Inputs.Count);
        foreach (var i in e.Inputs)
            inputs.Add(new ExplanationInputModel { Label = i.Label, Value = i.Value });
        return new LineItemExplanationModel
        {
            Title = e.Title,
            Method = e.Method,
            Table = e.Table,
            Inputs = inputs,
            Note = e.Note,
        };
    }
}
