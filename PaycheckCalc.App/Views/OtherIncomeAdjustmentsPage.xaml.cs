using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class OtherIncomeAdjustmentsPage : ContentPage
{
    public OtherIncomeAdjustmentsPage(OtherIncomeAdjustmentsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
