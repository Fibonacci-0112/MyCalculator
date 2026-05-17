using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Components;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Endpoints;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.DependencyInjection;
using PaycheckCalc.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor Web App: Razor Components with interactive Server rendering ──────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
//    HttpContextUserContext is the default (used by API endpoints).
//    Blazor scoped components/services can request CircuitUserContext
//    explicitly when they need it; in Phase 2 we'll wire CircuitUserContext
//    as the IUserContext for component-side EF access.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CircuitUserContext>();
builder.Services.AddScoped<IUserContext, HttpContextUserContext>();

// ── Per-user (per-circuit) in-memory calculator session shared by pages ─────
builder.Services.AddScoped<CalculatorSessionState>();
builder.Services.AddScoped<SelfEmploymentSessionState>();
builder.Services.AddScoped<AnnualTaxSessionState>();

// ── Saved-paychecks persistence backed by browser localStorage via JS interop.
//    Phase 1 leaves this in place; Phase 2 will swap to the EF repo and remove
//    the LocalStorage* classes after the import flow ships.
builder.Services.AddScoped<IPaycheckRepository, LocalStoragePaycheckRepository>();
builder.Services.AddScoped<IAnnualScenarioRepository, LocalStorageAnnualScenarioRepository>();

// ── EF-backed repositories — registered as concrete types in Phase 1 so the
//    new API endpoints can use them without disturbing the Blazor UI's
//    existing IPaycheckRepository wiring.
builder.Services.AddScoped<EfPaycheckRepository>();
builder.Services.AddScoped<EfAnnualScenarioRepository>();
builder.Services.AddScoped<EfSessionStateRepository>();
builder.Services.AddScoped<EfUserPreferencesRepository>();

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

// ── Apply migrations on startup in development so the SQLite file is
//    created and up to date without a manual `dotnet ef database update`.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
