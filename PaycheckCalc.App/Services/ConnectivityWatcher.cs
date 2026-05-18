using Microsoft.Maui.Networking;
using PaycheckCalc.App.Storage;

namespace PaycheckCalc.App.Services;

/// <summary>
/// Singleton service that mirrors the device's network state into
/// <see cref="SyncStatus"/> and kicks the syncing repositories' flush
/// methods whenever connectivity is restored. Instantiated on app
/// startup (resolved by AppShell) so the subscription is alive for the
/// process lifetime.
///
/// On Android this responds to wifi/cellular state changes; on Windows
/// (unpackaged) it works against Connectivity.NetworkAccess which only
/// detects the obvious "no network" case but not captive portals — good
/// enough for V1.
/// </summary>
public sealed class ConnectivityWatcher : IDisposable
{
    private readonly SyncStatus _status;
    private readonly SyncingPaycheckRepository _paycheckRepo;
    private readonly SyncingAnnualScenarioRepository _scenarioRepo;
    private bool _disposed;

    public ConnectivityWatcher(
        SyncStatus status,
        SyncingPaycheckRepository paycheckRepo,
        SyncingAnnualScenarioRepository scenarioRepo)
    {
        _status = status;
        _paycheckRepo = paycheckRepo;
        _scenarioRepo = scenarioRepo;

        _status.IsOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var nowOnline = e.NetworkAccess == NetworkAccess.Internet;
        var wasOnline = _status.IsOnline;
        _status.IsOnline = nowOnline;

        if (nowOnline && !wasOnline)
        {
            // Network just came back — drain pending writes. Failures
            // leave them in the queue for the next reconnect.
            try { await _paycheckRepo.FlushPendingAsync(); } catch { }
            try { await _scenarioRepo.FlushPendingAsync(); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }
}
