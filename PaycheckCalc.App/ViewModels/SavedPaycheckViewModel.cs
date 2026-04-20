using CommunityToolkit.Mvvm.ComponentModel;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// Lightweight display model for a single saved paycheck in the list.
/// </summary>
public partial class SavedPaycheckViewModel : ObservableObject
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

    /// <summary>
    /// Selection flag used by the multi-scenario compare picker on the
    /// Saved Paychecks page.
    /// </summary>
    [ObservableProperty] public partial bool IsSelected { get; set; }

    /// <summary>
    /// Callback invoked whenever <see cref="IsSelected"/> changes, so the
    /// parent list VM can recompute its selected count without each row
    /// having to hold a back-reference.
    /// </summary>
    public Action? SelectionChanged { get; set; }

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
}

