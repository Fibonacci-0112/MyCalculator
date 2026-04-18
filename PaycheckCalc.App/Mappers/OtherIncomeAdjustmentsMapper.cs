using PaycheckCalc.App.Services;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps <see cref="AnnualTaxSession"/> Schedule 1 inputs between the UI
/// and the Core <see cref="OtherIncomeInput"/> / <see cref="AdjustmentsInput"/>
/// types. Both directions are provided so saved scenarios rehydrate the session.
/// </summary>
public static class OtherIncomeAdjustmentsMapper
{
    public static OtherIncomeInput ToOtherIncome(AnnualTaxSession s) => new()
    {
        TaxableInterest = Math.Max(0m, s.TaxableInterest),
        OrdinaryDividends = Math.Max(0m, s.OrdinaryDividends),
        QualifiedDividends = Math.Max(0m, s.QualifiedDividends),
        CapitalGainOrLoss = s.CapitalGainOrLoss,       // may be negative
        UnemploymentCompensation = Math.Max(0m, s.UnemploymentCompensation),
        TaxableStateLocalRefunds = Math.Max(0m, s.TaxableStateLocalRefunds),
        TaxableSocialSecurity = Math.Max(0m, s.TaxableSocialSecurity),
        OtherAdditionalIncome = s.OtherAdditionalIncome
    };

    public static AdjustmentsInput ToAdjustments(AnnualTaxSession s) => new()
    {
        StudentLoanInterest = Math.Max(0m, s.StudentLoanInterest),
        HsaDeduction = Math.Max(0m, s.HsaDeduction),
        EducatorExpenses = Math.Max(0m, s.EducatorExpenses),
        SelfEmployedHealthInsurance = Math.Max(0m, s.SelfEmployedHealthInsurance),
        SelfEmployedRetirement = Math.Max(0m, s.SelfEmployedRetirement),
        TraditionalIraDeduction = Math.Max(0m, s.TraditionalIraDeduction),
        OtherAdjustments = s.OtherAdjustments
    };

    public static void FromDomain(
        AnnualTaxSession s,
        OtherIncomeInput income,
        AdjustmentsInput adjustments)
    {
        s.TaxableInterest = income.TaxableInterest;
        s.OrdinaryDividends = income.OrdinaryDividends;
        s.QualifiedDividends = income.QualifiedDividends;
        s.CapitalGainOrLoss = income.CapitalGainOrLoss;
        s.UnemploymentCompensation = income.UnemploymentCompensation;
        s.TaxableStateLocalRefunds = income.TaxableStateLocalRefunds;
        s.TaxableSocialSecurity = income.TaxableSocialSecurity;
        s.OtherAdditionalIncome = income.OtherAdditionalIncome;

        s.StudentLoanInterest = adjustments.StudentLoanInterest;
        s.HsaDeduction = adjustments.HsaDeduction;
        s.EducatorExpenses = adjustments.EducatorExpenses;
        s.SelfEmployedHealthInsurance = adjustments.SelfEmployedHealthInsurance;
        s.SelfEmployedRetirement = adjustments.SelfEmployedRetirement;
        s.TraditionalIraDeduction = adjustments.TraditionalIraDeduction;
        s.OtherAdjustments = adjustments.OtherAdjustments;
    }
}
