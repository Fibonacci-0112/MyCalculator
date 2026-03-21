using PaycheckCalc.App.Models;
using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Captures the current <see cref="CalculatorViewModel"/> state
/// into a <see cref="ScenarioSnapshot"/> for later comparison.
/// </summary>
public static class ScenarioMapper
{
    public static ScenarioSnapshot Capture(CalculatorViewModel vm)
    {
        return new ScenarioSnapshot
        {
            Frequency = vm.Frequency,
            HourlyRate = vm.HourlyRate,
            RegularHours = vm.RegularHours,
            OvertimeHours = vm.OvertimeHours,
            OvertimeMultiplier = vm.OvertimeMultiplier,
            State = vm.SelectedState,
            PretaxDeductions = vm.PretaxDeductions,
            PosttaxDeductions = vm.PosttaxDeductions,
            ResultCard = vm.ResultCard
        };
    }
}
