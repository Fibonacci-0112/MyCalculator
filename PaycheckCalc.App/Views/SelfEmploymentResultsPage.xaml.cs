using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class SelfEmploymentResultsPage : ContentPage
{
    public SelfEmploymentResultsPage(SelfEmploymentViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
