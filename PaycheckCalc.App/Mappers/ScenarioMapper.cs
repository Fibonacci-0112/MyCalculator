using PaycheckCalc.App.Helpers;
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
        var (stateFiling, stateAllowances) = ExtractStateDisplay(vm);
        return new ScenarioSnapshot
        {
            Name = "Current",
            Frequency = vm.Frequency,
            HourlyRate = vm.HourlyRate,
            RegularHours = vm.RegularHours,
            OvertimeHours = vm.OvertimeHours,
            OvertimeMultiplier = vm.OvertimeMultiplier,
            State = vm.SelectedState,
            PretaxDeductions = vm.TotalPretaxDeductions,
            PosttaxDeductions = vm.TotalPosttaxDeductions,
            FederalFilingStatusDisplay = EnumDisplay.FederalFilingStatus(vm.FederalFilingStatus.ToString()),
            StateFilingStatusDisplay = stateFiling,
            StateAllowancesDisplay = stateAllowances,
            ResultCard = vm.ResultCard
        };
    }

    /// <summary>
    /// Pulls common state filing-status and allowances fields from the
    /// dynamic state schema, using the conventional "FilingStatus" and
    /// "Allowances" keys that most state calculators expose. Returns empty
    /// strings for calculators that do not surface those fields.
    /// </summary>
    private static (string FilingStatus, string Allowances) ExtractStateDisplay(CalculatorViewModel vm)
    {
        string filing = "";
        string allowances = "";
        foreach (var f in vm.StateFields)
        {
            if (string.Equals(f.Key, "FilingStatus", StringComparison.OrdinalIgnoreCase))
                filing = f.GetResolvedValue()?.ToString() ?? "";
            else if (string.Equals(f.Key, "Allowances", StringComparison.OrdinalIgnoreCase))
                allowances = f.GetResolvedValue()?.ToString() ?? "";
        }
        return (filing, allowances);
    }
}
