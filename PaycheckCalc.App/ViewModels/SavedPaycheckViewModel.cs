namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// Lightweight display model for a single saved paycheck in the list.
/// </summary>
public sealed class SavedPaycheckViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string StateName { get; init; } = "";
    public decimal GrossPay { get; init; }
    public decimal NetPay { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Display-friendly date string.</summary>
    public string DateDisplay => UpdatedAt.LocalDateTime.ToString("MMM d, yyyy h:mm tt");
}
