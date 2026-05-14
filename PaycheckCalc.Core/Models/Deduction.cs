namespace PaycheckCalc.Core.Models;

public sealed class Deduction
{
    public string Name { get; init; } = "";
    public DeductionType Type { get; init; }
    public decimal Amount { get; init; }
    public DeductionAmountType AmountType { get; init; } = DeductionAmountType.Dollar;
    public bool ReducesStateTaxableWages { get; init; } = true;

    /// <summary>
    /// False for 401(k)/403(b)/457 contributions — these reduce federal and state
    /// taxable income but are still subject to FICA (Social Security and Medicare).
    /// True (default) for Section 125 cafeteria-plan deductions (health, dental, FSA)
    /// which are exempt from both income tax and FICA.
    /// </summary>
    public bool ReducesFicaWages { get; init; } = true;

    /// <summary>
    /// Returns the effective dollar amount of this deduction.
    /// For <see cref="DeductionAmountType.Dollar"/>, returns <see cref="Amount"/> directly.
    /// For <see cref="DeductionAmountType.Percentage"/>, computes <c>Amount / 100 × grossPay</c>.
    /// </summary>
    public decimal EffectiveAmount(decimal grossPay) => AmountType switch
    {
        DeductionAmountType.Percentage => Amount / 100m * grossPay,
        _ => Amount
    };
}
