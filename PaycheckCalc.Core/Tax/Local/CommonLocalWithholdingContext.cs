using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Local;

/// <summary>
/// Extends <see cref="CommonWithholdingContext"/> with the residency information
/// local calculators need for reciprocity rules (PA Act 32, Ohio work-city vs.
/// residence-city credit, NYC resident-only rule, etc.).
/// </summary>
public sealed record CommonLocalWithholdingContext(
    /// <summary>Payroll context inherited from state-level calculation.</summary>
    CommonWithholdingContext Common,

    /// <summary>Locality of the employee's primary residence, if known. Null when not supplied.</summary>
    LocalityId? HomeLocality,

    /// <summary>Locality where the work is performed, if known. Null when not supplied.</summary>
    LocalityId? WorkLocality,

    /// <summary>
    /// Whether <see cref="CurrentLocality"/> equals <see cref="HomeLocality"/>.
    /// Local calculators use this for resident-only taxes such as NYC and for the
    /// resident-rate side of PA Act 32.
    /// </summary>
    bool IsResident,

    /// <summary>The locality that this calculator is being invoked for.</summary>
    LocalityId CurrentLocality);
