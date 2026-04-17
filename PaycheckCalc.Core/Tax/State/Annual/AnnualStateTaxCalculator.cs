using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Tax.State.Annual;

/// <summary>
/// Annual state / local income tax projection. Sits above the per-paycheck
/// <see cref="StateCalculatorRegistry"/> and lets callers get a year-end
/// state refund/owe number alongside the federal Form 1040 engine's output.
///
/// <para>
/// Design:
/// The repository already has a full plugin model for state withholding
/// (<see cref="IStateWithholdingCalculator"/>). Rather than duplicate 50
/// states of bracket logic, this calculator drives the existing registered
/// calculator once at <see cref="PayFrequency.Annual"/> frequency so that
/// the engine's internal annualization is a no-op and the returned
/// <see cref="StateWithholdingResult.Withholding"/> IS the annual tax.
/// </para>
///
/// <para>
/// Limitations (acceptable for a year-round projection tool):
/// - State taxable-income adjustments beyond wages (state-specific
///   subtractions, local add-ons, reciprocal-state credits, non-wage
///   income sourcing) are not modeled.
/// - Local income taxes (NYC, OH municipal, PA local EIT, etc.) are not
///   modeled here; they can be layered in later without changing this
///   contract.
/// </para>
/// </summary>
public sealed class AnnualStateTaxCalculator
{
    private readonly StateCalculatorRegistry _registry;

    public AnnualStateTaxCalculator(StateCalculatorRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Produces the annual state-tax projection for a taxpayer whose W-2s
    /// live on <paramref name="profile"/>. <paramref name="federalTaxAnnual"/>
    /// is passed through so states that deduct federal tax from state wages
    /// (e.g., Alabama) see it on the context.
    /// </summary>
    public AnnualStateTaxResult Calculate(TaxYearProfile profile, decimal federalTaxAnnual)
    {
        var state = profile.ResidenceState;
        var withheld = R(profile.W2Jobs.Sum(j => Math.Max(0m, j.StateWithholdingBox17)));

        // Prefer W-2 Box 16 when supplied; fall back to Box 1 federal wages.
        var stateWages = R(profile.W2Jobs.Sum(j =>
            j.StateWagesBox16 > 0m ? j.StateWagesBox16 : j.WagesBox1));

        if (!_registry.IsSupported(state))
        {
            // Unregistered state — return the withheld amount as refund/owe=0
            // (we have no engine to project liability against).
            return new AnnualStateTaxResult
            {
                State = state,
                StateWages = stateWages,
                StateTaxWithheld = withheld,
                StateRefundOrOwe = withheld, // conservatively treat liability as 0
                Description = "State calculator not registered; liability not projected."
            };
        }

        var calculator = _registry.GetCalculator(state);

        // No-income-tax states short-circuit cleanly.
        if (calculator is NoIncomeTaxWithholdingAdapter)
        {
            return new AnnualStateTaxResult
            {
                State = state,
                IsNoIncomeTaxState = true,
                StateWages = stateWages,
                StateIncomeTax = 0m,
                StateTaxWithheld = withheld,
                StateRefundOrOwe = withheld,
                Description = "No state income tax"
            };
        }

        if (stateWages <= 0m)
        {
            return new AnnualStateTaxResult
            {
                State = state,
                StateWages = 0m,
                StateTaxWithheld = withheld,
                StateRefundOrOwe = withheld
            };
        }

        // Annual frequency = 1 period, so the engine's internal
        // annualize/de-annualize step is a no-op and the returned
        // withholding equals the annual tax liability.
        var context = new CommonWithholdingContext(
            State: state,
            GrossWages: stateWages,
            PayPeriod: PayFrequency.Annual,
            Year: profile.TaxYear,
            PreTaxDeductionsReducingStateWages: 0m,
            FederalWithholdingPerPeriod: federalTaxAnnual);

        var values = profile.StateInputValues ?? new StateInputValues();
        var result = calculator.Calculate(context, values);

        var stateTax = R(Math.Max(0m, result.Withholding));
        var sdi = R(Math.Max(0m, result.DisabilityInsurance));

        return new AnnualStateTaxResult
        {
            State = state,
            StateWages = stateWages,
            StateIncomeTax = stateTax,
            StateDisabilityInsurance = sdi,
            StateDisabilityInsuranceLabel = result.DisabilityInsuranceLabel,
            StateTaxWithheld = withheld,
            StateRefundOrOwe = R(withheld - stateTax),
            Description = result.Description
        };
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
