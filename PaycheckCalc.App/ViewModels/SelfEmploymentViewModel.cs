using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.Core.Export;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.SelfEmployment;
using PaycheckCalc.Core.Tax.State;
using System.Collections.ObjectModel;

namespace PaycheckCalc.App.ViewModels;

public partial class SelfEmploymentViewModel : ObservableObject
{
    private readonly SelfEmploymentCalculator _calc;
    private readonly StateCalculatorRegistry _stateRegistry;
    private UsState _previousState;

    public SelfEmploymentViewModel(SelfEmploymentCalculator calc, StateCalculatorRegistry stateRegistry)
    {
        _calc = calc;
        _stateRegistry = stateRegistry;
        SelectedState = UsState.TX;
        _previousState = SelectedState;
        SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == SelectedState);
        SelectedFederalPickerItem = FederalStatuses[0];
        RebuildStateFields();
    }

    // ── Results tab state ───────────────────────────────────
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

    // ── Federal filing status ───────────────────────────────
    public ObservableCollection<PickerItem<FederalFilingStatus>> FederalStatuses { get; } = new(
        Enum.GetValues<FederalFilingStatus>()
            .Select(s => new PickerItem<FederalFilingStatus>(s, EnumDisplay.FederalFilingStatus(s.ToString()))));

    [ObservableProperty] public partial PickerItem<FederalFilingStatus>? SelectedFederalPickerItem { get; set; }

    partial void OnSelectedFederalPickerItemChanged(PickerItem<FederalFilingStatus>? value)
    {
        if (value != null) FederalFilingStatus = value.Value;
    }

    [ObservableProperty]
    public partial FederalFilingStatus FederalFilingStatus { get; set; }
        = FederalFilingStatus.SingleOrMarriedSeparately;

    // ── Schedule C inputs ───────────────────────────────────
    [ObservableProperty] public partial decimal GrossRevenue { get; set; }
    [ObservableProperty] public partial decimal CostOfGoodsSold { get; set; }
    [ObservableProperty] public partial decimal TotalBusinessExpenses { get; set; }
    [ObservableProperty] public partial decimal OtherIncome { get; set; }

    // ── W-2 FICA coordination ───────────────────────────────
    /// <summary>W-2 Box 3: Social Security wages for SS wage base coordination.</summary>
    [ObservableProperty] public partial decimal W2SocialSecurityWages { get; set; }

    /// <summary>W-2 Box 5: Medicare wages for Additional Medicare threshold coordination.</summary>
    [ObservableProperty] public partial decimal W2MedicareWages { get; set; }

    // ── Deduction / QBI inputs ──────────────────────────────
    [ObservableProperty] public partial decimal ItemizedDeductionsOverStandard { get; set; }
    [ObservableProperty] public partial bool IsSpecifiedServiceBusiness { get; set; }
    [ObservableProperty] public partial decimal QualifiedBusinessW2Wages { get; set; }
    [ObservableProperty] public partial decimal QualifiedPropertyUbia { get; set; }

    // ── Estimated payments ──────────────────────────────────
    [ObservableProperty] public partial decimal EstimatedTaxPayments { get; set; }

    // ── State ───────────────────────────────────────────────
    [ObservableProperty] public partial UsState SelectedState { get; set; }

    [ObservableProperty] public partial PickerItem<UsState>? SelectedStatePickerItem { get; set; }

    partial void OnSelectedStatePickerItemChanged(PickerItem<UsState>? value)
    {
        if (value != null) SelectedState = value.Value;
    }

    public ObservableCollection<StateFieldViewModel> StateFields { get; } = new();

    [ObservableProperty] public partial ObservableCollection<string> StateValidationErrors { get; set; } = new();
    public bool HasStateValidationErrors => StateValidationErrors.Count > 0;

    partial void OnStateValidationErrorsChanged(ObservableCollection<string> value)
    {
        OnPropertyChanged(nameof(HasStateValidationErrors));
    }

    public bool HasNoStateFields => StateFields.Count == 0;

    private readonly Dictionary<UsState, Dictionary<string, object?>> _stateFieldCache = new();

    partial void OnSelectedStateChanged(UsState value)
    {
        if (SelectedStatePickerItem?.Value != value)
            SelectedStatePickerItem = StatePickerItems.FirstOrDefault(s => s.Value == value);
        RebuildStateFields();
    }

    private void RebuildStateFields()
    {
        if (StateFields.Count > 0)
            SaveFieldsForState(_previousState);

        StateFields.Clear();
        StateValidationErrors = new ObservableCollection<string>();

        if (_stateRegistry.IsSupported(SelectedState))
        {
            var calc = _stateRegistry.GetCalculator(SelectedState);
            foreach (var field in calc.GetInputSchema())
            {
                var vm = new StateFieldViewModel(field);
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

    public IReadOnlyList<UsState> SupportedStates => _stateRegistry.SupportedStates;

    private IReadOnlyList<PickerItem<UsState>>? _statePickerItems;
    public IReadOnlyList<PickerItem<UsState>> StatePickerItems =>
        _statePickerItems ??= SupportedStates
            .Select(s => new PickerItem<UsState>(s, EnumDisplay.UsStateName(s.ToString())))
            .ToList();

    // ── Result ──────────────────────────────────────────────
    private SelfEmploymentResult? _lastResult;

    [ObservableProperty] public partial SelfEmploymentResultModel? ResultModel { get; set; }

    public bool CanExport => _lastResult is not null;
    public bool HasResult => _lastResult is not null;

    partial void OnResultModelChanged(SelfEmploymentResultModel? value)
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(HasResult));
    }

    // ── Calculate ───────────────────────────────────────────
    [RelayCommand]
    private void Calculate()
    {
        var stateValues = new StateInputValues();
        foreach (var field in StateFields)
            stateValues[field.Key] = field.GetResolvedValue();

        bool hasFieldErrors = false;
        foreach (var field in StateFields)
        {
            field.Validate();
            if (field.HasError) hasFieldErrors = true;
        }

        var stateErrors = new List<string>();
        if (_stateRegistry.IsSupported(SelectedState))
        {
            var calc = _stateRegistry.GetCalculator(SelectedState);
            stateErrors.AddRange(calc.Validate(stateValues));
        }
        StateValidationErrors = new ObservableCollection<string>(stateErrors);

        if (hasFieldErrors || stateErrors.Count > 0)
            return;

        var input = SelfEmploymentInputMapper.Map(this, stateValues);
        var domainResult = _calc.Calculate(input);
        _lastResult = domainResult;
        ResultModel = SelfEmploymentResultMapper.Map(domainResult);
    }

    // ── Export ───────────────────────────────────────────────
    [RelayCommand]
    private async Task ExportCsv()
    {
        if (_lastResult is null) return;

        var csv = CsvSelfEmploymentExporter.Generate(_lastResult);
        var fileName = $"se_tax_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, csv);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export Self-Employment Tax CSV",
            File = new ShareFile(filePath)
        });
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        if (_lastResult is null) return;

        var pdf = PdfSelfEmploymentExporter.Generate(_lastResult);
        var fileName = $"se_tax_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, pdf);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export Self-Employment Tax PDF",
            File = new ShareFile(filePath)
        });
    }
}
