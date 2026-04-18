using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PaycheckCalc.Core.Data;
using PaycheckCalc.Core.Models;
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
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.SelfEmployment;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.State.Annual;
using PaycheckCalc.Web;
using PaycheckCalc.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient pointing at the WebAssembly host. Used both by the asset
// loader (to fetch wwwroot/data/*.json) and by anything else that needs
// to hit the same origin.
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// ── Asset loader (browser-side) ─────────────────────────
builder.Services.AddSingleton<ITaxDataAssetLoader, HttpClientTaxDataAssetLoader>();

// ── Pre-load JSON tax tables at startup ────────────────
//
// Blazor WASM is single-threaded, so there is no benefit to deferring
// these reads. Doing them once before the app starts means every calculator
// constructor below is fed JSON synchronously, which mirrors how the MAUI
// head registers them.
//
// We resolve the loader from a temporary host so it picks up the
// HttpClient-backed implementation registered above.
var bootstrapHost = builder.Build();
var bootstrapLoader = bootstrapHost.Services.GetRequiredService<ITaxDataAssetLoader>();

var arJson  = await bootstrapLoader.ReadAllTextAsync("ar_withholding_2026.json");
var okJson  = await bootstrapLoader.ReadAllTextAsync("ok_ow2_2026_percentage.json");
var fedJson = await bootstrapLoader.ReadAllTextAsync("us_irs_15t_2026_percentage_automated.json");
var caJson  = await bootstrapLoader.ReadAllTextAsync("ca_method_b_2026.json");
var coJson  = await bootstrapLoader.ReadAllTextAsync("co_dr0004_2026.json");
var ctJson  = await bootstrapLoader.ReadAllTextAsync("connecticut_withholding_2026.json");
var paJson  = await bootstrapLoader.ReadAllTextAsync("pa_eit_2026.json");
var nycJson = await bootstrapLoader.ReadAllTextAsync("nyc_withholding_2026.json");
var ritaJson = await bootstrapLoader.ReadAllTextAsync("oh_rita_2026.json");
var ccaJson  = await bootstrapLoader.ReadAllTextAsync("oh_cca_2026.json");
var mdJson   = await bootstrapLoader.ReadAllTextAsync("md_county_surtax_2026.json");
var f1040Json = await bootstrapLoader.ReadAllTextAsync("federal_1040_brackets_2026.json");

// Re-create a fresh builder and register everything against it. We can't
// reuse the bootstrap builder because Build() was already called above.
builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddSingleton<ITaxDataAssetLoader, HttpClientTaxDataAssetLoader>();

// ── Calculators ────────────────────────────────────────
builder.Services.AddSingleton(new ArkansasFormulaCalculator(arJson));
builder.Services.AddSingleton(new OklahomaOw2PercentageCalculator(okJson));
builder.Services.AddSingleton(new Irs15TPercentageCalculator(fedJson));
builder.Services.AddSingleton(new CaliforniaPercentageCalculator(caJson));
builder.Services.AddSingleton(new ColoradoWithholdingCalculator(coJson));
builder.Services.AddSingleton(new ConnecticutWithholdingCalculator(ctJson));
builder.Services.AddSingleton(new FicaCalculator());

builder.Services.AddSingleton<StateCalculatorRegistry>(sp =>
{
    var registry = new StateCalculatorRegistry();

    // Alabama — unique filing statuses, dependents, federal withholding
    registry.Register(new AlabamaWithholdingCalculator());

    // Arkansas — DFA formula method with transitional zone brackets
    registry.Register(new ArkansasWithholdingCalculator(sp.GetRequiredService<ArkansasFormulaCalculator>()));

    // California — Method B (EDD DE 44)
    registry.Register(new CaliforniaWithholdingCalculator(sp.GetRequiredService<CaliforniaPercentageCalculator>()));

    // Colorado — flat 4.4% with DR 0004 Table 1 allowance + FMLI
    registry.Register(sp.GetRequiredService<ColoradoWithholdingCalculator>());

    // Connecticut — TPG-211 table-driven withholding calculation rules
    registry.Register(sp.GetRequiredService<ConnecticutWithholdingCalculator>());

    // Delaware — DE W-4, percentage method with personal credits
    registry.Register(new DelawareWithholdingCalculator());

    // Illinois — flat 4.95% with IL-W-4 basic and additional allowances
    registry.Register(new IllinoisWithholdingCalculator());

    // Oklahoma — OW-2 percentage method
    registry.Register(new OklahomaWithholdingCalculator(sp.GetRequiredService<OklahomaOw2PercentageCalculator>()));

    // Pennsylvania — flat 3.07%
    registry.Register(new PennsylvaniaWithholdingCalculator());

    // States with no individual income tax
    UsState[] noTaxStates = [UsState.AK, UsState.FL, UsState.NV, UsState.NH, UsState.SD, UsState.TN, UsState.TX, UsState.WA, UsState.WY];
    foreach (var state in noTaxStates)
        registry.Register(new NoIncomeTaxWithholdingAdapter(state));

    // All remaining states via the annualized percentage method
    foreach (var (state, config) in StateTaxConfigs2026.Configs)
        registry.Register(new PercentageMethodWithholdingAdapter(state, config));

    return registry;
});

