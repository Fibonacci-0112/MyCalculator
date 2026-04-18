using PaycheckCalc.App.Services;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps <see cref="AnnualTaxSession"/> credit fields between the UI and
/// the Core <see cref="CreditsInput"/> / <see cref="ChildTaxCreditInput"/> /
/// <see cref="EducationCreditsInput"/> / <see cref="SaversCreditInput"/> /
/// <see cref="NetInvestmentIncomeInput"/> types.
///
/// Each structured credit has an accompanying <c>UseStructured*</c>
/// toggle on the session. When false the legacy pre-computed lump-sums
/// are used instead, preserving the Core engine's additive semantics.
/// </summary>
public static class CreditsMapper
{
    /// <summary>
    /// Pulls the session's credit block into a session-agnostic snapshot
    /// used by scenario persistence and What-If cloning. The primary
    /// Form 1040 flow still goes through <see cref="AnnualTaxInputMapper"/>.
    /// </summary>
    public static void FromDomain(AnnualTaxSession s, CreditsInput credits, OtherTaxesInput otherTaxes)
    {
        s.NonrefundableCredits = credits.NonrefundableCredits;
        s.RefundableCredits = credits.RefundableCredits;
        s.PrecomputedChildTaxCredit = credits.PrecomputedChildTaxCredit;

        if (credits.ChildTaxCreditInput is { } ctc)
        {
            s.UseStructuredChildTaxCredit = true;
            s.CtcQualifyingChildren = ctc.QualifyingChildren;
            s.CtcOtherDependents = ctc.OtherDependents;
            s.CtcEarnedIncome = ctc.EarnedIncome;
        }
        else
        {
            s.UseStructuredChildTaxCredit = false;
            s.CtcQualifyingChildren = 0;
            s.CtcOtherDependents = 0;
            s.CtcEarnedIncome = 0m;
        }

        s.EducationStudents.Clear();
        if (credits.EducationCredits is { } edu)
        {
            s.UseStructuredEducationCredits = true;
            s.EducationModifiedAgiOverride = edu.ModifiedAgiOverride ?? 0m;
            foreach (var st in edu.Students)
            {
                s.EducationStudents.Add(new EducationStudentItemViewModel
                {
                    Name = st.Name,
                    QualifiedExpenses = st.QualifiedExpenses,
                    ClaimAmericanOpportunityCredit = st.ClaimAmericanOpportunityCredit,
                    ClaimLifetimeLearningCredit = st.ClaimLifetimeLearningCredit
                });
            }
        }
        else
        {
            s.UseStructuredEducationCredits = false;
            s.EducationModifiedAgiOverride = 0m;
        }

        if (credits.SaversCredit is { } sv)
        {
            s.UseStructuredSaversCredit = true;
            s.SaversTaxpayerContributions = sv.TaxpayerContributions;
            s.SaversSpouseContributions = sv.SpouseContributions;
        }
        else
        {
            s.UseStructuredSaversCredit = false;
            s.SaversTaxpayerContributions = 0m;
            s.SaversSpouseContributions = 0m;
        }

        s.NetInvestmentIncomeTax = otherTaxes.NetInvestmentIncomeTax;
        s.OtherSchedule2Taxes = otherTaxes.OtherSchedule2Taxes;
        if (otherTaxes.NetInvestmentIncome is { } niit)
        {
            s.UseStructuredNiit = true;
            s.NiitNetInvestmentIncome = niit.NetInvestmentIncome;
            s.NiitModifiedAgiOverride = niit.ModifiedAgiOverride ?? 0m;
        }
        else
        {
            s.UseStructuredNiit = false;
            s.NiitNetInvestmentIncome = 0m;
            s.NiitModifiedAgiOverride = 0m;
        }
    }
}
