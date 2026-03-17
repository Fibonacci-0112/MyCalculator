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
    private readonly StateTaxCalculatorFactory _stateFactory;

    public CalculatorViewModel(PayCalculator calc, StateTaxCalculatorFactory stateFactory)
    {
        _calc = calc;
        _stateFactory = stateFactory;
        Frequency = PayFrequency.Biweekly;
        FilingStatus = FilingStatus.Single;
        OvertimeMultiplier = 1.5m;
        SelectedState = UsState.OK;
        SelectedFederalPickerItem = FederalStatuses[0];

        // Auto-recalculate whenever any input property changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(Result) && e.PropertyName != nameof(SelectedInputTab)
                && !e.PropertyName!.StartsWith("IsTab"))
                Calculate();
        };
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
    [ObservableProperty] public partial FilingStatus FilingStatus { get; set; }

    [ObservableProperty] public partial decimal HourlyRate { get; set; }
    [ObservableProperty] public partial decimal RegularHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeMultiplier { get; set; }

    [ObservableProperty] public partial UsState SelectedState { get; set; }
    [ObservableProperty] public partial int StateAllowances { get; set; }
    [ObservableProperty] public partial decimal StateAdditionalWithholding { get; set; }

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

    public IReadOnlyList<PayFrequency> Frequencies { get; } = Enum.GetValues(typeof(PayFrequency)).Cast<PayFrequency>().ToList();
    public IReadOnlyList<FilingStatus> Statuses { get; } = Enum.GetValues(typeof(FilingStatus)).Cast<FilingStatus>().ToList();
    public IReadOnlyList<UsState> SupportedStates => _stateFactory.SupportedStates;

    [RelayCommand]
    private void Calculate()
    {
        var input = new PaycheckInput
        {
            Frequency = Frequency,
            FilingStatus = FilingStatus,
            HourlyRate = HourlyRate,
            RegularHours = RegularHours,
            OvertimeHours = OvertimeHours,
            OvertimeMultiplier = OvertimeMultiplier,
            State = SelectedState,
            StateAllowances = StateAllowances,
            StateAdditionalWithholding = StateAdditionalWithholding,
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
