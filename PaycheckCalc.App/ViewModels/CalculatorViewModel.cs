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
using System.Collections.Specialized;

namespace PaycheckCalc.App.ViewModels;

public record PickerItem<T>(T Value, string Text)
{
    public override string ToString() => Text;
}
public partial class CalculatorViewModel : ObservableObject
{
    private readonly PayCalculator _calc;
    private readonly AnnualProjectionCalculator _projectionCalc;
    private readonly StateCalculatorRegistry _stateRegistry;
    private UsState _previousState;

    public CalculatorViewModel(PayCalculator calc, AnnualProjectionCalculator projectionCalc, StateCalculatorRegistry stateRegistry)
    {
        _calc = calc;
        _projectionCalc = projectionCalc;
        _stateRegistry = stateRegistry;
        Frequency = PayFrequency.Biweekly;
        SelectedFrequencyPickerItem = Frequencies.FirstOrDefault(f => f.Value == Frequency);
        OvertimeMultiplier = 1.5m;
        SelectedState = UsState.OK;
        _previousState = SelectedState;
        SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == SelectedState);
        SelectedFederalPickerItem = FederalStatuses[0];

        // Build initial dynamic state fields from schema
        RebuildStateFields();

        // Keep computed deduction totals in sync with the collection
        Deductions.CollectionChanged += OnDeductionsCollectionChanged;
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

    // ── Results-page tab state ──────────────────────────────
    [ObservableProperty] public partial int SelectedResultTab { get; set; } = 0;

    public bool IsResultTab0Visible => SelectedResultTab == 0;
    public bool IsResultTab1Visible => SelectedResultTab == 1;

