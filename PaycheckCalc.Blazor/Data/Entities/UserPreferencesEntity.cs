namespace PaycheckCalc.Blazor.Data.Entities;

/// <summary>
/// One row per user holding default form values pre-filled into the
/// calculator on first use. Mirrors the
/// <see cref="PaycheckCalc.Core.Models.UserPreferences"/> Core POCO.
/// </summary>
public class UserPreferencesEntity
{
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public string? DefaultState { get; set; }
    public string? DefaultFilingStatus { get; set; }
    public string? DefaultFrequency { get; set; }
    public decimal? DefaultOvertimeMultiplier { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
