using CommunityToolkit.Mvvm.ComponentModel;

namespace PaycheckCalc.App.Models;

/// <summary>
/// Presentation-ready list item for the Saved Annual Scenarios UI.
/// Derived from a <see cref="Core.Models.SavedAnnualScenario"/> via
/// <see cref="Mappers.AnnualScenarioMapper"/>. Kept as an
/// <see cref="ObservableObject"/> so rename commands can update the
/// display label without rebuilding the entire list.
/// </summary>
public partial class SavedAnnualScenarioItemViewModel : ObservableObject
{
    [ObservableProperty] public partial Guid Id { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial int TaxYear { get; set; }
    [ObservableProperty] public partial string FilingStatusDisplay { get; set; } = "";
    [ObservableProperty] public partial string StateDisplay { get; set; } = "";
    [ObservableProperty] public partial DateTimeOffset UpdatedAt { get; set; }
}
