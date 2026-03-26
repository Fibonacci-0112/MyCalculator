using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class FederalPage : ContentPage
{
    public FederalPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
