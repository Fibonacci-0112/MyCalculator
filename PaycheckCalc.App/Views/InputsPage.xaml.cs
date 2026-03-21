using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class InputsPage : ContentPage
{
    private const int LastTabIndex = 3;

    public InputsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnSwipedLeft(object sender, SwipedEventArgs e)
    {
        if (BindingContext is CalculatorViewModel vm && vm.SelectedInputTab < LastTabIndex)
            vm.SelectedInputTab++;
    }

    private void OnSwipedRight(object sender, SwipedEventArgs e)
    {
        if (BindingContext is CalculatorViewModel vm && vm.SelectedInputTab > 0)
            vm.SelectedInputTab--;
    }
}
