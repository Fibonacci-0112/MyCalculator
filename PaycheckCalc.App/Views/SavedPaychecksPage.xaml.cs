using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class SavedPaychecksPage : ContentPage
{
    private readonly SavedPaychecksViewModel _vm;

    public SavedPaychecksPage(SavedPaychecksViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadListAsync();
    }

    private async void OnPaycheckTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Guid id)
        {
            await _vm.LoadIntoCalculatorAsync(id);
            await Shell.Current.GoToAsync("//Inputs");
        }
    }

    private async void OnLoadClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
            await _vm.LoadIntoCalculatorAsync(id);
            await Shell.Current.GoToAsync("//Inputs");
        }
    }

    private async void OnCompareClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
            await _vm.SetAsComparisonAsync(id);
            await DisplayAlert("Compare", "Paycheck set as comparison scenario. Go to the Compare page to see it.", "OK");
        }
    }

    private async void OnCompareSelectedClicked(object? sender, EventArgs e)
    {
        await _vm.CompareSelectedCommand.ExecuteAsync(null);
        await Shell.Current.GoToAsync("//Compare");
    }

    private async void OnExportCsvClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
            await _vm.ExportCsvAsync(id);
    }

    private async void OnExportPdfClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
            await _vm.ExportPdfAsync(id);
    }

    private async void OnRenameClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
            var current = _vm.SavedPaychecks.FirstOrDefault(p => p.Id == id);
            var newName = await DisplayPromptAsync(
                "Rename Paycheck",
                "Enter a new name:",
                initialValue: current?.Name ?? "",
                maxLength: 100,
                keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(newName))
                await _vm.RenameWithNameAsync(id, newName.Trim());
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
            var confirmed = await DisplayAlert(
                "Delete Paycheck",
                "Are you sure you want to delete this saved paycheck?",
                "Delete", "Cancel");

            if (confirmed)
                await _vm.DeleteCommand.ExecuteAsync(id);
        }
    }
}
