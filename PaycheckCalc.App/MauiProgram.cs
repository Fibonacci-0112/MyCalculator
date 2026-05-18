using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using PaycheckCalc.App.Auth;
using PaycheckCalc.App.Services;
using PaycheckCalc.App.Storage;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.App.Views;
using PaycheckCalc.Core.DependencyInjection;
using PaycheckCalc.Core.Geocoding;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── PaycheckCalc.Core wiring (state/local/federal calculators, registries,
        //    schema provider, tax JSON tables). MAUI reads the JSON from the app
        //    package via FileSystem.OpenAppPackageFileAsync.
        builder.Services.AddPaycheckCalcCore(new MauiAppPackageTaxDataReader());

        // ── Address / geocoding / jurisdiction services ────────
        builder.Services.AddSingleton<IAddressService, AddressService>();
        builder.Services.AddSingleton<IGeocodingCache, InMemoryGeocodingCache>();
        builder.Services.AddSingleton<IGoogleMapsApiKeyProvider, SecureStorageGoogleMapsApiKeyProvider>();
        builder.Services.AddSingleton<HttpClient>(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        });
        builder.Services.AddSingleton<IGeocodingService, GoogleMapsGeocodingService>();
        builder.Services.AddSingleton<IJurisdictionService, JurisdictionResolver>();

        // ── Auth: SecureStorage-backed token store + user context, plus
        //    typed API/auth HttpClients. The "api" client carries the bearer
        //    token via AuthenticatingHttpHandler; the "auth" client is plain
        //    so login/register/refresh aren't circular.
        builder.Services.AddSingleton<AuthTokenStore>();
        builder.Services.AddSingleton<MauiUserContext>();
        builder.Services.AddTransient<AuthenticatingHttpHandler>();
        builder.Services.AddSingleton<AuthApiClient>();
        builder.Services.AddHttpClient(ApiConfiguration.AuthHttpClientName, client =>
        {
            client.BaseAddress = new Uri(ApiConfiguration.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        builder.Services.AddHttpClient(ApiConfiguration.ApiHttpClientName, client =>
        {
            client.BaseAddress = new Uri(ApiConfiguration.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
        })
        .AddHttpMessageHandler<AuthenticatingHttpHandler>();

        // ── Per-user persistence. The IPaycheckRepository / IAnnualScenarioRepository
        //    interfaces resolve to the syncing wrappers, which compose the
        //    HTTP repos with user-scoped JSON file caches plus an
        //    offline-write pending-ops queue. Reads prefer remote; writes
        //    go cache-first then push; failed pushes are queued for replay
        //    by the ConnectivityWatcher when network is restored.
        builder.Services.AddSingleton(sp =>
            new JsonPaycheckRepository(FileSystem.AppDataDirectory, sp.GetRequiredService<MauiUserContext>()));
        builder.Services.AddSingleton(sp =>
            new JsonAnnualScenarioRepository(FileSystem.AppDataDirectory, sp.GetRequiredService<MauiUserContext>()));
        builder.Services.AddSingleton(sp =>
            new PendingPaycheckQueue(FileSystem.AppDataDirectory, sp.GetRequiredService<MauiUserContext>()));
        builder.Services.AddSingleton(sp =>
            new PendingAnnualScenarioQueue(FileSystem.AppDataDirectory, sp.GetRequiredService<MauiUserContext>()));
        builder.Services.AddSingleton<HttpPaycheckRepository>();
        builder.Services.AddSingleton<HttpAnnualScenarioRepository>();
        builder.Services.AddSingleton<SyncingPaycheckRepository>();
        builder.Services.AddSingleton<SyncingAnnualScenarioRepository>();
        builder.Services.AddSingleton<IPaycheckRepository>(sp => sp.GetRequiredService<SyncingPaycheckRepository>());
        builder.Services.AddSingleton<IAnnualScenarioRepository>(sp => sp.GetRequiredService<SyncingAnnualScenarioRepository>());

        // ── Sync UX: SyncStatus is the live observable bound by AccountPage
        //    to render "Working offline" / "N changes pending" banners.
        //    ConnectivityWatcher mirrors network state into SyncStatus and
        //    drains the pending queues when the network is restored. It is
        //    held by AppShell so the subscription stays alive for the
        //    process lifetime.
        builder.Services.AddSingleton<SyncStatus>();
        builder.Services.AddSingleton<ConnectivityWatcher>();

        // Shared annual state consumed by every Phase 8 flyout view-model.
        builder.Services.AddSingleton<AnnualTaxSession>();

        // Shared paycheck multi-scenario compare session.
        builder.Services.AddSingleton<ComparisonSession>();

        builder.Services.AddSingleton<CalculatorViewModel>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<SavedPaychecksViewModel>();
        builder.Services.AddSingleton<CompareViewModel>();
        builder.Services.AddSingleton<SelfEmploymentViewModel>();
        builder.Services.AddSingleton<AnnualTaxViewModel>();
        builder.Services.AddSingleton<AnnualProjectionViewModel>();
        builder.Services.AddSingleton<JobsAndYtdViewModel>();
        builder.Services.AddSingleton<OtherIncomeAdjustmentsViewModel>();
        builder.Services.AddSingleton<CreditsViewModel>();
        builder.Services.AddSingleton<QuarterlyEstimatesViewModel>();
        builder.Services.AddSingleton<WhatIfViewModel>();
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<AccountViewModel>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<InputsPage>();
        builder.Services.AddSingleton<PayHoursPage>();
        builder.Services.AddSingleton<FederalPage>();
        builder.Services.AddSingleton<StatePage>();
        builder.Services.AddSingleton<DeductionsPage>();
        builder.Services.AddSingleton<ResultsPage>();
        builder.Services.AddSingleton<ComparePage>();
        builder.Services.AddSingleton<SavedPaychecksPage>();
        builder.Services.AddSingleton<SelfEmploymentPage>();
        builder.Services.AddSingleton<SelfEmploymentResultsPage>();
        builder.Services.AddSingleton<AnnualProjectionPage>();
        builder.Services.AddSingleton<JobsAndYtdPage>();
        builder.Services.AddSingleton<OtherIncomeAdjustmentsPage>();
        builder.Services.AddSingleton<CreditsPage>();
        builder.Services.AddSingleton<QuarterlyEstimatesPage>();
        builder.Services.AddSingleton<WhatIfPage>();
        builder.Services.AddSingleton<AnnualTaxResultsPage>();
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddSingleton<AccountPage>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
