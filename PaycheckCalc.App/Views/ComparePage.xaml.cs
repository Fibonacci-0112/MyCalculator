using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class ComparePage : ContentPage
{
    public ComparePage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
