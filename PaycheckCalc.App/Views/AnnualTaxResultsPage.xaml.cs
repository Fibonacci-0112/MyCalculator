using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class AnnualTaxResultsPage : ContentPage
{
    public AnnualTaxResultsPage(AnnualTaxViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Phase 8 split the old monolithic Annual Tax page into per-flyout
        // input pages (Jobs & YTD, Other Income & Adjustments, Credits,
        // Quarterly Estimates, What-If) that write to the shared
        // AnnualTaxSession but do not have their own Calculate buttons.
        // Re-run Form 1040 here so the results reflect whatever the user
        // has most recently entered on any of those input flyouts. Use
        // Recalculate (not CalculateCommand) so we don't re-navigate back
        // to this same page.
        if (BindingContext is AnnualTaxViewModel vm)
            vm.Recalculate();
    }
}
