using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class StatePage : ContentPage
{
    public StatePage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
