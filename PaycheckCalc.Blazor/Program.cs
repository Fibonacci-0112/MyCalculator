using PaycheckCalc.Blazor.Components;
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

// ── Per-user (per-circuit) in-memory calculator session shared by pages ─────
builder.Services.AddScoped<CalculatorSessionState>();
builder.Services.AddScoped<SelfEmploymentSessionState>();
builder.Services.AddScoped<AnnualTaxSessionState>();

// ── Saved-paychecks persistence backed by browser localStorage via JS interop ─
// Scoped because IJSRuntime is scoped per circuit; the repository's in-memory
// cache must match a single browser's localStorage view.
builder.Services.AddScoped<IPaycheckRepository, LocalStoragePaycheckRepository>();
builder.Services.AddScoped<IAnnualScenarioRepository, LocalStorageAnnualScenarioRepository>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
