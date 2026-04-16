using PaycheckCalc.Core.Tax.Fica;

namespace PaycheckCalc.Core.Tax.SelfEmployment;

/// <summary>
/// Computes self-employment tax (Schedule SE equivalent).
/// Self-employed individuals pay both the employer and employee shares
/// of Social Security and Medicare taxes.
/// </summary>
public sealed class SelfEmploymentTaxCalculator
{
    /// <summary>
    /// IRS factor: only 92.35% of net SE earnings are subject to SE tax.
    /// This accounts for the fact that employees only pay FICA on wages
    /// (not on the employer's share of FICA itself).
    /// </summary>
    public const decimal SelfEmploymentTaxableRate = 0.9235m;

    /// <summary>Combined employer + employee Social Security rate (6.2% × 2).</summary>
    public const decimal CombinedSocialSecurityRate = FicaCalculator.SocialSecurityRate * 2; // 12.4%

    /// <summary>Combined employer + employee Medicare rate (1.45% × 2).</summary>
    public const decimal CombinedMedicareRate = FicaCalculator.MedicareRate * 2; // 2.9%

    /// <summary>
    /// Additional Medicare tax rate (employee-only, 0.9%) on earnings
    /// above the threshold. Self-employed individuals pay only the
    /// employee share — the employer share does not apply.
    /// </summary>
    public const decimal AdditionalMedicareRate = FicaCalculator.AdditionalMedicareRate; // 0.9%

    private readonly decimal _socialSecurityWageBase;
    private readonly decimal _additionalMedicareThreshold;

    public SelfEmploymentTaxCalculator(FicaCalculator fica)
    {
        _socialSecurityWageBase = fica.SocialSecurityWageBase;
        _additionalMedicareThreshold = fica.AdditionalMedicareEmployerThreshold;
    }

    /// <summary>
    /// Calculates self-employment tax from net self-employment earnings.
    /// </summary>
    /// <param name="netSelfEmploymentEarnings">
    /// Schedule C net profit (Line 31). Negative values produce zero tax.
    /// </param>
    /// <param name="w2SocialSecurityWages">
    /// W-2 Social Security wages (Box 3) already subject to employer
    /// withholding. Reduces the remaining SS wage base available for SE tax.
    /// Per Schedule SE Section B, lines 8a–11.
    /// </param>
    /// <param name="w2MedicareWages">
    /// W-2 Medicare wages (Box 5) already subject to employer withholding.
    /// Shifts the Additional Medicare Tax threshold so the 0.9% surtax
    /// applies to combined wages + SE earnings above the filing-status
    /// threshold. Per Schedule SE Section B, lines 14–18.
    /// </param>
    public SelfEmploymentTaxResult Calculate(
        decimal netSelfEmploymentEarnings,
        decimal w2SocialSecurityWages = 0m,
        decimal w2MedicareWages = 0m)
    {
        if (netSelfEmploymentEarnings <= 0m)
            return SelfEmploymentTaxResult.Zero;

        // Step 1: 92.35% of net earnings (Schedule SE line 4a)
        var seTaxable = R(netSelfEmploymentEarnings * SelfEmploymentTaxableRate);

        // Step 2: Social Security — capped at the remaining wage base
        // after subtracting W-2 SS wages (Schedule SE lines 8a–11)
        var remainingSsBase = Math.Max(0m, _socialSecurityWageBase - w2SocialSecurityWages);
        var ssTaxable = Math.Min(seTaxable, remainingSsBase);
        var ssTax = R(ssTaxable * CombinedSocialSecurityRate);

        // Step 3: Medicare — on all SE taxable earnings (no cap)
        var medicareTax = R(seTaxable * CombinedMedicareRate);

        // Step 4: Additional Medicare — only on SE earnings above the
        // threshold reduced by W-2 Medicare wages (Schedule SE lines 14–18).
        // The threshold for the employee-only 0.9% Additional Medicare Tax
        // is effectively: max(0, threshold − W-2 Medicare wages).
        var reducedThreshold = Math.Max(0m, _additionalMedicareThreshold - w2MedicareWages);
        var additionalMedicare = R(Math.Max(0m, seTaxable - reducedThreshold) * AdditionalMedicareRate);

        // Step 5: Total SE tax
        var totalSeTax = R(ssTax + medicareTax + additionalMedicare);

        // Step 6: Deductible half (above-the-line deduction)
        var deductibleHalf = R(totalSeTax * 0.5m);

        return new SelfEmploymentTaxResult
        {
            SeTaxableEarnings = seTaxable,
            SocialSecurityTax = ssTax,
            MedicareTax = medicareTax,
            AdditionalMedicareTax = additionalMedicare,
            TotalSeTax = totalSeTax,
            DeductibleHalfOfSeTax = deductibleHalf
        };
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Itemized result of the self-employment tax calculation.
/// </summary>
public sealed class SelfEmploymentTaxResult
{
    public static readonly SelfEmploymentTaxResult Zero = new();

    public decimal SeTaxableEarnings { get; init; }
    public decimal SocialSecurityTax { get; init; }
    public decimal MedicareTax { get; init; }
    public decimal AdditionalMedicareTax { get; init; }
    public decimal TotalSeTax { get; init; }
    public decimal DeductibleHalfOfSeTax { get; init; }
}
