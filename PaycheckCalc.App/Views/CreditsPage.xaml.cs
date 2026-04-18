using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class CreditsPage : ContentPage
{
    public CreditsPage(CreditsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
