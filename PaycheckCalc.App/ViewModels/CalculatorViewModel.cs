using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
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
    /// <summary>
    /// Presentation-ready result card for the UI — never the raw domain PaycheckResult.
    /// </summary>
    [ObservableProperty] public partial ResultCardModel? ResultCard { get; set; }

    partial void OnResultCardChanged(ResultCardModel? value)
    {
        OnPropertyChanged(nameof(NetPayDifference));
    }

    /// <summary>
    /// Saved scenario snapshot for side-by-side comparison.
    /// </summary>
    [ObservableProperty] public partial ScenarioSnapshot? SavedScenario { get; set; }

    public bool HasSavedComparison => SavedScenario is not null;
    public bool HasNoSavedComparison => SavedScenario is null;

    public decimal NetPayDifference =>
        (ResultCard?.NetPay ?? 0m) - (SavedScenario?.ResultCard?.NetPay ?? 0m);

    partial void OnSavedScenarioChanged(ScenarioSnapshot? value)
    {
        OnPropertyChanged(nameof(HasSavedComparison));
        OnPropertyChanged(nameof(HasNoSavedComparison));
        OnPropertyChanged(nameof(NetPayDifference));
    }

    [RelayCommand]
    private void SaveForCompare()
    {
        SavedScenario = ScenarioMapper.Capture(this);
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

        // Map ViewModel state → domain input via mapper
        var input = PaycheckInputMapper.Map(this, stateValues);

        // Run domain calculation
        var domainResult = _calc.Calculate(input);

        // Map domain result → presentation model via mapper
        ResultCard = ResultCardMapper.Map(domainResult);
    }
}
