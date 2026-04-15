using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class PayHoursPage : ContentPage
{
    public PayHoursPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
