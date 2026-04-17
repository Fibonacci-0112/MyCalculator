namespace PaycheckCalc.Core.Models;

/// <summary>
/// Structured input for <c>Form8880SaversCreditCalculator</c> (Saver's Credit
/// for qualified retirement savings contributions). The credit is a
/// nonrefundable percentage (50%, 20%, or 10%) of up to $2,000 of eligible
/// contributions ($4,000 MFJ), with the percentage tied to AGI bands.
/// </summary>
public sealed class SaversCreditInput
{
    /// <summary>
    /// Eligible retirement contributions for the primary taxpayer (traditional
    /// or Roth IRA, elective deferrals to 401(k)/403(b)/457/TSP/SIMPLE,
    /// voluntary after-tax contributions to a qualified plan, contributions
    /// to an ABLE account by the designated beneficiary). The Form 8880
    /// cap is $2,000 per person.
    /// </summary>
    public decimal TaxpayerContributions { get; init; }

    /// <summary>Eligible retirement contributions for the spouse (MFJ only).</summary>
    public decimal SpouseContributions { get; init; }
}
