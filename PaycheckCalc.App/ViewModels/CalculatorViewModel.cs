using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers; 
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.ViewModels;

public record PickerItem<T>(T Value, string Text)
{
    public override string ToString() => Text;
}
public partial class CalculatorViewModel : ObservableObject
{
    private readonly PayCalculator _calc;
    private readonly StateCalculatorRegistry _stateRegistry;

    public CalculatorViewModel(PayCalculator calc, StateCalculatorRegistry stateRegistry)
    {
        _calc = calc;
        _stateRegistry = stateRegistry;
        Frequency = PayFrequency.Biweekly;
        OvertimeMultiplier = 1.5m;
        SelectedState = UsState.OK;
        SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == SelectedState);
        SelectedFederalPickerItem = FederalStatuses[0];

        // Build initial dynamic state fields from schema
        RebuildStateFields();
    }

    [ObservableProperty] public partial int SelectedInputTab { get; set; } = 0;

    public bool IsTab0Visible => SelectedInputTab == 0;
    public bool IsTab1Visible => SelectedInputTab == 1;
    public bool IsTab2Visible => SelectedInputTab == 2;
    public bool IsTab3Visible => SelectedInputTab == 3;

    partial void OnSelectedInputTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTab0Visible));
        OnPropertyChanged(nameof(IsTab1Visible));
        OnPropertyChanged(nameof(IsTab2Visible));
        OnPropertyChanged(nameof(IsTab3Visible));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedInputTab = int.Parse(tab);

    public ObservableCollection<PickerItem<FederalFilingStatus>> FederalStatuses { get; } = new(
        Enum.GetValues<FederalFilingStatus>()
            .Select(s => new PickerItem<FederalFilingStatus>(s, EnumDisplay.FederalFilingStatus(s.ToString()))));

    [ObservableProperty]
    public partial PickerItem<FederalFilingStatus>? SelectedFederalPickerItem { get; set; }

    partial void OnSelectedFederalPickerItemChanged(PickerItem<FederalFilingStatus>? value)
    {
        if (value != null)
            FederalFilingStatus = value.Value;
    }

    [ObservableProperty] public partial PayFrequency Frequency { get; set; }

    [ObservableProperty] public partial decimal HourlyRate { get; set; }
    [ObservableProperty] public partial decimal RegularHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeMultiplier { get; set; }

    [ObservableProperty] public partial UsState SelectedState { get; set; }

    [ObservableProperty]
    public partial PickerItem<UsState>? SelectedStatePickerItem { get; set; }

    partial void OnSelectedStatePickerItemChanged(PickerItem<UsState>? value)
    {
        if (value != null)
            SelectedState = value.Value;
    }

    /// <summary>
    /// Dynamic state input fields driven by the selected state's schema.
    /// The UI binds to this collection to render the appropriate controls.
    /// </summary>
    public ObservableCollection<StateFieldViewModel> StateFields { get; } = new();

    partial void OnSelectedStateChanged(UsState value)
    {
        // Keep the picker item in sync when SelectedState is set programmatically
        if (SelectedStatePickerItem?.Value != value)
            SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == value);
        RebuildStateFields();
    }

    private void RebuildStateFields()
    {
        StateFields.Clear();
        if (_stateRegistry.IsSupported(SelectedState))
        {
            var calc = _stateRegistry.GetCalculator(SelectedState);
            foreach (var field in calc.GetInputSchema())
                StateFields.Add(new StateFieldViewModel(field));
        }
    }

    [ObservableProperty] public partial decimal PretaxDeductions { get; set; }
    [ObservableProperty] public partial decimal PosttaxDeductions { get; set; }

    // Federal (IRS 15-T / W-4)
    [ObservableProperty]
    public partial FederalFilingStatus FederalFilingStatus { get; set; }
        = FederalFilingStatus.SingleOrMarriedSeparately;

    [ObservableProperty] public partial bool FederalStep2Checked { get; set; }
    [ObservableProperty] public partial decimal FederalStep3Credits { get; set; }
    [ObservableProperty] public partial decimal FederalStep4aOtherIncome { get; set; }
    [ObservableProperty] public partial decimal FederalStep4bDeductions { get; set; }
    [ObservableProperty] public partial decimal FederalStep4cExtraWithholding { get; set; }
    [ObservableProperty] public partial PaycheckCalc.Core.Models.PaycheckResult? Result { get; set; }

    partial void OnResultChanged(PaycheckCalc.Core.Models.PaycheckResult? value)
    {
        OnPropertyChanged(nameof(NetPayDifference));
    }

    [ObservableProperty] public partial ComparisonSnapshot? SavedComparison { get; set; }

    public bool HasSavedComparison => SavedComparison is not null;
    public bool HasNoSavedComparison => SavedComparison is null;

    public decimal NetPayDifference =>
        (Result?.NetPay ?? 0m) - (SavedComparison?.Result?.NetPay ?? 0m);

    partial void OnSavedComparisonChanged(ComparisonSnapshot? value)
    {
        OnPropertyChanged(nameof(HasSavedComparison));
        OnPropertyChanged(nameof(HasNoSavedComparison));
        OnPropertyChanged(nameof(NetPayDifference));
    }

    [RelayCommand]
    private void SaveForCompare()
    {
        SavedComparison = new ComparisonSnapshot
        {
            Frequency = Frequency,
            HourlyRate = HourlyRate,
            RegularHours = RegularHours,
            OvertimeHours = OvertimeHours,
            OvertimeMultiplier = OvertimeMultiplier,
            State = SelectedState,
            PretaxDeductions = PretaxDeductions,
            PosttaxDeductions = PosttaxDeductions,
            Result = Result
        };
    }

    public IReadOnlyList<PayFrequency> Frequencies { get; } = Enum.GetValues(typeof(PayFrequency)).Cast<PayFrequency>().ToList();
    public IReadOnlyList<UsState> SupportedStates => _stateRegistry.SupportedStates;

    private IReadOnlyList<PickerItem<UsState>>? _statePickerItems;
    public IReadOnlyList<PickerItem<UsState>> StatePickerItems =>
        _statePickerItems ??= SupportedStates
            .Select(s => new PickerItem<UsState>(s, EnumDisplay.UsStateName(s.ToString())))
            .ToList();

    [RelayCommand]
    private void Calculate()
    {
        // Build dynamic state input values from the schema-driven fields
        var stateValues = new StateInputValues();
        foreach (var field in StateFields)
            stateValues[field.Key] = field.GetResolvedValue();

        var input = new PaycheckInput
        {
            Frequency = Frequency,
            HourlyRate = HourlyRate,
            RegularHours = RegularHours,
            OvertimeHours = OvertimeHours,
            OvertimeMultiplier = OvertimeMultiplier,
            State = SelectedState,
            StateInputValues = stateValues,
            FederalW4 = new FederalW4Input
            {
                    FilingStatus = FederalFilingStatus,
                    Step2Checked = FederalStep2Checked,
                    Step3TaxCredits = FederalStep3Credits,
                    Step4aOtherIncome = FederalStep4aOtherIncome,
                    Step4bDeductions = FederalStep4bDeductions,
                    Step4cExtraWithholding = FederalStep4cExtraWithholding
            },
             Deductions = new[]
            {
                new Deduction { Name = "Pre-tax", Type = DeductionType.PreTax, Amount = PretaxDeductions, ReducesStateTaxableWages = true },
                new Deduction { Name = "Post-tax", Type = DeductionType.PostTax, Amount = PosttaxDeductions }
            }
        };

        Result = _calc.Calculate(input);
    }
}
