namespace PaycheckCalc.Blazor.Data.Entities;

/// <summary>
/// One row per user holding the most recent in-progress session snapshot
/// for the three hubs (calculator, self-employment, annual planner). Each
/// snapshot is an opaque JSON string owned by its Blazor session-state
/// service; the database persists them as TEXT columns without inspecting
/// the inner shape.
/// </summary>
public class UserSessionStateEntity
{
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public string? CalculatorState { get; set; }
    public string? SelfEmploymentState { get; set; }
    public string? AnnualTaxState { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
