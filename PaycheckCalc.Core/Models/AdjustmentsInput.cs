namespace PaycheckCalc.Core.Models;

/// <summary>
/// Above-the-line adjustments to income, primarily Schedule 1 Part II.
/// These reduce gross income to arrive at AGI. Amounts are annual dollars.
///
/// NOTE: The deductible-half-of-SE-tax adjustment is NOT entered here; the
/// <c>Form1040Calculator</c> derives it from the Schedule SE calculation to
/// avoid double-entry.
/// </summary>
public sealed class AdjustmentsInput
{
    /// <summary>Schedule 1 line 20 — student loan interest deduction (max $2,500, MAGI phase-outs).</summary>
    public decimal StudentLoanInterest { get; init; }

    /// <summary>Schedule 1 line 13 — HSA deduction (excluding employer contributions).</summary>
    public decimal HsaDeduction { get; init; }

    /// <summary>Schedule 1 line 20 — educator expenses (max $300 per educator, $600 MFJ).</summary>
    public decimal EducatorExpenses { get; init; }

    /// <summary>Schedule 1 line 17 — self-employed health insurance deduction.</summary>
    public decimal SelfEmployedHealthInsurance { get; init; }

    /// <summary>
    /// Schedule 1 line 16 — self-employed SEP, SIMPLE, and qualified plans contribution.
    /// Distinct from traditional IRA contribution.
    /// </summary>
    public decimal SelfEmployedRetirement { get; init; }

    /// <summary>Schedule 1 line 20 — traditional IRA deduction.</summary>
    public decimal TraditionalIraDeduction { get; init; }

    /// <summary>Schedule 1 line 26 — any other above-the-line adjustment not captured above.</summary>
    public decimal OtherAdjustments { get; init; }
}
