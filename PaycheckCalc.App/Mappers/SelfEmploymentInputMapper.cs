using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps <see cref="SelfEmploymentViewModel"/> state to a domain
/// <see cref="SelfEmploymentInput"/> ready for the calculation engine.
/// </summary>
public static class SelfEmploymentInputMapper
{
    public static SelfEmploymentInput Map(SelfEmploymentViewModel vm, StateInputValues stateValues)
    {
        return new SelfEmploymentInput
        {
            GrossRevenue = vm.GrossRevenue,
            CostOfGoodsSold = vm.CostOfGoodsSold,
            TotalBusinessExpenses = vm.TotalBusinessExpenses,
            OtherIncome = vm.OtherIncome,
            W2SocialSecurityWages = vm.W2SocialSecurityWages,
            W2MedicareWages = vm.W2MedicareWages,
            FilingStatus = vm.FederalFilingStatus,
            State = vm.SelectedState,
            StateInputValues = stateValues,
            ItemizedDeductionsOverStandard = vm.ItemizedDeductionsOverStandard,
            IsSpecifiedServiceBusiness = vm.IsSpecifiedServiceBusiness,
            QualifiedBusinessW2Wages = vm.QualifiedBusinessW2Wages,
            QualifiedPropertyUbia = vm.QualifiedPropertyUbia,
            EstimatedTaxPayments = vm.EstimatedTaxPayments
        };
    }
}
