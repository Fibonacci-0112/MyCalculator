using CommunityToolkit.Mvvm.ComponentModel;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// ViewModel for a single deduction item in the deductions collection.
/// The UI binds to these properties to render editable deduction rows.
/// </summary>
public partial class DeductionItemViewModel : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial decimal Amount { get; set; }
    [ObservableProperty] public partial DeductionType Type { get; set; }
    [ObservableProperty] public partial bool ReducesStateTaxableWages { get; set; } = true;

    public IReadOnlyList<DeductionType> DeductionTypes { get; } =
        Enum.GetValues<DeductionType>().ToList();

    /// <summary>
    /// Maps this view model to a core <see cref="Deduction"/> domain object.
    /// </summary>
    public Deduction ToDeduction() => new()
    {
        Name = Name,
        Type = Type,
        Amount = Amount,
        ReducesStateTaxableWages = ReducesStateTaxableWages
    };
}
