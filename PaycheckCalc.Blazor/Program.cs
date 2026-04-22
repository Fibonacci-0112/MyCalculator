using PaycheckCalc.Blazor.Components;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Arizona;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.California;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.Connecticut;
using PaycheckCalc.Core.Tax.Delaware;
using PaycheckCalc.Core.Tax.DistrictOfColumbia;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Georgia;
using PaycheckCalc.Core.Tax.Hawaii;
using PaycheckCalc.Core.Tax.Idaho;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Indiana;
using PaycheckCalc.Core.Tax.Iowa;
using PaycheckCalc.Core.Tax.Kansas;
using PaycheckCalc.Core.Tax.Kentucky;
using PaycheckCalc.Core.Tax.Louisiana;
using PaycheckCalc.Core.Tax.Maine;
using PaycheckCalc.Core.Tax.Maryland;
using PaycheckCalc.Core.Tax.Missouri;
using PaycheckCalc.Core.Tax.Mississippi;
using PaycheckCalc.Core.Tax.Minnesota;
using PaycheckCalc.Core.Tax.Montana;
using PaycheckCalc.Core.Tax.Nebraska;
using PaycheckCalc.Core.Tax.NewJersey;
using PaycheckCalc.Core.Tax.NewMexico;
using PaycheckCalc.Core.Tax.NewYork;
using PaycheckCalc.Core.Tax.NorthCarolina;
using PaycheckCalc.Core.Tax.NorthDakota;
using PaycheckCalc.Core.Tax.Ohio;
using PaycheckCalc.Core.Tax.Oregon;
using PaycheckCalc.Core.Tax.RhodeIsland;
using PaycheckCalc.Core.Tax.SouthCarolina;
using PaycheckCalc.Core.Tax.Utah;
using PaycheckCalc.Core.Tax.Vermont;
using PaycheckCalc.Core.Tax.Virginia;
using PaycheckCalc.Core.Tax.Washington;
using PaycheckCalc.Core.Tax.WestVirginia;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.Massachusetts;
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
stateRegistry.Register(new ArizonaWithholdingCalculator());
stateRegistry.Register(new ArkansasWithholdingCalculator(arFormulaCalc));
stateRegistry.Register(new CaliforniaWithholdingCalculator(caPercentCalc));
stateRegistry.Register(coCalc);
stateRegistry.Register(ctCalc);
stateRegistry.Register(new DelawareWithholdingCalculator());
stateRegistry.Register(new DistrictOfColumbiaWithholdingCalculator());
stateRegistry.Register(new GeorgiaWithholdingCalculator());
stateRegistry.Register(new HawaiiWithholdingCalculator());
stateRegistry.Register(new IdahoWithholdingCalculator());
stateRegistry.Register(new IllinoisWithholdingCalculator());
stateRegistry.Register(new IndianaWithholdingCalculator());
stateRegistry.Register(new IowaWithholdingCalculator());
stateRegistry.Register(new KansasWithholdingCalculator());
stateRegistry.Register(new KentuckyWithholdingCalculator());
stateRegistry.Register(new LouisianaWithholdingCalculator());
stateRegistry.Register(new MaineWithholdingCalculator());
stateRegistry.Register(new MarylandWithholdingCalculator());
stateRegistry.Register(new MassachusettsWithholdingCalculator());
stateRegistry.Register(new MichiganWithholdingCalculator());
stateRegistry.Register(new MinnesotaWithholdingCalculator());
stateRegistry.Register(new MississippiWithholdingCalculator());
stateRegistry.Register(new MissouriWithholdingCalculator());
stateRegistry.Register(new MontanaWithholdingCalculator());
stateRegistry.Register(new NebraskaWithholdingCalculator());
stateRegistry.Register(new NewJerseyWithholdingCalculator());
stateRegistry.Register(new NewMexicoWithholdingCalculator());
stateRegistry.Register(new NewYorkWithholdingCalculator());
stateRegistry.Register(new NorthCarolinaWithholdingCalculator());
stateRegistry.Register(new NorthDakotaWithholdingCalculator());
stateRegistry.Register(new OhioWithholdingCalculator());
stateRegistry.Register(new OregonWithholdingCalculator());
stateRegistry.Register(new RhodeIslandWithholdingCalculator());
stateRegistry.Register(new SouthCarolinaWithholdingCalculator());

// Utah — federal W-4 filing statuses (Single/Married), flat 4.5% rate,
// phase-out allowance credit ($450/$900 per allowance for Single/Married,
// phased out at 1.3% of wages above $9,107/$18,213) per Utah Publication 14 (2026)
stateRegistry.Register(new UtahWithholdingCalculator());

// Vermont — W-4VT filing statuses (Single/Married/Head of Household),
// no state standard deduction, $5,400 per W-4VT allowance, and four
// graduated brackets (3.35%/6.60%/7.60%/8.75%) per Vermont Department
// of Taxes BP-55 (2026 Income Tax Withholding Instructions, Tables, and Charts)
stateRegistry.Register(new VermontWithholdingCalculator());

// Virginia — VA-4 filing statuses (Single/Married/Head of Household),
// $8,750 standard deduction for Single and $17,500 for Married/HoH,
// $930 per VA-4 personal exemption, and four graduated brackets
// (2%/3%/5%/5.75% at $0/$3,000/$5,000/$17,000) per the Virginia
// Department of Taxation Employer Withholding Instructions (Pub. 93045, 2026)
stateRegistry.Register(new VirginiaWithholdingCalculator());

// West Virginia — IT-104 filing statuses (Single/Married), no state standard
// deduction, $2,000 per IT-104 personal exemption, and five graduated brackets
// (3%/4%/4.5%/6%/6.5% at $0/$10,000/$25,000/$40,000/$60,000)
// per the WV State Tax Dept. Form IT-104 and WV Code § 11-21-71 (2026)
stateRegistry.Register(new WestVirginiaWithholdingCalculator());
stateRegistry.Register(new OklahomaWithholdingCalculator(okCalc));
stateRegistry.Register(new PennsylvaniaWithholdingCalculator());

// Washington — no income tax; WA Cares Fund (Long-Term Care) at 0.58 %
stateRegistry.Register(new WashingtonWithholdingCalculator());

// No-income-tax states
UsState[] noTaxStates =
[
    UsState.AK, UsState.FL, UsState.NV, UsState.NH,
    UsState.SD, UsState.TN, UsState.TX, UsState.WY
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
