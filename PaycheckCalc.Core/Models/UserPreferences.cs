namespace PaycheckCalc.Core.Models;

/// <summary>
/// Per-user defaults pre-filled into the calculator forms on first use.
/// Stored as opaque strings/decimals so Core stays decoupled from picking
/// specific enum types for preferences (the consumer parses to UsState,
/// FederalFilingStatus, PayFrequency as needed).
/// </summary>
public sealed record UserPreferences(
    string? DefaultState,
    string? DefaultFilingStatus,
    string? DefaultFrequency,
    decimal? DefaultOvertimeMultiplier,
    DateTimeOffset UpdatedAt);
