using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalc.Core.Tax.State;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// ViewModel for a single dynamic state input field.
/// The UI binds to these properties to render the appropriate control
/// (Picker, Entry, Switch) and read/write the current value.
/// </summary>
public partial class StateFieldViewModel : ObservableObject
{
    private readonly Action _onValueChanged;

    public StateFieldViewModel(StateFieldDefinition definition, Action onValueChanged)
    {
        Definition = definition;
        _onValueChanged = onValueChanged;

        // Initialize from the schema default
        if (definition.FieldType == StateFieldType.Picker)
            _selectedOption = definition.DefaultValue?.ToString()
                ?? definition.Options?.FirstOrDefault() ?? "";
        else if (definition.FieldType == StateFieldType.Toggle)
            _boolValue = definition.DefaultValue is true;
        else
            _stringValue = definition.DefaultValue?.ToString() ?? "0";
    }

    public StateFieldDefinition Definition { get; }

    public string Key => Definition.Key;
    public string Label => Definition.Label;
    public IReadOnlyList<string>? Options => Definition.Options;

    // Visibility flags for XAML control switching
    public bool IsPicker => Definition.FieldType == StateFieldType.Picker;
    public bool IsNumeric => Definition.FieldType is StateFieldType.Integer or StateFieldType.Decimal;
    public bool IsToggle => Definition.FieldType == StateFieldType.Toggle;
    public bool IsCurrency => Definition.FieldType == StateFieldType.Decimal;

    // Picker value
    [ObservableProperty] public partial string? SelectedOption { get; set; }

    // Text/Numeric value (Entry binding)
    [ObservableProperty] public partial string StringValue { get; set; } = "0";

    // Toggle value (Switch binding)
    [ObservableProperty] public partial bool BoolValue { get; set; }

    partial void OnSelectedOptionChanged(string? value) => _onValueChanged();
    partial void OnStringValueChanged(string value) => _onValueChanged();
    partial void OnBoolValueChanged(bool value) => _onValueChanged();

    /// <summary>
    /// Returns the resolved typed value to store in <see cref="StateInputValues"/>.
    /// </summary>
    public object? GetResolvedValue() => Definition.FieldType switch
    {
        StateFieldType.Picker => SelectedOption,
        StateFieldType.Toggle => BoolValue,
        StateFieldType.Integer => int.TryParse(StringValue, out var i) ? i : 0,
        StateFieldType.Decimal => decimal.TryParse(StringValue, out var d) ? d : 0m,
        _ => StringValue
    };
}
