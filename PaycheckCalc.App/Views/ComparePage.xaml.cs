using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class ComparePage : ContentPage
{
    public ComparePage(CompareViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

