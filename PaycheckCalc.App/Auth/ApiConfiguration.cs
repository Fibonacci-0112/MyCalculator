namespace PaycheckCalc.App.Auth;

/// <summary>
/// Build-time constants that point the MAUI app at the backend API. The
/// Blazor Server hosts both the API and (in production) the user-facing
/// web; the MAUI client talks to the API only.
///
/// Defaults assume a developer machine running the Blazor head locally
/// over HTTPS on port 5001. Android emulators reach the host machine via
/// 10.0.2.2; Windows can use localhost directly.
/// </summary>
public static class ApiConfiguration
{
#if ANDROID
    public const string BaseUrl = "https://10.0.2.2:5001";
#else
    public const string BaseUrl = "https://localhost:5001";
#endif

    public const string ApiHttpClientName = "api";
    public const string AuthHttpClientName = "auth";
}
