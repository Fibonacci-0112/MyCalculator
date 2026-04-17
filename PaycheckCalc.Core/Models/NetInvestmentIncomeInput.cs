namespace PaycheckCalc.Core.Models;

/// <summary>
/// Structured input for <c>Form8960NiitCalculator</c> (Net Investment
/// Income Tax). Replaces the pre-computed lump-sum
/// <see cref="OtherTaxesInput.NetInvestmentIncomeTax"/> value with the two
/// figures the formula actually requires: net investment income and
/// Modified AGI.
/// </summary>
public sealed class NetInvestmentIncomeInput
{
    /// <summary>
    /// Net investment income for the year (taxable interest + ordinary and
    /// qualified dividends + net capital gains + annuity/royalty/passive-
    /// rental income − allowable investment expenses). The calculator does
    /// not attempt to re-derive this from <see cref="OtherIncomeInput"/>;
    /// it trusts the caller's Form 8960 line 8 figure.
    /// </summary>
    public decimal NetInvestmentIncome { get; init; }

    /// <summary>
    /// Modified AGI for Form 8960 (AGI + foreign earned income exclusion
    /// add-back). When unset (0), the calculator uses AGI.
    /// </summary>
    public decimal? ModifiedAgiOverride { get; init; }
}
