using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace PaycheckCalc.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => PaycheckCalc.App.MauiProgram.CreateMauiApp();
}
