using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State;

/// <summary>
/// Common contract that every state-tax calculator must implement.
/// States with no income tax return zero withholding.
/// </summary>
public interface IStateTaxCalculator
{
    UsState State { get; }
    StateTaxResult CalculateWithholding(StateTaxInput input);
}
