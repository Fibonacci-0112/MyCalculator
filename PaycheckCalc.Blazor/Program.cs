using PaycheckCalc.Blazor.Components;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.California;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.Connecticut;
using PaycheckCalc.Core.Tax.Delaware;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Georgia;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.Michigan;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.SelfEmployment;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.State.Annual;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor Web App: Razor Components with interactive Server rendering ──────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Load tax JSON tables from wwwroot/data/ (linked from PaycheckCalc.Core/Data) ──
// In a Blazor Web App the server process can read these directly off disk at
// startup instead of going through HttpClient like the WASM head does.
// Files are copied to the build output's wwwroot/data/ via the csproj's
// <Content Link="..."> items, so resolve from AppContext.BaseDirectory (the
// bin output folder) rather than WebRootPath (which still points at the
// source wwwroot during `dotnet run`).
string DataPath(string filename) =>
    Path.Combine(AppContext.BaseDirectory, "wwwroot", "data", filename);

var irs15tJson = File.ReadAllText(DataPath("us_irs_15t_2026_percentage_automated.json"));
var arJson     = File.ReadAllText(DataPath("ar_withholding_2026.json"));
var okJson     = File.ReadAllText(DataPath("ok_ow2_2026_percentage.json"));
var caJson     = File.ReadAllText(DataPath("ca_method_b_2026.json"));
var coJson     = File.ReadAllText(DataPath("co_dr0004_2026.json"));
var ctJson     = File.ReadAllText(DataPath("connecticut_withholding_2026.json"));
var paEitJson  = File.ReadAllText(DataPath("pa_eit_2026.json"));
var nycJson    = File.ReadAllText(DataPath("nyc_withholding_2026.json"));
var ohRitaJson = File.ReadAllText(DataPath("oh_rita_2026.json"));
var ohCcaJson  = File.ReadAllText(DataPath("oh_cca_2026.json"));
var mdJson     = File.ReadAllText(DataPath("md_county_surtax_2026.json"));
var f1040Json  = File.ReadAllText(DataPath("federal_1040_brackets_2026.json"));

// ── FICA ────────────────────────────────────────────────────────────────────
var fica = new FicaCalculator();
builder.Services.AddSingleton(fica);

// ── Federal withholding (IRS Pub 15-T percentage method) ────────────────────
var irs15t = new Irs15TPercentageCalculator(irs15tJson);
builder.Services.AddSingleton(irs15t);

// ── State withholding registry ──────────────────────────────────────────────
var arFormulaCalc = new ArkansasFormulaCalculator(arJson);
var caPercentCalc = new CaliforniaPercentageCalculator(caJson);
var coCalc        = new ColoradoWithholdingCalculator(coJson);
var ctCalc        = new ConnecticutWithholdingCalculator(ctJson);
var okCalc        = new OklahomaOw2PercentageCalculator(okJson);

var stateRegistry = new StateCalculatorRegistry();

// Dedicated state calculators
stateRegistry.Register(new AlabamaWithholdingCalculator());
stateRegistry.Register(new ArkansasWithholdingCalculator(arFormulaCalc));
stateRegistry.Register(new CaliforniaWithholdingCalculator(caPercentCalc));
stateRegistry.Register(coCalc);
stateRegistry.Register(ctCalc);
stateRegistry.Register(new DelawareWithholdingCalculator());
stateRegistry.Register(new GeorgiaWithholdingCalculator());
stateRegistry.Register(new IllinoisWithholdingCalculator());
stateRegistry.Register(new MichiganWithholdingCalculator());
stateRegistry.Register(new OklahomaWithholdingCalculator(okCalc));
stateRegistry.Register(new PennsylvaniaWithholdingCalculator());

// No-income-tax states
UsState[] noTaxStates =
[
    UsState.AK, UsState.FL, UsState.NV, UsState.NH,
    UsState.SD, UsState.TN, UsState.TX, UsState.WA, UsState.WY
];
foreach (var state in noTaxStates)
    stateRegistry.Register(new NoIncomeTaxWithholdingAdapter(state));

// All remaining states via annualized percentage method
foreach (var (state, config) in StateTaxConfigs2026.Configs)
    stateRegistry.Register(new PercentageMethodWithholdingAdapter(state, config));

builder.Services.AddSingleton(stateRegistry);

// ── Local (sub-state) tax calculators ───────────────────────────────────────
var localRegistry = new LocalCalculatorRegistry();
localRegistry.Register(new PaEitCalculator(new PaEitRateTable(paEitJson)));
localRegistry.Register(new PaLstCalculator());
localRegistry.Register(new NycWithholdingCalculator(nycJson));
localRegistry.Register(new OhRitaCalculator(ohRitaJson));
localRegistry.Register(new OhCcaCalculator(ohCcaJson));
localRegistry.Register(new MdCountyCalculator(mdJson));
builder.Services.AddSingleton(localRegistry);

// ── PayCalculator (main orchestrator) ───────────────────────────────────────
builder.Services.AddSingleton(new PayCalculator(stateRegistry, fica, irs15t, localRegistry));

// ── Annual projection & Form 1040 engine ────────────────────────────────────
builder.Services.AddSingleton(new AnnualProjectionCalculator(irs15t, fica));

var seCalc = new SelfEmploymentTaxCalculator(fica);
builder.Services.AddSingleton(seCalc);
builder.Services.AddSingleton<QbiDeductionCalculator>();
builder.Services.AddSingleton(new SelfEmploymentCalculator(seCalc, new QbiDeductionCalculator(), irs15t, stateRegistry));

var f1040TaxCalc = new Federal1040TaxCalculator(f1040Json);
builder.Services.AddSingleton(f1040TaxCalc);
builder.Services.AddSingleton<Schedule1Calculator>();
builder.Services.AddSingleton(new AnnualStateTaxCalculator(stateRegistry));
builder.Services.AddSingleton(sp =>
    new Form1040Calculator(
        f1040TaxCalc,
        sp.GetRequiredService<Schedule1Calculator>(),
        seCalc,
        sp.GetRequiredService<QbiDeductionCalculator>(),
        fica,
        stateTax: sp.GetRequiredService<AnnualStateTaxCalculator>()));
builder.Services.AddSingleton<WithholdingSuggestionCalculator>();
builder.Services.AddSingleton<Form1040ESCalculator>();

// ── Per-user (per-circuit) in-memory calculator session shared by pages ─────
builder.Services.AddScoped<CalculatorSessionState>();

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
