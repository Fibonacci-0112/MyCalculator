using PaycheckCalc.App.Controls;
using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class ResultsPage : ContentPage
{
    private readonly CalculatorViewModel _vm;
    private readonly DoughnutChartDrawable _chartDrawable = new();

    public ResultsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;

        DoughnutChart.Drawable = _chartDrawable;

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CalculatorViewModel.Result))
                UpdateChart();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateChart();
    }

    private void UpdateChart()
    {
        _chartDrawable.Result = _vm.Result;
        DoughnutChart.Invalidate();
    }
}
