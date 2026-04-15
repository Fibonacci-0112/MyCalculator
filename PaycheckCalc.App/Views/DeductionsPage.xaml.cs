using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class DeductionsPage : ContentPage
{
    public DeductionsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
