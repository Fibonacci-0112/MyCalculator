using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// Holds a snapshot of key inputs and the calculated result for side-by-side comparison.
/// </summary>
public sealed class ComparisonSnapshot
{
    public PayFrequency Frequency { get; init; }
    public decimal HourlyRate { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeMultiplier { get; init; }
    public UsState State { get; init; }
    public decimal PretaxDeductions { get; init; }
    public decimal PosttaxDeductions { get; init; }
    public PaycheckResult? Result { get; init; }
}
