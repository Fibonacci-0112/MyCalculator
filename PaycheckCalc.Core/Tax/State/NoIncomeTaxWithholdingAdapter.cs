using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Adapter for states with no individual income tax (AK, FL, NV, NH, SD, TN, TX, WA, WY).
/// Provides an empty input schema and always returns zero withholding.
/// </summary>
public sealed class NoIncomeTaxWithholdingAdapter : IStateWithholdingCalculator
{
    public NoIncomeTaxWithholdingAdapter(UsState state) => State = state;

    public UsState State { get; }

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => [];

    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
        => new() { TaxableWages = 0m, Withholding = 0m, Description = "No state income tax" };
}
