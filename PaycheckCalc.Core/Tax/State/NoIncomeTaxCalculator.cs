using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Calculator for states that have no individual income tax:
/// AK, FL, NV, NH, SD, TN, TX, WA, WY.
/// </summary>
public sealed class NoIncomeTaxCalculator : IStateTaxCalculator
{
    public NoIncomeTaxCalculator(UsState state) => State = state;

    public UsState State { get; }

    public StateTaxResult CalculateWithholding(StateTaxInput input)
        => new() { TaxableWages = 0m, Withholding = 0m };
}
