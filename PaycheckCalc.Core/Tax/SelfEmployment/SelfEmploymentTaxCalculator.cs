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
    public SelfEmploymentTaxResult Calculate(decimal netSelfEmploymentEarnings)
    {
        if (netSelfEmploymentEarnings <= 0m)
            return SelfEmploymentTaxResult.Zero;

        // Step 1: 92.35% of net earnings
        var seTaxable = R(netSelfEmploymentEarnings * SelfEmploymentTaxableRate);

        // Step 2: Social Security — capped at the wage base
        var ssTaxable = Math.Min(seTaxable, _socialSecurityWageBase);
        var ssTax = R(ssTaxable * CombinedSocialSecurityRate);

        // Step 3: Medicare — on all SE taxable earnings
        var medicareTax = R(seTaxable * CombinedMedicareRate);

        // Step 4: Additional Medicare — only on earnings above threshold
        var additionalMedicare = R(Math.Max(0m, seTaxable - _additionalMedicareThreshold) * AdditionalMedicareRate);

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
