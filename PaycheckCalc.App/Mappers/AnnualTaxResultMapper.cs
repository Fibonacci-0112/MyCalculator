using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps a domain <see cref="AnnualTaxResult"/> (optionally carrying an
/// <see cref="AnnualStateTaxResult"/>) to a UI-friendly
/// <see cref="AnnualTaxResultModel"/>.
/// </summary>
public static class AnnualTaxResultMapper
{
    public static AnnualTaxResultModel Map(AnnualTaxResult r)
    {
        var state = r.StateTax;

        return new AnnualTaxResultModel
        {
            TaxYear = r.TaxYear,
            FilingStatusDisplay = EnumDisplay.FederalFilingStatus(r.FilingStatus.ToString()),

            TotalW2Wages = r.TotalW2Wages,
            ScheduleCNetProfit = r.ScheduleCNetProfit,
            AdditionalIncome = r.AdditionalIncome,
            TotalAdjustments = r.TotalAdjustments,
            TotalIncome = r.TotalIncome,
            AdjustedGrossIncome = r.AdjustedGrossIncome,

            StandardDeduction = r.StandardDeduction,
            ItemizedDeductionsOverStandard = r.ItemizedDeductionsOverStandard,
            QbiDeduction = r.QbiDeduction,
            TaxableIncome = r.TaxableIncome,

            IncomeTaxBeforeCredits = r.IncomeTaxBeforeCredits,
            NonrefundableCredits = r.NonrefundableCredits,
            ChildTaxCredit = r.ChildTaxCredit,
            EducationCreditsNonrefundable = r.EducationCreditsNonrefundable,
            SaversCredit = r.SaversCredit,
            IncomeTaxAfterCredits = r.IncomeTaxAfterCredits,

            SelfEmploymentTax = r.SelfEmploymentTax,
            NetInvestmentIncomeTax = r.NetInvestmentIncomeTax,
            OtherSchedule2Taxes = r.OtherSchedule2Taxes,
            TotalTax = r.TotalTax,

            FederalWithholdingFromW2s = r.FederalWithholdingFromW2s,
            EstimatedTaxPayments = r.EstimatedTaxPayments,
            ExcessSocialSecurityCredit = r.ExcessSocialSecurityCredit,
            RefundableCredits = r.RefundableCredits,
            RefundableEducationCredit = r.RefundableEducationCredit,
            RefundableAdditionalChildTaxCredit = r.RefundableAdditionalChildTaxCredit,
            TotalPayments = r.TotalPayments,

            RefundOrOwe = r.RefundOrOwe,
            EffectiveTaxRate = r.EffectiveTaxRate,
            MarginalTaxRate = r.MarginalTaxRate,

            StateName = state is not null
                ? EnumDisplay.UsStateName(state.State.ToString())
                : "",
            IsNoIncomeTaxState = state?.IsNoIncomeTaxState ?? false,
            StateWages = state?.StateWages ?? 0m,
            StateIncomeTax = state?.StateIncomeTax ?? 0m,
            StateDisabilityInsurance = state?.StateDisabilityInsurance ?? 0m,
            StateDisabilityInsuranceLabel = state?.StateDisabilityInsuranceLabel ?? "State Disability Insurance",
            StateTaxWithheld = state?.StateTaxWithheld ?? 0m,
            StateRefundOrOwe = state?.StateRefundOrOwe ?? 0m,
            StateDescription = state?.Description
        };
    }
}
