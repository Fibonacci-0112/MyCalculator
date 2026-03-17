using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class ResultsPage : ContentPage
{
    public ResultsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
