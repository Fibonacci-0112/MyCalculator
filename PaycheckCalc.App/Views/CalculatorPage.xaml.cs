using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class CalculatorPage : ContentPage
{
    public CalculatorPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
