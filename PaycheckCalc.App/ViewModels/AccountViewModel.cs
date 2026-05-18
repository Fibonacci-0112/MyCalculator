using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Auth;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for <c>AccountPage</c>. Shows the signed-in user and offers
/// a Sign Out button. When the user signs out, <see cref="AuthTokenStore.ClearAsync"/>
/// fires <c>UserChanged</c>, which invalidates per-user repository caches
/// so the next saved-paychecks read returns the anonymous folder's
/// contents (typically empty).
/// </summary>
public partial class AccountViewModel : ObservableObject
{
    private readonly AuthTokenStore _tokens;
    private readonly MauiUserContext _userContext;

    public AccountViewModel(AuthTokenStore tokens, MauiUserContext userContext)
    {
        _tokens = tokens;
        _userContext = userContext;
    }

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
