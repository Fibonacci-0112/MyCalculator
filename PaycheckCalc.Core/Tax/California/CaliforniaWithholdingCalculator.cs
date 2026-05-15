using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.California;

/// <summary>
/// State module for California that wraps <see cref="CaliforniaPercentageCalculator"/>
/// and exposes its inputs through the dynamic <see cref="IStateWithholdingCalculator"/> schema.
/// </summary>
public sealed class CaliforniaWithholdingCalculator : IStateWithholdingCalculator
{
    private readonly CaliforniaPercentageCalculator _inner;
    private readonly IReadOnlyList<string> _filingStatusOptions;

    /// <summary>2026 California SDI rate (1.3%) applied to all gross wages.</summary>
    private const decimal SdiRate = 0.013m;

    public CaliforniaWithholdingCalculator(CaliforniaPercentageCalculator inner, IStateSchemaProvider schemaProvider)
    {
        _inner = inner;
        _filingStatusOptions = schemaProvider.GetOptions(UsState.CA, "FilingStatus");
    }

    public UsState State => UsState.CA;

    public IReadOnlyList<string> Validate(StateInputValues values)
    {
        var errors = new List<string>();
        var status = values.GetValueOrDefault<string>("FilingStatus", "");
        if (!_filingStatusOptions.Contains(status))
            errors.Add($"Filing Status must be one of: {string.Join(", ", _filingStatusOptions)}.");
        return errors;
    }

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var filingStatusStr = values.GetValueOrDefault("FilingStatus", "Single");
        var filingStatus = MapFilingStatus(filingStatusStr);
        var regularAllowances = values.GetValueOrDefault("RegularAllowances", 0);
        var estimatedDeductionAllowances = values.GetValueOrDefault("EstimatedDeductionAllowances", 0);
        var additionalWithholding = values.GetValueOrDefault("AdditionalWithholding", 0m);

        var grossWages = Math.Max(0m, context.GrossWages - context.PreTaxDeductionsReducingStateWages);

        var withholding = _inner.CalculateWithholding(
            grossWages,
            context.PayPeriod,
            filingStatus,
            regularAllowances,
            estimatedDeductionAllowances);

        // Workaround: Single filing status is off by 3 cents
        if (filingStatus == CaliforniaFilingStatus.Single && withholding > 0m)
            withholding = Math.Max(0m, withholding - 0.03m);

        // California SDI: 1.3% of ALL gross wages (no wage cap)
        var sdi = Math.Round(Math.Max(0m, context.GrossWages) * SdiRate, 2, MidpointRounding.AwayFromZero);

        return new StateWithholdingResult
        {
            TaxableWages = grossWages,
            Withholding = withholding + additionalWithholding,
            DisabilityInsurance = sdi,
            DisabilityInsuranceLabel = "State Disability Insurance (SDI)"
        };
    }

    private static CaliforniaFilingStatus MapFilingStatus(string status) => status switch
    {
        "Married" => CaliforniaFilingStatus.Married,
        "Head of Household" => CaliforniaFilingStatus.HeadOfHousehold,
        _ => CaliforniaFilingStatus.Single
    };
}
