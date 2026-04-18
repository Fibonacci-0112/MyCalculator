using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class WhatIfPage : ContentPage
{
    public WhatIfPage(WhatIfViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
