using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Singleton VM holds state between visits; refresh on every appearance
        // so the user sees the dashboard reflect any paycheck saved in another
        // tab.
        await _vm.LoadAsync();
    }
}
