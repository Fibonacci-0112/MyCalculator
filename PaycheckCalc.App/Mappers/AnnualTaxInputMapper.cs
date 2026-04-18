using PaycheckCalc.App.Services;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps <see cref="AnnualTaxSession"/> state to a Core
/// <see cref="TaxYearProfile"/> ready for <c>Form1040Calculator</c>.
/// Phase 8 moved the source of truth from the old monolithic
/// <c>AnnualTaxViewModel</c> onto the shared session so every annual
/// flyout page contributes to the same profile.
/// </summary>
public static class AnnualTaxInputMapper
{
    public static TaxYearProfile Map(AnnualTaxSession s)
    {
        var jobs = JobsAndYtdMapper.ToDomain(s.W2Jobs);

        var ctc = s.UseStructuredChildTaxCredit
            ? new ChildTaxCreditInput
            {
                QualifyingChildren = Math.Max(0, s.CtcQualifyingChildren),
                OtherDependents = Math.Max(0, s.CtcOtherDependents),
                EarnedIncome = Math.Max(0m, s.CtcEarnedIncome)
            }
            : null;

        var education = s.UseStructuredEducationCredits
            ? new EducationCreditsInput
            {
                Students = s.EducationStudents
                    .Select(st => new EducationStudentInput
                    {
                        Name = st.Name,
                        QualifiedExpenses = Math.Max(0m, st.QualifiedExpenses),
                        ClaimAmericanOpportunityCredit = st.ClaimAmericanOpportunityCredit,
                        ClaimLifetimeLearningCredit = st.ClaimLifetimeLearningCredit
                    })
                    .ToList(),
                ModifiedAgiOverride = s.EducationModifiedAgiOverride > 0m
                    ? s.EducationModifiedAgiOverride
                    : null
            }
            : null;

        var savers = s.UseStructuredSaversCredit
            ? new SaversCreditInput
            {
                TaxpayerContributions = Math.Max(0m, s.SaversTaxpayerContributions),
                SpouseContributions = Math.Max(0m, s.SaversSpouseContributions)
            }
            : null;

        var niit = s.UseStructuredNiit
            ? new NetInvestmentIncomeInput
            {
                NetInvestmentIncome = Math.Max(0m, s.NiitNetInvestmentIncome),
                ModifiedAgiOverride = s.NiitModifiedAgiOverride > 0m
                    ? s.NiitModifiedAgiOverride
                    : null
            }
            : null;

        var priorYear = s.UsePriorYearSafeHarbor
            ? new PriorYearSafeHarborInput
            {
                PriorYearTotalTax = Math.Max(0m, s.PriorYearTotalTax),
                PriorYearAdjustedGrossIncome = Math.Max(0m, s.PriorYearAdjustedGrossIncome),
                PriorYearWasFullYear = s.PriorYearWasFullYear
            }
            : null;

        return new TaxYearProfile
        {
            TaxYear = s.TaxYear,
            FilingStatus = s.FilingStatus,
            QualifyingChildren = Math.Max(0, s.QualifyingChildren),
            ResidenceState = s.SelectedState,
            W2Jobs = jobs,
            ItemizedDeductionsOverStandard = Math.Max(0m, s.ItemizedDeductionsOverStandard),
            OtherIncome = OtherIncomeAdjustmentsMapper.ToOtherIncome(s),
            Adjustments = OtherIncomeAdjustmentsMapper.ToAdjustments(s),
            Credits = new CreditsInput
            {
                NonrefundableCredits = Math.Max(0m, s.NonrefundableCredits),
                RefundableCredits = Math.Max(0m, s.RefundableCredits),
                PrecomputedChildTaxCredit = Math.Max(0m, s.PrecomputedChildTaxCredit),
                ChildTaxCreditInput = ctc,
                EducationCredits = education,
                SaversCredit = savers
            },
            OtherTaxes = new OtherTaxesInput
            {
                NetInvestmentIncomeTax = Math.Max(0m, s.NetInvestmentIncomeTax),
                OtherSchedule2Taxes = Math.Max(0m, s.OtherSchedule2Taxes),
                NetInvestmentIncome = niit
            },
            EstimatedTaxPayments = Math.Max(0m, s.EstimatedTaxPayments),
            AdditionalExpectedWithholding = Math.Max(0m, s.AdditionalExpectedWithholding),
            PriorYearSafeHarbor = priorYear
        };
    }
}
