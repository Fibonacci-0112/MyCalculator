namespace PaycheckCalc.Core.Models;

/// <summary>
/// Server-side snapshot of a user's in-progress calculator/SE/annual planner
/// session state. The three string fields are opaque JSON blobs — Core does
/// not know their inner shape; the Blazor session state services own the
/// (de)serialization of their own snapshot records.
/// </summary>
public sealed record SessionStateSnapshot(
    string? CalculatorState,
    string? SelfEmploymentState,
    string? AnnualTaxState,
    DateTimeOffset UpdatedAt);
