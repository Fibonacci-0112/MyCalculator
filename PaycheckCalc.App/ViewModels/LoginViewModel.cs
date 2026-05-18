using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.ViewModels;

/// <summary>
/// View model for <c>LoginPage</c>. Handles sign-in and one-tap
/// registration against the Identity API endpoints on the Blazor server.
/// On success, stores tokens in <see cref="AuthTokenStore"/> (which raises
/// <c>UserChanged</c>, invalidating per-user repository caches), kicks
/// off an auto-sync that pulls the server's saved-paychecks /
/// annual-scenarios into the local cache, and navigates to the
/// Dashboard.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthApiClient _api;
    private readonly AuthTokenStore _tokens;
    private readonly IPaycheckRepository _paycheckRepo;
    private readonly IAnnualScenarioRepository _scenarioRepo;

    public LoginViewModel(
        AuthApiClient api,
        AuthTokenStore tokens,
        IPaycheckRepository paycheckRepo,
        IAnnualScenarioRepository scenarioRepo)
    {
        _api = api;
        _tokens = tokens;
        _paycheckRepo = paycheckRepo;
        _scenarioRepo = scenarioRepo;
    }

    [ObservableProperty] public partial string Email { get; set; } = "";
    [ObservableProperty] public partial string Password { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        await RunAuthAsync(() => _api.LoginAsync(Email, Password));
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        await RunAuthAsync(() => _api.RegisterAsync(Email, Password));
    }

    private async Task RunAuthAsync(Func<Task<AuthResult>> attempt)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await attempt();
            if (!result.IsSuccess || result.Tokens is null)
            {
                ErrorMessage = result.Error ?? "Sign-in failed.";
                return;
            }

            await _tokens.SaveAsync(result.Tokens);
            Password = "";

            // Auto-sync in the background — pulls server state into the
            // local cache and flushes any pending ops. Fire-and-forget so
            // the user lands on the Dashboard immediately; failures are
            // retried on the next read.
            _ = AutoSyncAsync();

            await Shell.Current.GoToAsync("//Dashboard");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not reach the server: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "The request timed out. Check your connection and try again.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AutoSyncAsync()
    {
        try { await _paycheckRepo.GetAllAsync(); } catch { }
        try { await _scenarioRepo.GetAllAsync(); } catch { }
    }
}
