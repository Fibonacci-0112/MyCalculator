using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Services;
using PaycheckCalc.CloudSync;

namespace PaycheckCalc.App.ViewModels;

public partial class CloudSyncViewModel : ObservableObject
{
    private readonly CloudSyncOptions _options;
    private readonly ISyncTokenProvider _tokenProvider;

    public CloudSyncViewModel(CloudSyncOptions options, ISyncTokenProvider tokenProvider)
    {
        _options = options;
        _tokenProvider = tokenProvider;
        IsCloudSyncEnabled = options.Enabled;
        ConnectionString = options.ConnectionString;
    }

    [ObservableProperty] public partial bool IsCloudSyncEnabled { get; set; }
    [ObservableProperty] public partial string ConnectionString { get; set; } = "";
    [ObservableProperty] public partial string? SyncToken { get; set; }
    [ObservableProperty] public partial string TokenInput { get; set; } = "";
    [ObservableProperty] public partial string? StatusMessage { get; set; }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string? value) =>
        OnPropertyChanged(nameof(HasStatusMessage));

    public async Task LoadAsync()
    {
        SyncToken = await _tokenProvider.GetOrCreateTokenAsync();
    }

    [RelayCommand]
    private async Task CopyTokenAsync()
    {
        if (SyncToken is null) return;
        await Clipboard.Default.SetTextAsync(SyncToken);
        StatusMessage = "Sync token copied to clipboard.";
    }

    [RelayCommand]
    private async Task SetTokenAsync()
    {
        var t = TokenInput.Trim();
        if (string.IsNullOrWhiteSpace(t)) return;
        await _tokenProvider.SetTokenAsync(t);
        SyncToken = t;
        TokenInput = "";
        StatusMessage = "Token updated. Restart the app to reconnect.";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        _options.Enabled = IsCloudSyncEnabled;
        _options.ConnectionString = ConnectionString;
        await MauiCloudSyncOptionsProvider.SaveAsync(_options);
        StatusMessage = "Settings saved. Restart the app for changes to take effect.";
    }
}
