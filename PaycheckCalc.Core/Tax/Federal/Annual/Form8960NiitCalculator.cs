using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Form 8960 Net Investment Income Tax calculator. NIIT is a flat 3.8%
/// additional tax on the lesser of (a) net investment income or
/// (b) MAGI in excess of a filing-status threshold. The thresholds are
/// statutory and not adjusted for inflation (§1411(b)):
/// <para>Single / MFS-separate-return / HoH: $200,000</para>
/// <para>MFJ and qualifying surviving spouse: $250,000</para>
/// <para>MFS filing separately:               $125,000</para>
/// </summary>
public sealed class Form8960NiitCalculator
{
    public const decimal Rate = 0.038m;

    /// <summary>
    /// Statutory MAGI threshold. The <see cref="FederalFilingStatus"/> enum
    /// folds Single and MFS together, so the $125,000 MFS threshold cannot
    /// be exactly expressed with the current enum; this calculator uses
    /// $200,000 for the combined bucket, which matches Single behavior and
    /// is the conservative (larger) threshold for MFS. Callers who must
    /// model MFS precisely can compute NIIT externally and pass the result
    /// through <see cref="OtherTaxesInput.NetInvestmentIncomeTax"/>.
    /// </summary>
    public static decimal Threshold(FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => 250_000m,
        _ => 200_000m
    };

    public decimal Calculate(
        NetInvestmentIncomeInput input,
        FederalFilingStatus status,
        decimal adjustedGrossIncome)
    {
        var nii = Math.Max(0m, input.NetInvestmentIncome);
        if (nii <= 0m) return 0m;

        var magi = input.ModifiedAgiOverride ?? adjustedGrossIncome;
        var excess = Math.Max(0m, magi - Threshold(status));
        if (excess <= 0m) return 0m;

        var base_ = Math.Min(nii, excess);
        return Math.Round(base_ * Rate, 2, MidpointRounding.AwayFromZero);
    }
}
