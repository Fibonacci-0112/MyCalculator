using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Registry that maps each <see cref="UsState"/> to its <see cref="IStateTaxCalculator"/>.
/// States without a registered calculator throw so the caller knows a calculator is missing.
/// </summary>
public sealed class StateTaxCalculatorFactory
{
    private readonly Dictionary<UsState, IStateTaxCalculator> _calculators = new();
    private IReadOnlyList<UsState>? _sortedStates;

    /// <summary>
    /// Register a calculator. If one is already registered for the state it is replaced.
    /// </summary>
    public void Register(IStateTaxCalculator calculator)
    {
        _calculators[calculator.State] = calculator;
        _sortedStates = null; // invalidate cache
    }

    /// <summary>
    /// Returns true when a calculator has been registered for <paramref name="state"/>.
    /// </summary>
    public bool IsSupported(UsState state) => _calculators.ContainsKey(state);

    /// <summary>
    /// Get the calculator for the given state.
    /// </summary>
    public IStateTaxCalculator GetCalculator(UsState state)
    {
        if (_calculators.TryGetValue(state, out var calc))
            return calc;

        throw new NotSupportedException(
            $"State tax calculator for {state} has not been registered. " +
            $"Implement IStateTaxCalculator for {state} and register it with the factory.");
    }

    /// <summary>
    /// Returns all states that currently have a registered calculator.
    /// </summary>
    public IReadOnlyList<UsState> SupportedStates =>
        _sortedStates ??= _calculators.Keys.OrderBy(s => s.ToString()).ToList();
}
