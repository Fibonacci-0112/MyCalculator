using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps <see cref="AnnualTaxViewModel"/> state to a Core
/// <see cref="TaxYearProfile"/> ready for <c>Form1040Calculator</c>.
/// </summary>
public static class AnnualTaxInputMapper
{
    public static TaxYearProfile Map(AnnualTaxViewModel vm)
    {
        var jobs = vm.W2Jobs.Select(j => new W2JobInput
        {
            Name = j.Name,
            Holder = j.IsSpouse ? W2JobHolder.Spouse : W2JobHolder.Taxpayer,
            WagesBox1 = j.WagesBox1,
            FederalWithholdingBox2 = j.FederalWithholdingBox2,
            SocialSecurityWagesBox3 = j.SocialSecurityWagesBox3,
            SocialSecurityTaxBox4 = j.SocialSecurityTaxBox4,
            MedicareWagesBox5 = j.MedicareWagesBox5,
            MedicareTaxBox6 = j.MedicareTaxBox6,
            StateWagesBox16 = j.StateWagesBox16,
            StateWithholdingBox17 = j.StateWithholdingBox17
        }).ToList();

        return new TaxYearProfile
        {
            TaxYear = vm.TaxYear,
            FilingStatus = vm.FilingStatus,
            QualifyingChildren = Math.Max(0, vm.QualifyingChildren),
            ResidenceState = vm.SelectedState,
            W2Jobs = jobs,
            ItemizedDeductionsOverStandard = Math.Max(0m, vm.ItemizedDeductionsOverStandard),
            OtherIncome = new OtherIncomeInput
            {
                TaxableInterest = vm.TaxableInterest,
                OrdinaryDividends = vm.OrdinaryDividends,
                QualifiedDividends = vm.QualifiedDividends,
                CapitalGainOrLoss = vm.CapitalGainOrLoss,
                UnemploymentCompensation = vm.UnemploymentCompensation,
                TaxableSocialSecurity = vm.TaxableSocialSecurity,
                OtherAdditionalIncome = vm.OtherAdditionalIncome
            },
            Adjustments = new AdjustmentsInput
            {
                StudentLoanInterest = vm.StudentLoanInterest,
                HsaDeduction = vm.HsaDeduction,
                TraditionalIraDeduction = vm.TraditionalIraDeduction,
                EducatorExpenses = vm.EducatorExpenses,
                OtherAdjustments = vm.OtherAdjustments
            },
            Credits = new CreditsInput
            {
                NonrefundableCredits = Math.Max(0m, vm.NonrefundableCredits),
                RefundableCredits = Math.Max(0m, vm.RefundableCredits),
                PrecomputedChildTaxCredit = Math.Max(0m, vm.PrecomputedChildTaxCredit)
            },
            OtherTaxes = new OtherTaxesInput
            {
                NetInvestmentIncomeTax = Math.Max(0m, vm.NetInvestmentIncomeTax),
                OtherSchedule2Taxes = Math.Max(0m, vm.OtherSchedule2Taxes)
            },
            EstimatedTaxPayments = Math.Max(0m, vm.EstimatedTaxPayments)
        };
    }
}
