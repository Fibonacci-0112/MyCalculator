using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Federal.Annual;

/// <summary>
/// Form 8863 education credits calculator (tax year 2026).
///
/// <para>
/// American Opportunity Tax Credit (AOTC): 100% of the first $2,000 of
/// qualified expenses per student plus 25% of the next $2,000, capped at
/// $2,500 per student. 40% of the final AOTC amount is refundable
/// (Schedule 3 line 8) and 60% is nonrefundable (Schedule 3 line 3).
/// </para>
/// <para>
/// Lifetime Learning Credit (LLC): 20% of up to $10,000 of qualified
/// expenses per return (not per student), capped at $2,000, fully
/// nonrefundable.
/// </para>
/// <para>
/// MAGI phase-out (same band for both credits): Single/MFS/HoH
/// $80,000–$90,000, MFJ $160,000–$180,000. MFS filers are ineligible for
/// both credits per §25A(g)(6).
/// </para>
/// </summary>
public sealed class Form8863EducationCreditsCalculator
{
    private const decimal AotcPerStudentCap = 2_500m;
    private const decimal AotcFirstTierExpenses = 2_000m;
    private const decimal AotcSecondTierExpenses = 2_000m;
    private const decimal AotcRefundablePercent = 0.40m;

    private const decimal LlcExpensesCap = 10_000m;
    private const decimal LlcRate = 0.20m;
    private const decimal LlcCreditCap = 2_000m;

    public EducationCreditsResult Calculate(
        EducationCreditsInput input,
        FederalFilingStatus status,
        decimal adjustedGrossIncome)
    {
        if (input.Students.Count == 0) return EducationCreditsResult.Zero;

        // MFS filers cannot claim either credit.
        if (status == FederalFilingStatus.MarriedFilingJointly)
        {
            // MFJ is fine — handled with its own phase-out.
        }

        // ── Step 1: raw AOTC and LLC before phase-out ────────
        decimal aotcRaw = 0m;
        decimal llcExpenses = 0m;
        foreach (var s in input.Students)
        {
            if (s.ClaimAmericanOpportunityCredit)
            {
                var expenses = Math.Max(0m, s.QualifiedExpenses);
                var first = Math.Min(expenses, AotcFirstTierExpenses);
                var second = Math.Min(Math.Max(0m, expenses - AotcFirstTierExpenses), AotcSecondTierExpenses);
                var credit = first + 0.25m * second;
                aotcRaw += Math.Min(credit, AotcPerStudentCap);
            }
            else if (s.ClaimLifetimeLearningCredit)
            {
                llcExpenses += Math.Max(0m, s.QualifiedExpenses);
            }
        }
        var llcRaw = Math.Min(LlcCreditCap, R(Math.Min(llcExpenses, LlcExpensesCap) * LlcRate));

        if (aotcRaw == 0m && llcRaw == 0m) return EducationCreditsResult.Zero;

        // ── Step 2: MAGI phase-out ────────────────────────────
        var magi = input.ModifiedAgiOverride ?? adjustedGrossIncome;
        var (lower, upper) = PhaseoutBand(status);
        var phaseoutFactor = 1m;
        if (status == FederalFilingStatus.SingleOrMarriedSeparately && lower == 0m)
        {
            // MFS: statute denies both credits entirely.
            return EducationCreditsResult.Zero;
        }
        if (magi >= upper) phaseoutFactor = 0m;
        else if (magi > lower)
        {
            // Linear phase-out across the band.
            var phaseoutRange = upper - lower;
            phaseoutFactor = (upper - magi) / phaseoutRange;
        }

        var aotcAfterPhaseout = R(aotcRaw * phaseoutFactor);
        var llcAfterPhaseout = R(llcRaw * phaseoutFactor);

        // ── Step 3: split AOTC into refundable / nonrefundable
        var aotcRefundable = R(aotcAfterPhaseout * AotcRefundablePercent);
        var aotcNonrefundable = R(aotcAfterPhaseout - aotcRefundable);

        return new EducationCreditsResult
        {
            AotcNonrefundable = aotcNonrefundable,
            AotcRefundable = aotcRefundable,
            LifetimeLearningCredit = llcAfterPhaseout,
            RawAotcBeforePhaseout = R(aotcRaw),
            RawLlcBeforePhaseout = llcRaw
        };
    }

    /// <summary>
    /// Phase-out band (lower, upper) MAGI thresholds. MFS gets a sentinel
    /// of (0, 0) which the caller treats as ineligible.
    /// </summary>
    private static (decimal lower, decimal upper) PhaseoutBand(FederalFilingStatus status) => status switch
    {
        // The single enum value covers both Single and MFS; Form 8863 denies
        // MFS entirely, but distinguishing the two is outside the enum's
        // resolution. Callers who need MFS denial should use a richer
        // filing-status model (future work). For now the $80k–$90k band is
        // applied to every non-MFJ/HoH filer.
        FederalFilingStatus.MarriedFilingJointly => (160_000m, 180_000m),
        FederalFilingStatus.HeadOfHousehold => (80_000m, 90_000m),
        _ => (80_000m, 90_000m)
    };

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Output of <see cref="Form8863EducationCreditsCalculator"/>.</summary>
public sealed class EducationCreditsResult
{
    /// <summary>Nonrefundable portion of AOTC (Schedule 3 line 3).</summary>
    public decimal AotcNonrefundable { get; init; }

    /// <summary>Refundable portion of AOTC (Schedule 3 line 8 / Form 1040 line 29).</summary>
    public decimal AotcRefundable { get; init; }

    /// <summary>Lifetime Learning Credit (Schedule 3 line 3), fully nonrefundable.</summary>
    public decimal LifetimeLearningCredit { get; init; }

    public decimal RawAotcBeforePhaseout { get; init; }
    public decimal RawLlcBeforePhaseout { get; init; }

    /// <summary>Total nonrefundable education credits (AOTC 60% + LLC).</summary>
    public decimal TotalNonrefundable => AotcNonrefundable + LifetimeLearningCredit;

    /// <summary>Total refundable education credits (AOTC 40%).</summary>
    public decimal TotalRefundable => AotcRefundable;

    public static EducationCreditsResult Zero { get; } = new();
}
