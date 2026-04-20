using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Models;

/// <summary>
/// Storage model for a saved comparison scenario.
/// Captures key input parameters alongside the presentation-ready result card
/// so the Compare page can display both without referencing domain types directly.
/// </summary>
public sealed class ScenarioSnapshot
{
    /// <summary>
    /// Display-only label for the scenario. Empty for the "live" snapshot
    /// captured from the calculator; populated with the saved paycheck
    /// name for persisted scenarios used in multi-scenario compare.
    /// </summary>
    public string Name { get; init; } = "";

    // ── Input summary ───────────────────────────────────────
    public PayFrequency Frequency { get; init; }
    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; }
    public UsState State { get; init; }
    public decimal PretaxDeductions { get; init; }
    public decimal PosttaxDeductions { get; init; }

    /// <summary>
    /// Display-only federal filing status (e.g. "Married filing jointly").
    /// Surfaced on the multi-scenario compare view so the user can see at
    /// a glance which W-4 filing status each scenario assumes.
    /// </summary>
    public string FederalFilingStatusDisplay { get; init; } = "";

    /// <summary>
    /// Display-only state filing status taken from the state input schema
    /// when present (e.g. Oklahoma "Single", California "Head of Household").
    /// Empty when the state calculator does not expose a filing status.
    /// </summary>
    public string StateFilingStatusDisplay { get; init; } = "";

    /// <summary>
    /// Display-only state allowances / exemptions taken from the state input
    /// schema when present. Empty when not applicable.
    /// </summary>
    public string StateAllowancesDisplay { get; init; } = "";

    // ── Presentation-ready result ───────────────────────────
    public ResultCardModel? ResultCard { get; init; }
}
