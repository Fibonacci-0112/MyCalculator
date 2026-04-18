using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class QuarterlyEstimatesPage : ContentPage
{
    public QuarterlyEstimatesPage(QuarterlyEstimatesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
