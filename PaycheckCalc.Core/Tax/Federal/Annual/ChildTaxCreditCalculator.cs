using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Child Tax Credit + Credit for Other Dependents (CTC/ACTC/ODC) calculator
/// for tax year 2026. Per the One Big Beautiful Bill Act (OBBBA) the
/// nonrefundable CTC is permanently $2,200 per qualifying child and the
/// refundable Additional Child Tax Credit (ACTC) is capped at $1,700 per
/// qualifying child. The ODC remains $500 per other dependent, fully
/// nonrefundable.
///
/// <para>
/// AGI phase-out: the total CTC + ODC is reduced by $50 for every $1,000
/// (or fraction thereof) of AGI above the threshold — $200,000 Single/MFS/HoH,
/// $400,000 MFJ. Phase-out amounts round the excess up to the next $1,000.
/// </para>
/// <para>
/// ACTC earned-income formula: refundable amount is limited to
/// 15% × (EarnedIncome − $2,500), capped at $1,700 × qualifying children and
/// capped again at the remainder of the nonrefundable credit that would be
/// lost because tax ran out first.
/// </para>
/// </summary>
public sealed class ChildTaxCreditCalculator
{
    private const decimal NonrefundablePerChild = 2_200m;
    private const decimal RefundablePerChildCap = 1_700m;
    private const decimal OtherDependentCredit = 500m;
    private const decimal PhaseoutStep = 1_000m;
    private const decimal PhaseoutRate = 50m;         // $50 per $1,000
    private const decimal ActcEarnedIncomeFloor = 2_500m;
    private const decimal ActcRate = 0.15m;

    /// <summary>AGI phase-out threshold for the given filing status.</summary>
    public static decimal PhaseoutThreshold(FederalFilingStatus status) => status switch
    {
        FederalFilingStatus.MarriedFilingJointly => 400_000m,
        _ => 200_000m
    };

    public ChildTaxCreditResult Calculate(
        ChildTaxCreditInput input,
        FederalFilingStatus status,
        decimal adjustedGrossIncome,
        decimal taxBeforeCtc)
    {
        var qc = Math.Max(0, input.QualifyingChildren);
        var od = Math.Max(0, input.OtherDependents);
        if (qc == 0 && od == 0) return ChildTaxCreditResult.Zero;

        // ── Step 1: tentative credit before phase-out ────────
        var ctcBeforePhaseout = NonrefundablePerChild * qc;
        var odcBeforePhaseout = OtherDependentCredit * od;
        var totalBeforePhaseout = ctcBeforePhaseout + odcBeforePhaseout;

        // ── Step 2: AGI phase-out ────────────────────────────
        var threshold = PhaseoutThreshold(status);
        decimal phaseoutReduction = 0m;
        if (adjustedGrossIncome > threshold)
        {
            // Round excess up to the next $1,000 (§24(b)(2)).
            var excess = adjustedGrossIncome - threshold;
            var steps = Math.Ceiling(excess / PhaseoutStep);
            phaseoutReduction = steps * PhaseoutRate;
        }

        var totalAfterPhaseout = Math.Max(0m, totalBeforePhaseout - phaseoutReduction);

        // ── Step 3: allocate phase-out between CTC and ODC ───
        // The statute reduces the CTC portion first (Form 8812 mechanics).
        var ctcAfterPhaseout = Math.Min(ctcBeforePhaseout, totalAfterPhaseout);
        var odcAfterPhaseout = Math.Max(0m, totalAfterPhaseout - ctcAfterPhaseout);

        // ── Step 4: nonrefundable portion limited by tax ─────
        var nonrefundableApplied = Math.Min(taxBeforeCtc, totalAfterPhaseout);

        // ── Step 5: refundable ACTC ──────────────────────────
        // Only the CTC portion (not ODC) is refundable.
        decimal refundable = 0m;
        if (qc > 0 && ctcAfterPhaseout > 0m)
        {
            var earnedOver = Math.Max(0m, input.EarnedIncome - ActcEarnedIncomeFloor);
            var earnedLimit = R(earnedOver * ActcRate);
            var perChildCap = RefundablePerChildCap * qc;
            // The refundable portion can only recover the CTC that couldn't
            // be used nonrefundably, i.e. the unused CTC portion.
            var ctcUsedNonrefundably = Math.Min(ctcAfterPhaseout, nonrefundableApplied);
            var ctcUnused = ctcAfterPhaseout - ctcUsedNonrefundably;
            refundable = Math.Min(Math.Min(earnedLimit, perChildCap), ctcUnused);
        }

        return new ChildTaxCreditResult
        {
            NonrefundableApplied = R(nonrefundableApplied),
            RefundableActc = R(refundable),
            CtcBeforePhaseout = R(ctcBeforePhaseout),
            OdcBeforePhaseout = R(odcBeforePhaseout),
            PhaseoutReduction = R(phaseoutReduction)
        };
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Output of <see cref="ChildTaxCreditCalculator"/>.</summary>
public sealed class ChildTaxCreditResult
{
    /// <summary>Nonrefundable CTC + ODC amount actually applied against tax.</summary>
    public decimal NonrefundableApplied { get; init; }

    /// <summary>Refundable Additional Child Tax Credit amount (paid out as a payment).</summary>
    public decimal RefundableActc { get; init; }

    /// <summary>Tentative CTC (qualifying-children × $2,200) before phase-out.</summary>
    public decimal CtcBeforePhaseout { get; init; }

    /// <summary>Tentative ODC (other-dependents × $500) before phase-out.</summary>
    public decimal OdcBeforePhaseout { get; init; }

    /// <summary>AGI phase-out reduction applied.</summary>
    public decimal PhaseoutReduction { get; init; }

    public static ChildTaxCreditResult Zero { get; } = new();
}
