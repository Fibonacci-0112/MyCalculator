using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using PaycheckCalc.App.Services;
using PaycheckCalc.App.Storage;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.App.Views;
using PaycheckCalc.Core.Geocoding;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Storage;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Arizona;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.California;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.Connecticut;
using PaycheckCalc.Core.Tax.Delaware;
using PaycheckCalc.Core.Tax.DistrictOfColumbia;
using PaycheckCalc.Core.Tax.Georgia;
using PaycheckCalc.Core.Tax.Hawaii;
using PaycheckCalc.Core.Tax.Idaho;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Indiana;
using PaycheckCalc.Core.Tax.Iowa;
using PaycheckCalc.Core.Tax.Kansas;
using PaycheckCalc.Core.Tax.Kentucky;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Louisiana;
using PaycheckCalc.Core.Tax.Maine;
using PaycheckCalc.Core.Tax.Maryland;
using PaycheckCalc.Core.Tax.Missouri;
using PaycheckCalc.Core.Tax.Mississippi;
using PaycheckCalc.Core.Tax.Minnesota;
using PaycheckCalc.Core.Tax.Montana;
using PaycheckCalc.Core.Tax.Nebraska;
using PaycheckCalc.Core.Tax.NewJersey;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
using PaycheckCalc.Core.Tax.Massachusetts;
using PaycheckCalc.Core.Tax.Michigan;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.Federal.Annual;
using PaycheckCalc.Core.Tax.State;
using PaycheckCalc.Core.Tax.State.Annual;
using PaycheckCalc.Core.Tax.SelfEmployment;

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

        builder.Services.AddSingleton<ArkansasFormulaCalculator>(sp =>
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("ar_withholding_2026.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return new ArkansasFormulaCalculator(json);
        });
        builder.Services.AddSingleton<OklahomaOw2PercentageCalculator>(sp =>
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("ok_ow2_2026_percentage.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return new OklahomaOw2PercentageCalculator(json);
        });
        builder.Services.AddSingleton<Irs15TPercentageCalculator>(sp =>
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("us_irs_15t_2026_percentage_automated.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return new Irs15TPercentageCalculator(json);
        });
        builder.Services.AddSingleton<CaliforniaPercentageCalculator>(sp =>
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("ca_method_b_2026.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return new CaliforniaPercentageCalculator(json);
        });
        builder.Services.AddSingleton<ColoradoWithholdingCalculator>(sp =>
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("co_dr0004_2026.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return new ColoradoWithholdingCalculator(json);
        });
        builder.Services.AddSingleton<ConnecticutWithholdingCalculator>(sp =>
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("connecticut_withholding_2026.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return new ConnecticutWithholdingCalculator(json);
        });

        builder.Services.AddSingleton(new FicaCalculator());

        // Data-driven state calculator registry (schema + validation + calculation)
        builder.Services.AddSingleton<StateCalculatorRegistry>(sp =>
        {
            var registry = new StateCalculatorRegistry();

            // Alabama — unique filing statuses, dependents, federal withholding
            registry.Register(new AlabamaWithholdingCalculator());

            // Arizona — Form A-4 percentage-election method (0.5%–3.5%,
            // 2.0% default when no A-4 is on file)
            registry.Register(new ArizonaWithholdingCalculator());

            // Arkansas — DFA formula method with transitional zone brackets
            var arCalc = sp.GetRequiredService<ArkansasFormulaCalculator>();
            registry.Register(new ArkansasWithholdingCalculator(arCalc));

            // California — Method B (EDD DE 44)
            var caCalc = sp.GetRequiredService<CaliforniaPercentageCalculator>();
            registry.Register(new CaliforniaWithholdingCalculator(caCalc));

            // Colorado — flat 4.4% with DR 0004 Table 1 allowance + FMLI
            var coCalc = sp.GetRequiredService<ColoradoWithholdingCalculator>();
            registry.Register(coCalc);

            // Connecticut — TPG-211 table-driven withholding calculation rules
            var ctCalc = sp.GetRequiredService<ConnecticutWithholdingCalculator>();
            registry.Register(ctCalc);

            // Delaware — DE W-4, percentage method with personal credits
            registry.Register(new DelawareWithholdingCalculator());

            // District of Columbia — D-4 annualized percentage method with
            // filing-status standard deduction and $1,675 per-allowance
            // exemption (FR-230 2026)
            registry.Register(new DistrictOfColumbiaWithholdingCalculator());

            // Georgia — flat 5.19% with G-4 filing statuses, dependents,
            // standard deduction, and additional allowances (HB 111 / 2026
            // Employer's Tax Guide)
            registry.Register(new GeorgiaWithholdingCalculator());

            // Hawaii — Booklet A percentage method with HW-4 filing-status
            // standard deduction ($2,200 / $4,400) and $1,144 per HW-4
            // allowance; 2026 graduated brackets (1.4%–11.0%)
            registry.Register(new HawaiiWithholdingCalculator());

            // Idaho — flat 5.3% with ID W-4 filing-status standard deduction
            // and $3,300 per pre-2020 allowance (Idaho HB 521 / EPB00006)
            registry.Register(new IdahoWithholdingCalculator());

            // Illinois — flat 4.95% with IL-W-4 basic and additional allowances
            registry.Register(new IllinoisWithholdingCalculator());

            // Indiana — flat 3.05% with WH-4 personal/age/blind exemptions
            // ($1,000 each) and additional dependent exemption ($3,000 each,
            // Indiana Departmental Notice #1 for tax years 2023+)
            registry.Register(new IndianaWithholdingCalculator());

            // Iowa — flat 3.65% with pre-tax deductions and optional extra
            // withholding (IA W-4 Line 6)
            registry.Register(new IowaWithholdingCalculator());

            // Kansas — K-4 filing status (Single/Married), standard deduction
            // ($3,605 / $8,240), $2,250 per K-4 allowance, and two graduated
            // brackets (5.20% up to $23,000/$46,000, then 5.58%)
            registry.Register(new KansasWithholdingCalculator());

            // Kentucky — flat 4.0% with $3,160 standard deduction and $10
            // K-4 allowance credit per the 2026 Form 42A003 withholding formula
            registry.Register(new KentuckyWithholdingCalculator());
            // Louisiana — L-4 filing statuses (Single/Married/Head of Household),
            // $4,500/$9,000 personal exemption, $1,000 per-dependent deduction,
            // and three graduated brackets (1.85%/3.50%/4.25%) per R-1306
            registry.Register(new LouisianaWithholdingCalculator());

            // Maine — W-4ME filing statuses (Single/Married), $15,300/$30,600
            // standard deduction, $5,300 per W-4ME allowance, and three graduated
            // brackets (5.80%/6.75%/7.15%) per Maine Revenue Services 2026
            // Withholding Tables
            registry.Register(new MaineWithholdingCalculator());

            // Maryland — MW507 filing statuses (Single/Married/Head of Household),
            // variable standard deduction (15% of wages, min $1,600/$3,200,
            // max $2,550/$5,100), $3,200 per exemption, and ten graduated brackets
            // (2%–6.5%) per the Comptroller of Maryland 2026 Employer Withholding Guide
            registry.Register(new MarylandWithholdingCalculator());

            // Massachusetts — M-4 filing statuses (Single/Married/Head of Household),
            // personal/dependent/blind/age exemptions, flat 5% with 4% surtax above $1M
            registry.Register(new MassachusettsWithholdingCalculator());

            // Michigan — flat 4.25% with MI-W4 personal/dependent exemptions
            // ($5,900 per exemption, 2026 Form 446 Withholding Guide)
            registry.Register(new MichiganWithholdingCalculator());

            // Minnesota — W-4MN filing statuses (Single/Married/Head of Household),
            // $15,300/$30,600/$23,000 standard deduction, $5,300 per W-4MN allowance,
            // and four graduated brackets (5.35%/6.80%/7.85%/9.85%) per the Minnesota
            // Department of Revenue 2026 Withholding Tax Instructions and Tables (Pub. 89)
            registry.Register(new MinnesotaWithholdingCalculator());

            // Mississippi — 89-350 filing statuses (Single/Married/Head of Household),
            // standard deduction ($2,300/$4,600/$3,400), personal exemption
            // ($6,000/$12,000/$9,500), $1,500 per dependent, and two brackets
            // (0% on $0–$10,000, 4% over $10,000) per MS Pub 89-105 and HB 1 (2023)
            registry.Register(new MississippiWithholdingCalculator());

            // Missouri — MO W-4 filing statuses (Single/Married/Head of Household),
            // $15,750/$31,500/$23,625 standard deduction (mirrors federal),
            // $2,100 per MO W-4 allowance, and eight graduated brackets (0%–4.7%)
            // per the Missouri DOR 2026 Employer's Withholding Tax Guide
            registry.Register(new MissouriWithholdingCalculator());

            // Montana — MW-4 filing statuses (Single/Married/Head of Household),
            // variable standard deduction (20% of wages, min $4,370/$8,740,
            // max $5,310/$10,620 for Single/Married), $3,040 per MW-4 exemption,
            // and two brackets (4.7% on $0–$23,800, 5.9% over $23,800) per the
            // Montana DOR 2026 Withholding Tax Guide
            registry.Register(new MontanaWithholdingCalculator());

            // Nebraska — W-4N filing statuses (Single/Married/Head of Household),
            // standard deductions $8,600/$17,200/$12,900, $171 per-allowance credit
            // (applied to computed tax), and four graduated brackets (2.46%/3.51%/5.01%/5.2%)
            // with filing-status–specific thresholds per the Nebraska DOR 2026 Circular EN
            registry.Register(new NebraskaWithholdingCalculator());

            // New Jersey — NJ-W4 filing statuses A–E, $1,000 per-allowance deduction,
            // Table A (single) brackets for Status A and C, and Table B
            // (married/HoH/surviving) brackets for Status B, D, and E per the 2026 NJ-WT
            registry.Register(new NewJerseyWithholdingCalculator());

            // Oklahoma — OW-2 percentage method
            var okCalc = sp.GetRequiredService<OklahomaOw2PercentageCalculator>();
            registry.Register(new OklahomaWithholdingCalculator(okCalc));

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

        builder.Services.AddSingleton<PayCalculator>(sp =>
            new PayCalculator(
                sp.GetRequiredService<StateCalculatorRegistry>(),
                sp.GetRequiredService<FicaCalculator>(),
                sp.GetRequiredService<Irs15TPercentageCalculator>(),
                sp.GetRequiredService<LocalCalculatorRegistry>()));

        // ── Local (sub-state) tax calculators ──────────────────
        builder.Services.AddSingleton<LocalCalculatorRegistry>(sp =>
        {
            var registry = new LocalCalculatorRegistry();

            // Pennsylvania Act 32 EIT + LST
            using (var stream = FileSystem.OpenAppPackageFileAsync("pa_eit_2026.json").Result)
            using (var reader = new StreamReader(stream))
            {
                registry.Register(new PaEitCalculator(new PaEitRateTable(reader.ReadToEnd())));
            }
            registry.Register(new PaLstCalculator());

            // New York City
            using (var stream = FileSystem.OpenAppPackageFileAsync("nyc_withholding_2026.json").Result)
            using (var reader = new StreamReader(stream))
            {
                registry.Register(new NycWithholdingCalculator(reader.ReadToEnd()));
            }

            // Ohio RITA + CCA
            using (var stream = FileSystem.OpenAppPackageFileAsync("oh_rita_2026.json").Result)
            using (var reader = new StreamReader(stream))
            {
                registry.Register(new OhRitaCalculator(reader.ReadToEnd()));
            }
            using (var stream = FileSystem.OpenAppPackageFileAsync("oh_cca_2026.json").Result)
            using (var reader = new StreamReader(stream))
            {
                registry.Register(new OhCcaCalculator(reader.ReadToEnd()));
            }

            // Maryland county surtax
            using (var stream = FileSystem.OpenAppPackageFileAsync("md_county_surtax_2026.json").Result)
            using (var reader = new StreamReader(stream))
            {
                registry.Register(new MdCountyCalculator(reader.ReadToEnd()));
            }

            return registry;
        });

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

        builder.Services.AddSingleton<AnnualProjectionCalculator>(sp =>
            new AnnualProjectionCalculator(
                sp.GetRequiredService<Irs15TPercentageCalculator>(),
                sp.GetRequiredService<FicaCalculator>()));

        builder.Services.AddSingleton<IPaycheckRepository>(
            new JsonPaycheckRepository(FileSystem.AppDataDirectory));

        // ── Self-Employment calculators ─────────────────────
        builder.Services.AddSingleton<SelfEmploymentTaxCalculator>(sp =>
            new SelfEmploymentTaxCalculator(sp.GetRequiredService<FicaCalculator>()));
        builder.Services.AddSingleton<QbiDeductionCalculator>();
        builder.Services.AddSingleton<SelfEmploymentCalculator>(sp =>
            new SelfEmploymentCalculator(
                sp.GetRequiredService<SelfEmploymentTaxCalculator>(),
                sp.GetRequiredService<QbiDeductionCalculator>(),
                sp.GetRequiredService<Irs15TPercentageCalculator>(),
                sp.GetRequiredService<StateCalculatorRegistry>()));

        // ── Annual Form 1040 engine ─────────────────────────
        builder.Services.AddSingleton<Federal1040TaxCalculator>(sp =>
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("federal_1040_brackets_2026.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return new Federal1040TaxCalculator(json);
        });
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

        // 1040-ES engine and Phase-8 annual-scenario persistence.
        builder.Services.AddSingleton<Form1040ESCalculator>();
        builder.Services.AddSingleton<IAnnualScenarioRepository>(
            new JsonAnnualScenarioRepository(FileSystem.AppDataDirectory));

        // Shared annual state consumed by every Phase 8 flyout view-model.
        builder.Services.AddSingleton<AnnualTaxSession>();

        // Shared paycheck multi-scenario compare session.
        builder.Services.AddSingleton<ComparisonSession>();

        builder.Services.AddSingleton<CalculatorViewModel>();
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
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
