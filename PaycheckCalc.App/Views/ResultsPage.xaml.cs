using PaycheckCalc.App.Controls;
using PaycheckCalc.App.Helpers;
using PaycheckCalc.App.ViewModels;
using System.ComponentModel;

namespace PaycheckCalc.App.Views;

public partial class ResultsPage : ContentPage
{
    private readonly CalculatorViewModel _vm;
    private readonly DoughnutChartDrawable _chartDrawable = new();

    public ResultsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;

        DoughnutChart.Drawable = _chartDrawable;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdateChart();
    }

    protected override void OnDisappearing()
    {
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalculatorViewModel.ResultCard))
            UpdateChart();
    }

    private void UpdateChart()
    {
        _chartDrawable.Result = _vm.ResultCard;
        DoughnutChart.Invalidate();
    }

    private async void OnSavePaycheckClicked(object? sender, EventArgs e)
    {
        if (_vm.HasLoadedPaycheck)
        {
            // Overwrite the currently loaded paycheck
            await _vm.SaveCurrentAsync();
            await DisplayAlert("Saved", $"Paycheck \"{_vm.LoadedPaycheckName}\" updated.", "OK");
        }
        else
        {
            // Prompt for a name and create a new entry
            var stateName = EnumDisplay.UsStateName(_vm.SelectedState.ToString());
            var defaultName = $"Paycheck - {stateName} - {DateTime.Now:MMM d}";

            var name = await DisplayPromptAsync(
                "Save Paycheck",
                "Enter a name for this paycheck:",
                initialValue: defaultName,
                maxLength: 100,
                keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(name))
            {
                await _vm.SaveWithNameAsync(name.Trim());
                await DisplayAlert("Saved", $"Paycheck \"{name.Trim()}\" saved.", "OK");
            }
        }
    }

    private async void OnSaveAsNewClicked(object? sender, EventArgs e)
    {
        // Clear loaded ID so the next save creates a new entry, then prompt
        _vm.ClearLoadedPaycheckCommand.Execute(null);

        var stateName = EnumDisplay.UsStateName(_vm.SelectedState.ToString());
        var defaultName = $"Paycheck - {stateName} - {DateTime.Now:MMM d}";

        var name = await DisplayPromptAsync(
            "Save as New Paycheck",
            "Enter a name for this paycheck:",
            initialValue: defaultName,
            maxLength: 100,
            keyboard: Keyboard.Text);

        if (!string.IsNullOrWhiteSpace(name))
        {
            await _vm.SaveWithNameAsync(name.Trim());
            await DisplayAlert("Saved", $"Paycheck \"{name.Trim()}\" saved.", "OK");
        }
    }
}
