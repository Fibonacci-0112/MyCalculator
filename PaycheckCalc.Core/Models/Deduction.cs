namespace PaycheckCalc.Core.Models;

public sealed class Deduction
{
    public string Name { get; init; } = "";
    public DeductionType Type { get; init; }
    public decimal Amount { get; init; }
    public bool ReducesStateTaxableWages { get; init; } = true;
}
