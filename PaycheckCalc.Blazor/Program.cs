using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Components;
using PaycheckCalc.Blazor.Components.Account;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Endpoints;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.DependencyInjection;
using PaycheckCalc.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor Web App: Razor Components with interactive Server rendering ──────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Surfaces the AuthenticationState cascade to every component so
// <AuthorizeView> and [Authorize] work everywhere without per-page wiring.
builder.Services.AddCascadingAuthenticationState();

// ── PaycheckCalc.Core wiring (state/local/federal calculators, registries,
//    schema provider, tax JSON tables). The JSON files live in wwwroot/data/,
//    linked from PaycheckCalc.Core/Data/ via this project's csproj.
builder.Services.AddPaycheckCalcCore(new WwwrootTaxDataReader());

// ── EF Core + SQLite database for users + per-user persistence ──────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=paycheckcalc.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// ── ASP.NET Core Identity with two auth schemes:
//    - Cookie scheme for the Blazor UI (IdentityConstants.ApplicationScheme).
//    - Bearer token scheme for the MAUI client (IdentityConstants.BearerScheme),
//      surfaced via MapIdentityApi<TUser>().
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// External providers — Google + Microsoft.
// Client ids/secrets live in user-secrets for dev and a real secret store
// in prod (Azure App Configuration / env vars / Key Vault).
// Use `dotnet user-secrets set "Authentication:Google:ClientId" "..."` etc.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret))
{
    builder.Services.AddAuthentication().AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });
}

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Email confirmation is off for V1; the email sender plumbing is
        // a follow-up. Identity API endpoints still work without it.
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddApiEndpoints();

// Default policy accepts both the cookie scheme (for Blazor UI) and the
// bearer scheme (for the MAUI client). Without this, .RequireAuthorization()
// would only honor the cookie scheme and bearer tokens would be ignored.
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
            IdentityConstants.ApplicationScheme,
            IdentityConstants.BearerScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// ── User-context resolution.
//    CompositeUserContext works in both Blazor circuit scopes (via
//    AuthenticationStateProvider) and API endpoint scopes (via
//    HttpContext.User). One implementation for both surfaces.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, CompositeUserContext>();

// Replace Blazor Server's default AuthenticationStateProvider with one that
// revalidates the cached identity against the Identity store every 30 min
// — disabled / deleted users get signed out automatically.
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// ── Per-user (per-circuit) in-memory calculator session shared by pages ─────
builder.Services.AddScoped<CalculatorSessionState>();
builder.Services.AddScoped<SelfEmploymentSessionState>();
builder.Services.AddScoped<AnnualTaxSessionState>();

// Subscribes to AuthenticationStateChanged for the circuit and resets all
// three session states when the authenticated user changes. Injected into
// MainLayout to force instantiation on every circuit. The class for the
// legacy browser-localStorage repos remains in Services/ until the Phase 5
// importer ships — it's just not DI-registered anymore.
builder.Services.AddScoped<SessionStateLifecycle>();

// ── Per-user persistence (database is the source of truth).
builder.Services.AddScoped<IPaycheckRepository, EfPaycheckRepository>();
builder.Services.AddScoped<IAnnualScenarioRepository, EfAnnualScenarioRepository>();
builder.Services.AddScoped<ISessionStateRepository, EfSessionStateRepository>();
builder.Services.AddScoped<IUserPreferencesRepository, EfUserPreferencesRepository>();

// Phase 5: one-shot importer that copies pre-account browser localStorage
// data into the signed-in user's account. The LocalStorage* classes were
// kept across Phase 2 specifically so this importer can read the legacy
// keys via the same JsonSerializerOptions they wrote with. Surfaced on
// SavedPaychecks.razor via <LegacyImportBanner />.
builder.Services.AddScoped<LegacyDataImporter>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// ── Identity API endpoints for the MAUI client (bearer tokens).
//    Exposes /register, /login, /refresh, /confirmEmail, /resendConfirmationEmail,
//    /forgotPassword, /resetPassword, /manage/2fa, /manage/info under /api/auth.
app.MapGroup("/api/auth")
   .MapIdentityApi<ApplicationUser>()
   .WithTags("Auth");

// ── Per-feature API endpoints (all RequireAuthorization()).
app.MapPaycheckEndpoints();
app.MapAnnualScenarioEndpoints();
app.MapSessionEndpoints();
app.MapPreferencesEndpoints();

// ── Form-bound cookie auth handlers for the Blazor /account/login,
//    /account/register, /account/logout, and external-provider buttons.
app.MapAccountEndpoints();

// ── Apply migrations on startup in development so the SQLite file is
//    created and up to date without a manual `dotnet ef database update`.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
