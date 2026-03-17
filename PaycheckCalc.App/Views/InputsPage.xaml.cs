using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class InputsPage : ContentPage
{
    public InputsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
