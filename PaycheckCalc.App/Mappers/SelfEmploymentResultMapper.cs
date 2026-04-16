using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps a domain <see cref="SelfEmploymentResult"/> to a
/// <see cref="SelfEmploymentResultModel"/> presentation model for the UI.
/// </summary>
public static class SelfEmploymentResultMapper
{
    public static SelfEmploymentResultModel Map(SelfEmploymentResult result)
    {
        return new SelfEmploymentResultModel
        {
            GrossRevenue = result.GrossRevenue,
            CostOfGoodsSold = result.CostOfGoodsSold,
            TotalExpenses = result.TotalExpenses,
            NetProfit = result.NetProfit,
            SeTaxableEarnings = result.SeTaxableEarnings,
            SocialSecurityTax = result.SocialSecurityTax,
            MedicareTax = result.MedicareTax,
            AdditionalMedicareTax = result.AdditionalMedicareTax,
            TotalSeTax = result.TotalSeTax,
            DeductibleHalfOfSeTax = result.DeductibleHalfOfSeTax,
            OtherIncome = result.OtherIncome,
            AdjustedGrossIncome = result.AdjustedGrossIncome,
            StandardDeduction = result.StandardDeduction,
            QbiDeduction = result.QbiDeduction,
            TaxableIncome = result.TaxableIncome,
            FederalIncomeTax = result.FederalIncomeTax,
            StateName = EnumDisplay.UsStateName(result.State.ToString()),
            StateIncomeTax = result.StateIncomeTax,
            TotalFederalTax = result.TotalFederalTax,
            TotalStateTax = result.TotalStateTax,
            TotalTax = result.TotalTax,
            EffectiveTaxRate = result.EffectiveTaxRate,
            EstimatedQuarterlyPayment = result.EstimatedQuarterlyPayment,
            OverUnderPayment = result.OverUnderPayment
        };
    }
}
