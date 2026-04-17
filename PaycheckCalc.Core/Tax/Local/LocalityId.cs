using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.Local;

/// <summary>
/// Identifies a single taxing locality (city, school district, county add-on, etc.).
/// <para>
/// The <see cref="Code"/> is a free-form machine key (e.g. <c>"PA-PSD-510101"</c>,
/// <c>"NY-NYC"</c>, <c>"OH-RITA-CLEV"</c>, <c>"MD-MONT"</c>). It is deliberately a
/// string rather than an enum because jurisdictions such as PA PSD codes number in
/// the thousands and change over time, so hard-coding them as enum values does not scale.
/// </para>
/// </summary>
public sealed record LocalityId(UsState State, string Code, string Name)
{
    /// <summary>Returns a human-readable label (e.g. "Philadelphia, PA").</summary>
    public override string ToString() => $"{Name}, {State}";
}
