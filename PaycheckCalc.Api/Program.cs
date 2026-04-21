using System.Text.Json.Serialization;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.California;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.Connecticut;
using PaycheckCalc.Core.Tax.Delaware;
using PaycheckCalc.Core.Tax.DistrictOfColumbia;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Georgia;
using PaycheckCalc.Core.Tax.Hawaii;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers + JSON: serialize enums by name so the Angular client sees
//    stable strings ("Biweekly", "OK") rather than numeric ordinals. ─────────
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// ── CORS for the Angular dev server (ng serve defaults to :4200). ──────────
//    In production the Angular app is typically served from the same origin,
//    but during development the two projects run on different ports.
const string AngularDevCorsPolicy = "AngularDev";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? new[] { "http://localhost:4200", "https://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Load tax JSON tables from the API's build output /data/ folder
//    (content-linked from PaycheckCalc.Core/Data via the .csproj). ───────────
string DataPath(string filename) =>
    Path.Combine(AppContext.BaseDirectory, "data", filename);

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

// ── FICA ────────────────────────────────────────────────────────────────────
var fica = new FicaCalculator();
builder.Services.AddSingleton(fica);

// ── Federal withholding (IRS Pub 15-T percentage method) ────────────────────
var irs15t = new Irs15TPercentageCalculator(irs15tJson);
builder.Services.AddSingleton(irs15t);

// ── State withholding registry (mirrors PaycheckCalc.Blazor/Program.cs) ────
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
stateRegistry.Register(new DistrictOfColumbiaWithholdingCalculator());
stateRegistry.Register(new GeorgiaWithholdingCalculator());
stateRegistry.Register(new HawaiiWithholdingCalculator());
stateRegistry.Register(new IllinoisWithholdingCalculator());
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

// All remaining states via the annualized percentage method
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(AngularDevCorsPolicy);
app.MapControllers();

app.Run();
