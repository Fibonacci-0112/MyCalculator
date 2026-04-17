using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Form 8880 Saver's Credit (Credit for Qualified Retirement Savings
/// Contributions) for tax year 2026. The credit is a fully nonrefundable
/// percentage (50%, 20%, 10%, or 0%) of up to $2,000 of eligible
/// contributions per taxpayer ($4,000 MFJ when both spouses contribute),
/// with the percentage selected from AGI bands.
///
/// <para>
/// 2026 AGI thresholds (Rev. Proc. 2025-32, Saver's Credit schedule):
/// </para>
/// <para>Single / MFS: 50% ≤ $25,000 | 20% ≤ $27,250 | 10% ≤ $42,000 | 0% above</para>
/// <para>HoH:          50% ≤ $37,500 | 20% ≤ $40,875 | 10% ≤ $63,000 | 0% above</para>
/// <para>MFJ:          50% ≤ $50,000 | 20% ≤ $54,500 | 10% ≤ $84,000 | 0% above</para>
/// </summary>
public sealed class Form8880SaversCreditCalculator
{
    private const decimal PerTaxpayerContributionCap = 2_000m;

    public SaversCreditResult Calculate(
        SaversCreditInput input,
        FederalFilingStatus status,
        decimal adjustedGrossIncome)
    {
        var taxpayer = Math.Min(PerTaxpayerContributionCap, Math.Max(0m, input.TaxpayerContributions));
        var spouse = status == FederalFilingStatus.MarriedFilingJointly
            ? Math.Min(PerTaxpayerContributionCap, Math.Max(0m, input.SpouseContributions))
            : 0m;
        var eligibleContributions = taxpayer + spouse;
        if (eligibleContributions <= 0m) return SaversCreditResult.Zero;

        var rate = GetRate(status, adjustedGrossIncome);
        if (rate <= 0m) return SaversCreditResult.Zero;

        var credit = R(eligibleContributions * rate);
        return new SaversCreditResult
        {
            Credit = credit,
            Rate = rate,
            EligibleContributions = eligibleContributions
        };
    }

    private static decimal GetRate(FederalFilingStatus status, decimal agi)
    {
        switch (status)
        {
            case FederalFilingStatus.MarriedFilingJointly:
                if (agi <= 50_000m) return 0.50m;
                if (agi <= 54_500m) return 0.20m;
                if (agi <= 84_000m) return 0.10m;
                return 0m;
            case FederalFilingStatus.HeadOfHousehold:
                if (agi <= 37_500m) return 0.50m;
                if (agi <= 40_875m) return 0.20m;
                if (agi <= 63_000m) return 0.10m;
                return 0m;
            default: // Single / MFS
                if (agi <= 25_000m) return 0.50m;
                if (agi <= 27_250m) return 0.20m;
                if (agi <= 42_000m) return 0.10m;
                return 0m;
        }
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Output of <see cref="Form8880SaversCreditCalculator"/>.</summary>
public sealed class SaversCreditResult
{
    /// <summary>Final nonrefundable Saver's Credit amount.</summary>
    public decimal Credit { get; init; }

    /// <summary>Rate applied (0.50, 0.20, 0.10, or 0).</summary>
    public decimal Rate { get; init; }

    /// <summary>Eligible contributions used (capped at $2,000 per taxpayer).</summary>
    public decimal EligibleContributions { get; init; }

    public static SaversCreditResult Zero { get; } = new();
}
