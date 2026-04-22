using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Tax.Washington;

/// <summary>
/// State module for Washington.  Washington levies no state individual income tax,
/// so income-tax withholding is always zero.  The calculator adds the mandatory
/// WA Cares Fund (Long-Term Care Insurance) premium at 0.58 % of all gross wages,
/// which is withheld from the employee each pay period.
///
/// Employees who hold a Department of Social and Health Services (DSHS)-approved
/// exemption certificate may opt out of the WA Cares Fund.  Set the
/// <c>WaCaresExempt</c> schema field to <c>true</c> to suppress that deduction.
///
/// Source: Washington State Department of Social and Health Services, WA Cares Fund
/// Employer Information (2026); RCW 50B.04.080.
/// </summary>
public sealed class WashingtonWithholdingCalculator : IStateWithholdingCalculator
{
    /// <summary>2026 WA Cares Fund employee premium rate (0.58 %).</summary>
    private const decimal WaCaresRate = 0.0058m;

    /// <summary>Display label for the WA Cares Fund line item.</summary>
    private const string WaCaresLabel = "WA Cares Fund (Long-Term Care)";

    private static readonly IReadOnlyList<StateFieldDefinition> Schema =
    [
        new()
        {
            Key = "WaCaresExempt",
            Label = "WA Cares Fund Exempt",
            FieldType = StateFieldType.Toggle,
            IsRequired = false,
            DefaultValue = false
        }
    ];

    public UsState State => UsState.WA;

    public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Schema;

    /// <summary>
    /// No required fields — validation always passes.
    /// </summary>
    public IReadOnlyList<string> Validate(StateInputValues values) => [];

    public StateWithholdingResult Calculate(CommonWithholdingContext context, StateInputValues values)
    {
        var exempt = values.GetValueOrDefault("WaCaresExempt", false);

        // WA Cares Fund: 0.58 % of ALL gross wages (no wage-base cap).
        // Applied before any pre-tax deductions — the fund does not follow
        // the same taxable-wage reductions used for income-tax purposes.
        var waCares = exempt
            ? 0m
            : Math.Round(Math.Max(0m, context.GrossWages) * WaCaresRate, 2,
                MidpointRounding.AwayFromZero);

        return new StateWithholdingResult
        {
            // Washington has no state income tax.
            TaxableWages = 0m,
            Withholding = 0m,
            DisabilityInsurance = waCares,
            DisabilityInsuranceLabel = WaCaresLabel
        };
    }
}
