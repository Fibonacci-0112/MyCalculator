using PaycheckCalc.Core.Tax.Federal;

namespace PaycheckCalc.Core.Models;

public sealed class PaycheckInput
{
    public PayFrequency Frequency { get; init; }
    public FilingStatus FilingStatus { get; init; }

    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; } = 1.5m;

    public UsState State { get; init; } = UsState.OK;
    public int StateAllowances { get; init; }
    public decimal StateAdditionalWithholding { get; init; } = 0m;

    public FederalW4Input FederalW4 { get; init; } = new();

    public IReadOnlyList<Deduction> Deductions { get; init; } = Array.Empty<Deduction>();
     
    public decimal YtdSocialSecurityWages { get; init; } = 0m;
    public decimal YtdMedicareWages { get; init; } = 0m;
}
