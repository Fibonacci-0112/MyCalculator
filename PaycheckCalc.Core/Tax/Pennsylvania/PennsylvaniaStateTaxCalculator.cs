using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Pennsylvania;

/// <summary>
/// Pennsylvania state income tax uses a flat rate of 3.07% applied to
/// taxable compensation (gross wages minus pre-tax deductions).
/// </summary>
public sealed class PennsylvaniaStateTaxCalculator : IStateTaxCalculator
{
    private const decimal FlatRate = 0.0307m;

    public UsState State => UsState.PA;

    public StateTaxResult CalculateWithholding(StateTaxInput input)
    {
        var taxableWages = Math.Max(0m, input.GrossWages - input.PreTaxDeductionsReducingStateWages);
        var withholding = Math.Round(taxableWages * FlatRate, 2, MidpointRounding.AwayFromZero)
                        + input.AdditionalWithholding;

        return new StateTaxResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
