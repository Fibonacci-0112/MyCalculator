using CommunityToolkit.Mvvm.ComponentModel;

namespace PaycheckCalc.App.Services;

/// <summary>
/// Observable singleton holding live sync state — network reachability
/// and the count of operations queued for replay. Pages (e.g.
/// <c>AccountPage</c>) bind to its properties via their view models to
/// show an "X changes pending sync" badge or a "Working offline"
/// banner.
///
/// Mutated by <c>ConnectivityWatcher</c> (when the network goes up/down)
/// and by the <c>Syncing*Repository</c> classes (after every enqueue /
/// flush). Pure state object; doesn't perform any sync work itself.
/// </summary>
public partial class SyncStatus : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    public partial bool IsOnline { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending))]
    [NotifyPropertyChangedFor(nameof(TotalPending))]
    [NotifyPropertyChangedFor(nameof(PendingSummary))]
    public partial int PendingPaycheckOps { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPending))]
    [NotifyPropertyChangedFor(nameof(TotalPending))]
    [NotifyPropertyChangedFor(nameof(PendingSummary))]
    public partial int PendingScenarioOps { get; set; }

    public bool IsOffline => !IsOnline;
    public int TotalPending => PendingPaycheckOps + PendingScenarioOps;
    public bool HasPending => TotalPending > 0;

    public string PendingSummary => TotalPending switch
    {
        0 => "All changes synced.",
        1 => "1 change waiting to sync.",
        _ => $"{TotalPending} changes waiting to sync."
    };
}
