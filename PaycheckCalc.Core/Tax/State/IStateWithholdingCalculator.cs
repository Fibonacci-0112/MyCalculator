using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Enhanced state tax calculator interface that treats each state as a 
/// self-contained plugin answering three questions:
/// <list type="number">
///   <item>What inputs do I need? → <see cref="GetInputSchema"/></item>
///   <item>Are the provided inputs valid? → <see cref="Validate"/></item>
///   <item>How do I calculate withholding? → <see cref="Calculate"/></item>
/// </list>
/// <para>
/// Each state defines its own fields via a flexible
/// <see cref="StateInputValues"/> dictionary so it can declare
/// whatever inputs it needs (e.g., Alabama's 5 filing statuses, dependents).
/// </para>
/// </summary>
public interface IStateWithholdingCalculator
{
    /// <summary>The state this calculator handles.</summary>
    UsState State { get; }

    /// <summary>
    /// Returns the input field definitions this state requires.
    /// The UI reads this list and dynamically builds the form controls.
    /// </summary>
    IReadOnlyList<StateFieldDefinition> GetInputSchema();

    /// <summary>
    /// Validates the user-supplied values against this state's rules.
    /// Returns an empty list when all values are valid.
    /// </summary>
    IReadOnlyList<string> Validate(StateInputValues values);

    /// <summary>
    /// Calculates state income tax withholding for one pay period.
    /// </summary>
    StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values);
}
