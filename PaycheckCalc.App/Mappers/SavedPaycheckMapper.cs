using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Maps between <see cref="SavedPaycheck"/> domain objects and
/// various presentation models used by the UI.
/// </summary>
public static class SavedPaycheckMapper
{
    /// <summary>
    /// Maps a persisted <see cref="SavedPaycheck"/> to a lightweight
    /// <see cref="SavedPaycheckViewModel"/> for list display.
    /// </summary>
    public static SavedPaycheckViewModel MapToListItem(SavedPaycheck saved)
    {
        return new SavedPaycheckViewModel
        {
            Id = saved.Id,
            Name = saved.Name,
            StateName = EnumDisplay.UsStateName(saved.Input.State.ToString()),
            GrossPay = saved.Result.GrossPay,
            NetPay = saved.Result.NetPay,
            CreatedAt = saved.CreatedAt,
            UpdatedAt = saved.UpdatedAt
        };
    }

    /// <summary>
    /// Maps a persisted <see cref="SavedPaycheck"/> to a
    /// <see cref="ScenarioSnapshot"/> for the Compare page.
    /// </summary>
    public static ScenarioSnapshot MapToScenarioSnapshot(SavedPaycheck saved)
    {
        return new ScenarioSnapshot
        {
            Frequency = saved.Input.Frequency,
            HourlyRate = saved.Input.HourlyRate,
            RegularHours = saved.Input.RegularHours,
            OvertimeHours = saved.Input.OvertimeHours,
            OvertimeMultiplier = saved.Input.OvertimeMultiplier,
            State = saved.Input.State,
            PretaxDeductions = saved.Result.PreTaxDeductions,
            PosttaxDeductions = saved.Result.PostTaxDeductions,
            ResultCard = ResultCardMapper.Map(saved.Result)
        };
    }
}
