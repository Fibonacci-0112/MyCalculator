namespace PaycheckCalc.Core.Models;

/// <summary>
/// Domain DTO that encapsulates a complete calculation scenario:
/// the input parameters used and the computed result.
/// Useful for persisting, comparing, or replaying scenarios
/// without coupling to any UI model.
/// </summary>
public sealed class CalculationScenario
{
    public required PaycheckInput Input { get; init; }
    public required PaycheckResult Result { get; init; }
}
