using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Models;

/// <summary>
/// Storage model for a saved comparison scenario.
/// Captures key input parameters alongside the presentation-ready result card
/// so the Compare page can display both without referencing domain types directly.
/// </summary>
public sealed class ScenarioSnapshot
{
    // ── Input summary ───────────────────────────────────────
    public PayFrequency Frequency { get; init; }
    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; }
    public UsState State { get; init; }
    public decimal PretaxDeductions { get; init; }
    public decimal PosttaxDeductions { get; init; }

    // ── Presentation-ready result ───────────────────────────
    public ResultCardModel? ResultCard { get; init; }
}
