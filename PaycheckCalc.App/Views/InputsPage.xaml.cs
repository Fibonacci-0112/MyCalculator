using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class InputsPage : ContentPage
{
    private Button _activeTab;
    private readonly ScrollView[] _contentPanels;
    private readonly Button[] _tabButtons;

    // Per-tab accent colors: Pay & Hours = Teal, Federal = Indigo, State = Purple, Deductions = Deep Orange
    private static readonly Color[] TabAccentColors =
    {
        Color.FromArgb("#00897B"),
        Color.FromArgb("#3949AB"),
        Color.FromArgb("#7B1FA2"),
        Color.FromArgb("#E64A19"),
    };

    private static readonly Color InactiveTabBackground = Color.FromArgb("#455A64");
    private static readonly Color InactiveTabText = Color.FromArgb("#B0BEC5");

    public InputsPage(CalculatorViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        _tabButtons = [TabPayHours, TabFederal, TabState, TabDeductions];
        _contentPanels = [PayHoursContent, FederalContent, StateContent, DeductionsContent];
        _activeTab = TabPayHours;
    }

    private void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button tapped || tapped == _activeTab)
            return;

        var index = Array.IndexOf(_tabButtons, tapped);
        if (index < 0)
            return;

        // Reset all tabs to inactive style
        foreach (var tab in _tabButtons)
        {
            tab.BackgroundColor = InactiveTabBackground;
            tab.TextColor = InactiveTabText;
            tab.FontAttributes = FontAttributes.None;
        }

        // Activate selected tab with its accent color
        tapped.BackgroundColor = TabAccentColors[index];
        tapped.TextColor = Colors.White;
        tapped.FontAttributes = FontAttributes.Bold;

        // Toggle content visibility
        for (var i = 0; i < _contentPanels.Length; i++)
            _contentPanels[i].IsVisible = i == index;

        _activeTab = tapped;
    }
}
