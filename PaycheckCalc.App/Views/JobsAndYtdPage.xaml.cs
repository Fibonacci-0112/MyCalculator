using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class JobsAndYtdPage : ContentPage
{
    public JobsAndYtdPage(JobsAndYtdViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is JobsAndYtdViewModel vm)
            vm.RebuildSummaryCommand.Execute(null);
    }
}
