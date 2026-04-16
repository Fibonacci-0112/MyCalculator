using PaycheckCalc.App.ViewModels;

namespace PaycheckCalc.App.Views;

public partial class SelfEmploymentPage : ContentPage
{
    private Button _activeTab;
    private readonly ScrollView[] _contentPanels;
    private readonly Button[] _tabButtons;

    private static readonly Color[] TabAccentColors =
    {
        Color.FromArgb("#00897B"),  // Income = Teal
        Color.FromArgb("#3949AB"),  // Tax Info = Indigo
        Color.FromArgb("#7B1FA2"),  // QBI = Purple
    };

    private static readonly Color InactiveTabBackground = Color.FromArgb("#455A64");
    private static readonly Color InactiveTabText = Color.FromArgb("#B0BEC5");

    public SelfEmploymentPage(SelfEmploymentViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        _tabButtons = [TabIncome, TabTaxInfo, TabQbi];
        _contentPanels = [IncomeContent, TaxInfoContent, QbiContent];
        _activeTab = TabIncome;
    }

    private void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button tapped || tapped == _activeTab)
            return;

        var index = Array.IndexOf(_tabButtons, tapped);
        if (index < 0)
            return;

        foreach (var tab in _tabButtons)
        {
            tab.BackgroundColor = InactiveTabBackground;
            tab.TextColor = InactiveTabText;
            tab.FontAttributes = FontAttributes.None;
        }

        tapped.BackgroundColor = TabAccentColors[index];
        tapped.TextColor = Colors.White;
        tapped.FontAttributes = FontAttributes.Bold;

        for (var i = 0; i < _contentPanels.Length; i++)
            _contentPanels[i].IsVisible = i == index;

        _activeTab = tapped;
    }
}
