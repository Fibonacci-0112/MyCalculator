using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class AnnualTaxResultsPage : ContentPage
{
    public AnnualTaxResultsPage(AnnualTaxViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
