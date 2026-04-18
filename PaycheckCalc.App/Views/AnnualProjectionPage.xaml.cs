using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class AnnualProjectionPage : ContentPage
{
    public AnnualProjectionPage(AnnualProjectionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Refresh the Projection-bound property in case the user recomputed on Inputs.
        if (BindingContext is AnnualProjectionViewModel vm)
            vm.RefreshCommand.Execute(null);
    }
}
