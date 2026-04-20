using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Storage;
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
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.State.Annual;
using PaycheckCalc.Core.Tax.SelfEmployment;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Web;
using PaycheckCalc.Web.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient for loading static assets (tax JSON data from wwwroot/data/)
var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// ── Pre-load tax JSON tables via HttpClient ──────────────────────────────────
// All files are served from wwwroot/data/ (linked from PaycheckCalc.Core/Data/).
var irs15tJson  = await http.GetStringAsync("data/us_irs_15t_2026_percentage_automated.json");
var arJson      = await http.GetStringAsync("data/ar_withholding_2026.json");
var okJson      = await http.GetStringAsync("data/ok_ow2_2026_percentage.json");
var caJson      = await http.GetStringAsync("data/ca_method_b_2026.json");
var coJson      = await http.GetStringAsync("data/co_dr0004_2026.json");
var ctJson      = await http.GetStringAsync("data/connecticut_withholding_2026.json");
var paEitJson   = await http.GetStringAsync("data/pa_eit_2026.json");
var nycJson     = await http.GetStringAsync("data/nyc_withholding_2026.json");
var ohRitaJson  = await http.GetStringAsync("data/oh_rita_2026.json");
var ohCcaJson   = await http.GetStringAsync("data/oh_cca_2026.json");
var mdJson      = await http.GetStringAsync("data/md_county_surtax_2026.json");
var f1040Json   = await http.GetStringAsync("data/federal_1040_brackets_2026.json");

// ── FICA ─────────────────────────────────────────────────────────────────────
var fica = new FicaCalculator();
builder.Services.AddSingleton(fica);

// ── Federal withholding (IRS Pub 15-T percentage method) ─────────────────────
var irs15t = new Irs15TPercentageCalculator(irs15tJson);
builder.Services.AddSingleton(irs15t);

// ── State withholding registry ───────────────────────────────────────────────
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
stateRegistry.Register(new OklahomaWithholdingCalculator(okCalc));
stateRegistry.Register(new PennsylvaniaWithholdingCalculator());

// No-income-tax states
UsState[] noTaxStates = [UsState.AK, UsState.FL, UsState.NV, UsState.NH, UsState.SD, UsState.TN, UsState.TX, UsState.WA, UsState.WY];
foreach (var state in noTaxStates)
    stateRegistry.Register(new NoIncomeTaxWithholdingAdapter(state));

// All remaining states via annualized percentage method
foreach (var (state, config) in StateTaxConfigs2026.Configs)
    stateRegistry.Register(new PercentageMethodWithholdingAdapter(state, config));

builder.Services.AddSingleton(stateRegistry);

// ── Local (sub-state) tax calculators ────────────────────────────────────────
var localRegistry = new LocalCalculatorRegistry();
localRegistry.Register(new PaEitCalculator(new PaEitRateTable(paEitJson)));
localRegistry.Register(new PaLstCalculator());
localRegistry.Register(new NycWithholdingCalculator(nycJson));
localRegistry.Register(new OhRitaCalculator(ohRitaJson));
localRegistry.Register(new OhCcaCalculator(ohCcaJson));
localRegistry.Register(new MdCountyCalculator(mdJson));
builder.Services.AddSingleton(localRegistry);

// ── PayCalculator (main orchestrator) ────────────────────────────────────────
builder.Services.AddSingleton(new PayCalculator(stateRegistry, fica, irs15t, localRegistry));

// ── Annual projection ─────────────────────────────────────────────────────────
builder.Services.AddSingleton(new AnnualProjectionCalculator(irs15t, fica));

// ── Self-employment calculators ───────────────────────────────────────────────
var seCalc = new SelfEmploymentTaxCalculator(fica);
builder.Services.AddSingleton(seCalc);
builder.Services.AddSingleton<QbiDeductionCalculator>();
builder.Services.AddSingleton(new SelfEmploymentCalculator(seCalc, new QbiDeductionCalculator(), irs15t, stateRegistry));

// ── Annual Form 1040 engine ───────────────────────────────────────────────────
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

// ── Persistence ───────────────────────────────────────────────────────────────
// Blazored.LocalStorage registers ILocalStorageService as scoped; use AddScoped
// for LocalStoragePaycheckRepository so the scoped dependency resolves correctly.
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<IPaycheckRepository, LocalStoragePaycheckRepository>();

await builder.Build().RunAsync();