    partial void OnSelectedResultTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsResultTab0Visible));
        OnPropertyChanged(nameof(IsResultTab1Visible));
    }

    [RelayCommand]
    private void SelectResultTab(string tab) => SelectedResultTab = int.Parse(tab);

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

    [ObservableProperty]
    public partial PickerItem<PayFrequency>? SelectedFrequencyPickerItem { get; set; }

    partial void OnSelectedFrequencyPickerItemChanged(PickerItem<PayFrequency>? value)
    {
        if (value != null)
            Frequency = value.Value;
    }

    [ObservableProperty] public partial PayFrequency Frequency { get; set; }

    [ObservableProperty] public partial decimal HourlyRate { get; set; }
    [ObservableProperty] public partial decimal RegularHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeHours { get; set; }
    [ObservableProperty] public partial decimal OvertimeMultiplier { get; set; }

    /// <summary>
    /// 1-based paycheck number within the current year for annual projections.
    /// </summary>
    [ObservableProperty] public partial int PaycheckNumber { get; set; } = 1;

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

    /// <summary>
    /// State-level validation errors returned by the calculator's <c>Validate</c> method.
    /// </summary>
    [ObservableProperty] public partial ObservableCollection<string> StateValidationErrors { get; set; } = new();

    public bool HasStateValidationErrors => StateValidationErrors.Count > 0;

    partial void OnStateValidationErrorsChanged(ObservableCollection<string> value)
    {
        OnPropertyChanged(nameof(HasStateValidationErrors));
    }

    /// <summary>True when the selected state has no extra input fields (e.g., no-income-tax states).</summary>
    public bool HasNoStateFields => StateFields.Count == 0;

    /// <summary>
    /// Cache of entered field values keyed by UsState → (fieldKey → rawValue).
    /// Preserves user-entered values when switching between states.
    /// </summary>
    private readonly Dictionary<UsState, Dictionary<string, object?>> _stateFieldCache = new();

    partial void OnSelectedStateChanged(UsState value)
    {
        // Keep the picker item in sync when SelectedState is set programmatically
        if (SelectedStatePickerItem?.Value != value)
            SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == value);
        RebuildStateFields();
    }

    private void RebuildStateFields()
    {
        // Save values for the outgoing state before clearing
        if (StateFields.Count > 0)
        {
            SaveFieldsForState(_previousState);
        }

        StateFields.Clear();
        StateValidationErrors = new ObservableCollection<string>();

        if (_stateRegistry.IsSupported(SelectedState))
        {
            var calc = _stateRegistry.GetCalculator(SelectedState);
            foreach (var field in calc.GetInputSchema())
            {
                var vm = new StateFieldViewModel(field);
                // Restore cached values if available
                if (_stateFieldCache.TryGetValue(SelectedState, out var cache) &&
                    cache.TryGetValue(field.Key, out var cached))
                {
                    switch (field.FieldType)
                    {
                        case StateFieldType.Picker:
                            vm.SelectedOption = cached?.ToString();
                            break;
                        case StateFieldType.Toggle:
                            vm.BoolValue = cached is true;
                            break;
                        default:
                            vm.StringValue = cached?.ToString() ?? "";
                            break;
                    }
                }
                StateFields.Add(vm);
            }
        }

        _previousState = SelectedState;
        OnPropertyChanged(nameof(HasNoStateFields));
    }

    /// <summary>Save field values into the cache for a specific state.</summary>
    private void SaveFieldsForState(UsState state)
    {
        if (StateFields.Count == 0) return;
        var cache = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in StateFields)
        {
            cache[field.Key] = field.Definition.FieldType switch
            {
                StateFieldType.Picker => field.SelectedOption,
                StateFieldType.Toggle => field.BoolValue,
                _ => field.StringValue
            };
        }
        _stateFieldCache[state] = cache;
    }

    /// <summary>
    /// Collection of itemized deductions. Users can add, remove, and edit each entry.
    /// </summary>
    public ObservableCollection<DeductionItemViewModel> Deductions { get; } = new();

    /// <summary>Pre-tax deduction total for display/comparison.</summary>
    public decimal TotalPretaxDeductions =>
        Deductions.Where(d => d.Type == DeductionType.PreTax).Sum(d => d.Amount);

    /// <summary>Post-tax deduction total for display/comparison.</summary>
    public decimal TotalPosttaxDeductions =>
        Deductions.Where(d => d.Type == DeductionType.PostTax).Sum(d => d.Amount);

    [RelayCommand]
    private void AddDeduction()
    {
        Deductions.Add(new DeductionItemViewModel());
    }

    [RelayCommand]
    private void RemoveDeduction(DeductionItemViewModel item)
    {
        Deductions.Remove(item);
    }

    private void OnDeductionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (DeductionItemViewModel item in e.NewItems)
                item.PropertyChanged += OnDeductionItemPropertyChanged;

        if (e.OldItems is not null)
            foreach (DeductionItemViewModel item in e.OldItems)
                item.PropertyChanged -= OnDeductionItemPropertyChanged;

        RaiseDeductionTotalsChanged();
    }

    private void OnDeductionItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DeductionItemViewModel.Amount)
                           or nameof(DeductionItemViewModel.Type))
        {
            RaiseDeductionTotalsChanged();
        }
    }

    private void RaiseDeductionTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalPretaxDeductions));
        OnPropertyChanged(nameof(TotalPosttaxDeductions));
    }

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

    /// <summary>
    /// Presentation-ready annual projection for the UI.
    /// </summary>
    [ObservableProperty] public partial AnnualProjectionModel? Projection { get; set; }

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

    public IReadOnlyList<PickerItem<PayFrequency>> Frequencies { get; } =
        Enum.GetValues<PayFrequency>()
            .Select(f => new PickerItem<PayFrequency>(f, EnumDisplay.PayFrequency(f.ToString())))
            .ToList();
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

        // Run local field-level validation on each state field
        bool hasFieldErrors = false;
        foreach (var field in StateFields)
        {
            field.Validate();
            if (field.HasError) hasFieldErrors = true;
        }

        // Run state calculator's Validate(...) for cross-field / business rules
        var stateErrors = new List<string>();
        if (_stateRegistry.IsSupported(SelectedState))
        {
            var calc = _stateRegistry.GetCalculator(SelectedState);
            stateErrors.AddRange(calc.Validate(stateValues));
        }
        StateValidationErrors = new ObservableCollection<string>(stateErrors);

        // Block calculation when state input is invalid
        if (hasFieldErrors || stateErrors.Count > 0)
            return;

        // Map ViewModel state → domain input via mapper
        var input = PaycheckInputMapper.Map(this, stateValues);

        // Run domain calculation
        var domainResult = _calc.Calculate(input);

        // Map domain result → presentation model via mapper
        ResultCard = ResultCardMapper.Map(domainResult);

        // Compute annual projections
        var domainProjection = _projectionCalc.Calculate(input, domainResult);
        Projection = AnnualProjectionMapper.Map(domainProjection);
    }
}
