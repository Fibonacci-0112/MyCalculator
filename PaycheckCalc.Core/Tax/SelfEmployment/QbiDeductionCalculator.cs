using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.Core.Tax.SelfEmployment;

/// <summary>
/// Computes the Qualified Business Income (QBI) deduction under IRC § 199A.
/// Implements both the simplified Form 8995 and the full Form 8995-A logic
/// with SSTB phase-out and W-2/UBIA limitations.
/// </summary>
public sealed class QbiDeductionCalculator
{
    /// <summary>QBI deduction rate: 20% of qualified business income.</summary>
    public const decimal QbiRate = 0.20m;

    // ── 2026 projected thresholds (indexed from 2025 values) ────
    // These thresholds determine when the full 8995-A rules apply.

    /// <summary>Taxable income threshold for Single/MFS/HoH filers (2026).</summary>
    public const decimal SingleThreshold = 200_000m;

    /// <summary>Taxable income threshold for MFJ filers (2026).</summary>
    public const decimal MfjThreshold = 400_000m;

    /// <summary>Phase-in range for Single/MFS/HoH filers.</summary>
    public const decimal SinglePhaseInRange = 50_000m;

    /// <summary>Phase-in range for MFJ filers.</summary>
    public const decimal MfjPhaseInRange = 100_000m;

    /// <summary>
    /// Calculates the QBI deduction.
    /// </summary>
    /// <param name="qualifiedBusinessIncome">
    /// The qualified business income (generally Schedule C net profit).
    /// Negative or zero QBI produces a zero deduction.
    /// </param>
    /// <param name="taxableIncomeBeforeQbi">
    /// Taxable income before subtracting the QBI deduction.
    /// Used to cap the deduction and determine phase-out applicability.
    /// </param>
    /// <param name="filingStatus">Federal filing status for threshold selection.</param>
    /// <param name="isSstb">True if the business is a Specified Service Trade or Business.</param>
    /// <param name="w2Wages">W-2 wages paid by the qualified business.</param>
    /// <param name="ubia">Unadjusted basis immediately after acquisition of qualified property.</param>
    public decimal Calculate(
        decimal qualifiedBusinessIncome,
        decimal taxableIncomeBeforeQbi,
        FederalFilingStatus filingStatus,
        bool isSstb = false,
        decimal w2Wages = 0m,
        decimal ubia = 0m)
    {
        if (qualifiedBusinessIncome <= 0m || taxableIncomeBeforeQbi <= 0m)
            return 0m;

        var threshold = GetThreshold(filingStatus);
        var phaseInRange = GetPhaseInRange(filingStatus);

        // ── Simplified path (Form 8995): below the threshold ────
        if (taxableIncomeBeforeQbi <= threshold)
        {
            var simple = Math.Min(
                R(qualifiedBusinessIncome * QbiRate),
                R(taxableIncomeBeforeQbi * QbiRate));
            return R(simple);
        }

        // ── Full path (Form 8995-A): above the threshold ────────
        var excessOverThreshold = taxableIncomeBeforeQbi - threshold;

        // Phase-out ratio: 0 at threshold → 1 at (threshold + range)
        var phaseOutRatio = Math.Min(1m, excessOverThreshold / phaseInRange);

        // ── SSTB complete phase-out ─────────────────────────────
        // If SSTB and fully phased out, deduction is zero
        if (isSstb && phaseOutRatio >= 1m)
            return 0m;

        // Applicable QBI and W-2/UBIA for SSTBs are reduced by (1 − phaseOutRatio)
        var applicableQbi = isSstb
            ? R(qualifiedBusinessIncome * (1m - phaseOutRatio))
            : qualifiedBusinessIncome;

        var applicableW2 = isSstb
            ? R(w2Wages * (1m - phaseOutRatio))
            : w2Wages;

        var applicableUbia = isSstb
            ? R(ubia * (1m - phaseOutRatio))
            : ubia;

        // 20% of applicable QBI
        var twentyPercentQbi = R(applicableQbi * QbiRate);

        // W-2/UBIA limitation: greater of (50% of W-2 wages) or (25% of W-2 wages + 2.5% of UBIA)
        var w2Limit = Math.Max(
            R(applicableW2 * 0.50m),
            R(applicableW2 * 0.25m) + R(applicableUbia * 0.025m));

        // For non-SSTB above threshold, apply the W-2/UBIA limitation with phase-in
        // The reduction amount phases in over the phase-in range
        decimal qbiComponent;
        if (phaseOutRatio >= 1m)
        {
            // Fully above phase-in: apply full W-2/UBIA limitation
            qbiComponent = Math.Min(twentyPercentQbi, w2Limit);
        }
        else
        {
            // Within phase-in range: blend between unlimited and limited
            var reductionAmount = Math.Max(0m, twentyPercentQbi - w2Limit);
            var phasedReduction = R(reductionAmount * phaseOutRatio);
            qbiComponent = R(twentyPercentQbi - phasedReduction);
        }

        // Final cap: lesser of QBI component or 20% of taxable income
        var taxableIncomeCap = R(taxableIncomeBeforeQbi * QbiRate);
        return R(Math.Min(qbiComponent, taxableIncomeCap));
    }

    private static decimal GetThreshold(FederalFilingStatus status) =>
        status == FederalFilingStatus.MarriedFilingJointly ? MfjThreshold : SingleThreshold;

    private static decimal GetPhaseInRange(FederalFilingStatus status) =>
        status == FederalFilingStatus.MarriedFilingJointly ? MfjPhaseInRange : SinglePhaseInRange;

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
