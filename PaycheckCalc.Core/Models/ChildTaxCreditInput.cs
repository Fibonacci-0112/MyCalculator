namespace PaycheckCalc.Core.Models;

/// <summary>
/// Structured input for the Child Tax Credit / Credit for Other Dependents
/// computation used by <c>ChildTaxCreditCalculator</c>. The 2026 OBBBA rules
/// split the credit into a $2,200-per-qualifying-child nonrefundable portion
/// and a $1,700-per-qualifying-child refundable Additional Child Tax Credit,
/// both phased out above $200,000 AGI ($400,000 MFJ).
///
/// <para>
/// When the caller supplies this input on <see cref="TaxYearProfile"/>, the
/// annual engine computes CTC from it. The legacy
/// <see cref="CreditsInput.ChildTaxCredit"/> field remains supported as an
/// additive override for callers that still pass in a pre-computed amount.
/// </para>
/// </summary>
public sealed class ChildTaxCreditInput
{
    /// <summary>Number of qualifying children under age 17 at year-end.</summary>
    public int QualifyingChildren { get; init; }

    /// <summary>
    /// Number of other dependents who qualify for the $500 Credit for Other
    /// Dependents (ODC). ODC is fully nonrefundable.
    /// </summary>
    public int OtherDependents { get; init; }

    /// <summary>
    /// Earned income for the year, used by the refundable ACTC 15%-of-earned-
    /// income-over-$2,500 limit. Callers typically pass W-2 wages + net SE
    /// earnings; when omitted, the ACTC is not allowed (fail-safe toward $0).
    /// </summary>
    public decimal EarnedIncome { get; init; }
}
