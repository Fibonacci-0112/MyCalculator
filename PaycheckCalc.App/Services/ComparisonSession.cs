using PaycheckCalc.App.Models;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.Services;

/// <summary>
/// Shared source of truth for the multi-scenario paycheck "What-if" compare.
/// Holds the ordered set of <see cref="ScenarioSnapshot"/> rows that the
/// Compare page renders side-by-side. Registered as a singleton so that the
/// Saved Paychecks page can populate it and the Compare page can consume it
/// without any navigation parameter plumbing.
/// </summary>
public sealed class ComparisonSession
{
    /// <summary>
    /// The ordered set of scenarios to display side-by-side. Empty when the
    /// user has not yet selected any scenarios from the Saved Paychecks page,
    /// in which case the Compare page falls back to the legacy 1-vs-1
    /// Saved-vs-Current layout.
    /// </summary>
    public ObservableCollection<ScenarioSnapshot> Scenarios { get; } = new();

    public void Clear() => Scenarios.Clear();

    public void SetScenarios(IEnumerable<ScenarioSnapshot> scenarios)
    {
        Scenarios.Clear();
        foreach (var s in scenarios)
            Scenarios.Add(s);
    }
}