// ── Local (sub-state) tax calculators ──────────────────
builder.Services.AddSingleton<LocalCalculatorRegistry>(sp =>
{
    var registry = new LocalCalculatorRegistry();

    // Pennsylvania Act 32 EIT + LST
    registry.Register(new PaEitCalculator(new PaEitRateTable(paJson)));
    registry.Register(new PaLstCalculator());

    // New York City
    registry.Register(new NycWithholdingCalculator(nycJson));

    // Ohio RITA + CCA
    registry.Register(new OhRitaCalculator(ritaJson));
    registry.Register(new OhCcaCalculator(ccaJson));

    // Maryland county surtax
    registry.Register(new MdCountyCalculator(mdJson));

    return registry;
});

builder.Services.AddSingleton<PayCalculator>(sp =>
    new PayCalculator(
        sp.GetRequiredService<StateCalculatorRegistry>(),
        sp.GetRequiredService<FicaCalculator>(),
        sp.GetRequiredService<Irs15TPercentageCalculator>(),
        sp.GetRequiredService<LocalCalculatorRegistry>()));

builder.Services.AddSingleton<AnnualProjectionCalculator>(sp =>
    new AnnualProjectionCalculator(
        sp.GetRequiredService<Irs15TPercentageCalculator>(),
        sp.GetRequiredService<FicaCalculator>()));

// ── Storage — localStorage-backed, JSON shape compatible with MAUI heads ──
builder.Services.AddSingleton<LocalStoragePaycheckRepository>();
builder.Services.AddSingleton<IPaycheckRepository>(sp => sp.GetRequiredService<LocalStoragePaycheckRepository>());

builder.Services.AddSingleton<LocalStorageAnnualScenarioRepository>();
builder.Services.AddSingleton<IAnnualScenarioRepository>(sp => sp.GetRequiredService<LocalStorageAnnualScenarioRepository>());

// ── Self-Employment ────────────────────────────────────
builder.Services.AddSingleton<SelfEmploymentTaxCalculator>(sp =>
    new SelfEmploymentTaxCalculator(sp.GetRequiredService<FicaCalculator>()));
builder.Services.AddSingleton<QbiDeductionCalculator>();
builder.Services.AddSingleton<SelfEmploymentCalculator>(sp =>
    new SelfEmploymentCalculator(
        sp.GetRequiredService<SelfEmploymentTaxCalculator>(),
        sp.GetRequiredService<QbiDeductionCalculator>(),
        sp.GetRequiredService<Irs15TPercentageCalculator>(),
        sp.GetRequiredService<StateCalculatorRegistry>()));

// ── Annual Form 1040 engine ────────────────────────────
builder.Services.AddSingleton(new Federal1040TaxCalculator(f1040Json));
builder.Services.AddSingleton<Schedule1Calculator>();
builder.Services.AddSingleton<AnnualStateTaxCalculator>(sp =>
    new AnnualStateTaxCalculator(sp.GetRequiredService<StateCalculatorRegistry>()));
builder.Services.AddSingleton<Form1040Calculator>(sp =>
    new Form1040Calculator(
        sp.GetRequiredService<Federal1040TaxCalculator>(),
        sp.GetRequiredService<Schedule1Calculator>(),
        sp.GetRequiredService<SelfEmploymentTaxCalculator>(),
        sp.GetRequiredService<QbiDeductionCalculator>(),
        sp.GetRequiredService<FicaCalculator>(),
        stateTax: sp.GetRequiredService<AnnualStateTaxCalculator>()));
builder.Services.AddSingleton<WithholdingSuggestionCalculator>();
builder.Services.AddSingleton<Form1040ESCalculator>();

await builder.Build().RunAsync();
