using PaycheckCalc.App.Services;

namespace PaycheckCalc.App;

public partial class AppShell : Shell
{
    // ConnectivityWatcher is held to keep the singleton alive for the process
    // lifetime — it subscribes to Connectivity.ConnectivityChanged in its
    // constructor, and if nothing held a reference to it, the GC could collect
    // it and silently stop draining the pending-ops queue on reconnect.
#pragma warning disable IDE0052
    private readonly ConnectivityWatcher _connectivityWatcher;
#pragma warning restore IDE0052

    public AppShell(ConnectivityWatcher connectivityWatcher)
    {
        InitializeComponent();
        _connectivityWatcher = connectivityWatcher;
    }
}
