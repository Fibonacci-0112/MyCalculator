using PaycheckCalc.App.ViewModels;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.App.Mappers;

/// <summary>
/// Restores a <see cref="CalculatorViewModel"/>'s input fields from
/// a persisted <see cref="PaycheckInput"/>. This is the reverse of
/// <see cref="PaycheckInputMapper.Map"/>.
/// </summary>
public static class PaycheckInputRestorer
{
    public static void Restore(CalculatorViewModel vm, PaycheckInput input)
    {
        // ── Pay & Hours ─────────────────────────────────────
        vm.Frequency = input.Frequency;
        vm.SelectedFrequencyPickerItem = vm.Frequencies.FirstOrDefault(f => f.Value == input.Frequency);
        vm.HourlyRate = input.HourlyRate;
        vm.RegularHours = input.RegularHours;
        vm.OvertimeHours = input.OvertimeHours;
        vm.OvertimeMultiplier = input.OvertimeMultiplier;
        vm.PaycheckNumber = input.PaycheckNumber;

        // ── Federal W-4 ────────────────────────────────────
        vm.FederalFilingStatus = input.FederalW4.FilingStatus;
        vm.SelectedFederalPickerItem = vm.FederalStatuses.FirstOrDefault(
            f => f.Value == input.FederalW4.FilingStatus);
        vm.FederalStep2Checked = input.FederalW4.Step2Checked;
        vm.FederalStep3Credits = input.FederalW4.Step3TaxCredits;
        vm.FederalStep4aOtherIncome = input.FederalW4.Step4aOtherIncome;
        vm.FederalStep4bDeductions = input.FederalW4.Step4bDeductions;
        vm.FederalStep4cExtraWithholding = input.FederalW4.Step4cExtraWithholding;

        // ── Deductions ──────────────────────────────────────
        vm.Deductions.Clear();
        foreach (var d in input.Deductions)
        {
            var item = new DeductionItemViewModel
            {
                Name = d.Name,
                Amount = d.Amount,
                AmountType = d.AmountType,
                ReducesStateTaxableWages = d.ReducesStateTaxableWages
            };
            item.SelectedDeductionTypePickerItem = item.DeductionTypeItems
                .FirstOrDefault(t => t.Value == d.Type);
            vm.Deductions.Add(item);
        }

        // ── State ───────────────────────────────────────────
        // Pre-populate the state field cache so RebuildStateFields() (triggered by
        // setting SelectedState) restores the saved values into the field ViewModels.
        if (input.StateInputValues is not null)
        {
            var cache = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in input.StateInputValues)
                cache[kvp.Key] = kvp.Value;
            vm.SetStateFieldCache(input.State, cache);
        }

        vm.SelectedState = input.State;
        vm.SelectedStatePickerItem = vm.StatePickerItems.FirstOrDefault(s => s.Value == input.State);
    }
}
