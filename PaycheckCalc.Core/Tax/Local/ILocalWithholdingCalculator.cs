using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Local;

/// <summary>
/// Plugin contract for local (sub-state) income-tax withholding. Mirrors
/// <see cref="IStateWithholdingCalculator"/> so the UI can drive both from
/// the same schema-field rendering pipeline.
/// <list type="number">
///   <item>What inputs do I need? → <see cref="GetInputSchema"/></item>
///   <item>Are the provided inputs valid? → <see cref="Validate"/></item>
///   <item>How do I calculate withholding? → <see cref="Calculate"/></item>
/// </list>
/// </summary>
public interface ILocalWithholdingCalculator
{
    /// <summary>The locality this calculator handles.</summary>
    LocalityId Locality { get; }

    /// <summary>
    /// Returns the input field definitions this locality requires.
    /// Reuses <see cref="StateFieldDefinition"/> so the existing dynamic UI
    /// renderer works without a parallel schema type.
    /// </summary>
    IReadOnlyList<StateFieldDefinition> GetInputSchema();

    /// <summary>
    /// Validates the user-supplied values against this locality's rules.
    /// Returns an empty list when all values are valid.
    /// </summary>
    IReadOnlyList<string> Validate(LocalInputValues values);

    /// <summary>
    /// Calculates local income-tax withholding for one pay period.
    /// </summary>
    LocalWithholdingResult Calculate(CommonLocalWithholdingContext context, LocalInputValues values);
}
