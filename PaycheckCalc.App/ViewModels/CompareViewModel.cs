using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for the Compare page. Supports two render modes:
///
///   * Legacy 1-vs-1 (Saved vs Current): when the user taps
///     "Save to Compare" on the Results page or "Compare" on a single
///     saved paycheck. Driven by <see cref="CalculatorViewModel.SavedScenario"/>.
///
///   * N-way multi-scenario: when the user selects 2+ saved paychecks on
///     the Saved Paychecks page and publishes them through
///     <see cref="ComparisonSession"/>. Each scenario is rendered as its
///     own card showing inputs + every tax line + net pay side-by-side.
///
/// Multi-scenario takes precedence whenever the session has any scenarios.
/// </summary>
public partial class CompareViewModel : ObservableObject
{
    private readonly ComparisonSession _session;

    public CompareViewModel(CalculatorViewModel calculator, ComparisonSession session)
    {
        Calculator = calculator;
        _session = session;

        Scenarios = session.Scenarios;
        Scenarios.CollectionChanged += OnScenariosChanged;

        // Keep the 1-vs-1 bindings (NetPayDifference, SavedScenario, ResultCard)
        // fresh when the user recalculates or swaps the saved scenario.
        calculator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalculatorViewModel.SavedScenario)
                or nameof(CalculatorViewModel.ResultCard)
                or nameof(CalculatorViewModel.HasSavedComparison)
                or nameof(CalculatorViewModel.HasNoSavedComparison)
                or nameof(CalculatorViewModel.NetPayDifference))
            {
                OnPropertyChanged(nameof(HasLegacyCompare));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(NetPayDifference));
            }
        };
    }

    /// <summary>Underlying calculator VM for bindings in the legacy 1-vs-1 view.</summary>
    public CalculatorViewModel Calculator { get; }

    /// <summary>Live collection of scenarios for the N-way view.</summary>
    public ObservableCollection<ScenarioSnapshot> Scenarios { get; }

    /// <summary>True when the multi-scenario view should render.</summary>
    public bool HasMultiScenarios => Scenarios.Count >= 2;

    /// <summary>True when the legacy 1-vs-1 Saved-vs-Current view should render.</summary>
    public bool HasLegacyCompare
        => !HasMultiScenarios && Calculator.HasSavedComparison;

    /// <summary>True when neither view has data and the empty-state message should render.</summary>
    public bool ShowEmptyState
        => !HasMultiScenarios && !Calculator.HasSavedComparison;

    /// <summary>Net-pay delta for the legacy 1-vs-1 view.</summary>
    public decimal NetPayDifference => Calculator.NetPayDifference;

    /// <summary>
    /// Drops the current multi-scenario set so the user can start over.
    /// Also clears the legacy saved scenario so the empty-state message
    /// is the final resting state.
    /// </summary>
    [RelayCommand]
    private void ClearCompare()
    {
        _session.Clear();
        Calculator.SavedScenario = null;
    }

    private void OnScenariosChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasMultiScenarios));
        OnPropertyChanged(nameof(HasLegacyCompare));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
