using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Alabama;

public sealed class AlabamaStateTaxCalculator : IStateTaxCalculator
{
    private readonly AlabamaFormulaCalculator _inner;

    public AlabamaStateTaxCalculator(AlabamaFormulaCalculator inner)
        => _inner = inner;
    public UsState State => UsState.AL;

    public StateTaxResult CalculateWithholding(StateTaxInput input)
    {
        // Alabama state income tax uses a flat rate of 5% applied to
        // taxable compensation (gross wages minus pre-tax deductions).
        var taxableWages = Math.Max(0m, input.GrossWages - input.PreTaxDeductionsReducingStateWages);
        var withholding = Math.Round(taxableWages * 0.05m, 2, MidpointRounding.AwayFromZero)
                        + input.AdditionalWithholding;

        return new StateTaxResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
