using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.Core.Models;

public sealed class PaycheckInput
{
    public PayFrequency Frequency { get; init; }

    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; } = 1.5m;

    public UsState State { get; init; } = UsState.OK;

    /// <summary>
    /// Dynamic state-specific input values populated by the UI from the
    /// calculator's <see cref="IStateWithholdingCalculator.GetInputSchema"/>.
    /// </summary>
    public StateInputValues? StateInputValues { get; init; }

    public FederalW4Input FederalW4 { get; init; } = new();

    public IReadOnlyList<Deduction> Deductions { get; init; } = Array.Empty<Deduction>();
     
    public decimal YtdSocialSecurityWages { get; init; } = 0m;
    public decimal YtdMedicareWages { get; init; } = 0m;
}
