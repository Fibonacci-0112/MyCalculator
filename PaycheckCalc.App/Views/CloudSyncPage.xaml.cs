using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class CloudSyncPage : ContentPage
{
    private readonly CloudSyncViewModel _vm;

    public CloudSyncPage(CloudSyncViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
