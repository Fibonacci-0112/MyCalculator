using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Oklahoma;

/// <summary>
/// Adapts <see cref="OklahomaOw2PercentageCalculator"/> to the
/// <see cref="IStateTaxCalculator"/> interface so Oklahoma plugs into the
/// multi-state calculation pipeline.
/// </summary>
public sealed class OklahomaStateTaxCalculator : IStateTaxCalculator
{
    private readonly OklahomaOw2PercentageCalculator _inner;

    public OklahomaStateTaxCalculator(OklahomaOw2PercentageCalculator inner)
        => _inner = inner;

    public UsState State => UsState.OK;

    public StateTaxResult CalculateWithholding(StateTaxInput input)
    {
        var allowancesTotal = input.Allowances * _inner.GetAllowanceAmount(input.Frequency);
        var taxableWages = Math.Max(0m, input.GrossWages - input.PreTaxDeductionsReducingStateWages - allowancesTotal);
        var withholding = _inner.CalculateWithholding(taxableWages, input.Frequency, input.FilingStatus)
                        + input.AdditionalWithholding;

        return new StateTaxResult
        {
            TaxableWages = taxableWages,
            Withholding = withholding
        };
    }
}
