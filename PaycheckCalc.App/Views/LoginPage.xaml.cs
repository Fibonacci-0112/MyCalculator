using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
