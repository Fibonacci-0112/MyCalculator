namespace PaycheckCalc.Core.Models;

/// <summary>
/// Identifies which spouse owns a given W-2 job on a joint return.
/// For non-joint returns the value is ignored. Used by the annual
/// <c>Form1040Calculator</c> to compute the excess Social Security tax
/// credit (Schedule 3 line 11) on a per-taxpayer basis — the IRS applies
/// the excess test once per person, not once per return.
/// </summary>
public enum W2JobHolder
{
    /// <summary>Primary taxpayer (default for single filers and MFS).</summary>
    Taxpayer,
    /// <summary>Spouse (meaningful only when filing jointly).</summary>
    Spouse
}

/// <summary>
/// Annualized W-2 job inputs used by the annual Form 1040 engine.
/// A taxpayer may have one or more W-2 jobs aggregated into a single return.
/// Each job contributes wages, federal withholding, and FICA-tracking fields.
/// Per-paycheck calculations remain the responsibility of <see cref="PaycheckInput"/>;
/// this model is the year-level rollup used by <c>Form1040Calculator</c>.
/// </summary>
public sealed class W2JobInput
{
    /// <summary>Optional human-readable label (e.g. "Day job", "Employer A").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Which spouse this job belongs to. Only meaningful on a joint return.
    /// Defaults to <see cref="W2JobHolder.Taxpayer"/>; single/MFS filers can
    /// leave it at the default.
    /// </summary>
    public W2JobHolder Holder { get; init; } = W2JobHolder.Taxpayer;

    /// <summary>W-2 Box 1: federal taxable wages for the year (after pre-tax deductions).</summary>
    public decimal WagesBox1 { get; init; }

    /// <summary>W-2 Box 2: federal income tax withheld for the year.</summary>
    public decimal FederalWithholdingBox2 { get; init; }

    /// <summary>W-2 Box 3: Social Security wages (capped at the SS wage base per employer).</summary>
    public decimal SocialSecurityWagesBox3 { get; init; }

    /// <summary>W-2 Box 4: Social Security tax withheld.</summary>
    public decimal SocialSecurityTaxBox4 { get; init; }

    /// <summary>W-2 Box 5: Medicare wages (no cap).</summary>
    public decimal MedicareWagesBox5 { get; init; }

    /// <summary>W-2 Box 6: Medicare tax withheld (including any employer-withheld Additional Medicare).</summary>
    public decimal MedicareTaxBox6 { get; init; }

    /// <summary>
    /// W-2 Box 16: state taxable wages for the year. When zero, the annual
    /// state-tax engine falls back to <see cref="WagesBox1"/> (federal wages)
    /// as a reasonable approximation for most states.
    /// </summary>
    public decimal StateWagesBox16 { get; init; }

    /// <summary>W-2 Box 17: state income tax withheld for the year.</summary>
    public decimal StateWithholdingBox17 { get; init; }

    /// <summary>
    /// Optional: the state this job's wages are sourced to. If unset, the
    /// taxpayer's residence state on <see cref="TaxYearProfile"/> is used.
    /// </summary>
    public UsState? SourceState { get; init; }
}
