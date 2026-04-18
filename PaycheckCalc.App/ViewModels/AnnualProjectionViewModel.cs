using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Mappers;
using PaycheckCalc.App.Models;
using PaycheckCalc.App.Services;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for the Annual Projection flyout. Surfaces the per-paycheck
/// → annualized projection produced by <c>AnnualProjectionCalculator</c>
/// (populated on <see cref="CalculatorViewModel"/>) and delegates basics
/// editing (tax year, filing status, residence state) to the shared
/// <see cref="AnnualTaxSession"/>.
/// </summary>
public partial class AnnualProjectionViewModel : ObservableObject
{
    private readonly CalculatorViewModel _calculator;

    public AnnualProjectionViewModel(AnnualTaxSession session, CalculatorViewModel calculator)
    {
        Session = session;
        _calculator = calculator;
    }

    public AnnualTaxSession Session { get; }

    /// <summary>Latest per-paycheck annual projection (computed on the Inputs page).</summary>
    public AnnualProjectionModel? Projection => _calculator.Projection;

    /// <summary>True when <see cref="Projection"/> is populated.</summary>
    public bool HasProjection => _calculator.Projection is not null;

    [RelayCommand]
    private void Refresh()
    {
        OnPropertyChanged(nameof(Projection));
        OnPropertyChanged(nameof(HasProjection));
    }

    [RelayCommand]
    private async Task JumpToInputsAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//Inputs");
    }
}
