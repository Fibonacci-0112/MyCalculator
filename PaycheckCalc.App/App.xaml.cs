namespace PaycheckCalc.App;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new Window(_shell)
        {
            Width = 800,
            Height = 800,
        };
}
