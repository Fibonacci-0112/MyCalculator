namespace PaycheckCalc.Core.Models;

/// <summary>
/// A single student claimed on Form 8863. Exactly one of
/// <see cref="ClaimAmericanOpportunityCredit"/> (AOTC) or
/// <see cref="ClaimLifetimeLearningCredit"/> (LLC) may be true per student
/// per year. When both flags are set the calculator treats AOTC as winning
/// because it is the larger credit for most taxpayers.
/// </summary>
public sealed class EducationStudentInput
{
    /// <summary>Optional human-readable label (e.g. student name).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Qualified education expenses paid for this student.</summary>
    public decimal QualifiedExpenses { get; init; }

    /// <summary>
    /// True to claim the American Opportunity Tax Credit (first four years
    /// of post-secondary education, 40% refundable). Student must also be
    /// AOTC-eligible per Form 8863 rules (half-time enrollment, degree
    /// program, no prior felony drug conviction, etc.) — the calculator
    /// trusts the caller on these binary eligibility questions.
    /// </summary>
    public bool ClaimAmericanOpportunityCredit { get; init; }

    /// <summary>True to claim the Lifetime Learning Credit instead.</summary>
    public bool ClaimLifetimeLearningCredit { get; init; }
}

/// <summary>
/// Structured input for <c>Form8863EducationCreditsCalculator</c>. Combines
/// per-student AOTC claims with a single household LLC pool (Form 8863 only
/// allows one LLC amount per return regardless of student count, capped at
/// $10,000 of qualified expenses).
/// </summary>
public sealed class EducationCreditsInput
{
    /// <summary>Per-student entries.</summary>
    public IReadOnlyList<EducationStudentInput> Students { get; init; } =
        Array.Empty<EducationStudentInput>();

    /// <summary>
    /// Modified AGI for Form 8863 phase-out. When unset (0), the calculator
    /// falls back to AGI supplied by the engine. Callers needing to include
    /// add-backs (foreign earned income exclusion, etc.) can provide this
    /// explicitly.
    /// </summary>
    public decimal? ModifiedAgiOverride { get; init; }
}
