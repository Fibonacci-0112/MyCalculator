using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Auth;
using PaycheckCalc.App.Services;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for <c>AccountPage</c>. Shows the signed-in user, a Sign
/// Out button, and the live <see cref="SyncStatus"/> (online state +
/// count of operations waiting to sync). The SyncStatus instance is
/// exposed directly so the XAML can bind to its observable properties.
/// </summary>
public partial class AccountViewModel : ObservableObject
{
    private readonly AuthTokenStore _tokens;
    private readonly MauiUserContext _userContext;

    public AccountViewModel(AuthTokenStore tokens, MauiUserContext userContext, SyncStatus syncStatus)
    {
        _tokens = tokens;
        _userContext = userContext;
        SyncStatus = syncStatus;
    }

    public SyncStatus SyncStatus { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedOut))]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty] public partial string? CurrentEmail { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusMessage { get; set; }

    public bool IsSignedOut => !IsSignedIn;
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public async Task RefreshAsync()
    {
        IsSignedIn = await _userContext.IsAuthenticatedAsync();
        CurrentEmail = await _userContext.GetCurrentEmailAsync();
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _tokens.ClearAsync();
        StatusMessage = "Signed out.";
        await RefreshAsync();
        await Shell.Current.GoToAsync("//Login");
    }

    [RelayCommand]
    private async Task GoToSignInAsync()
    {
        await Shell.Current.GoToAsync("//Login");
    }
}
