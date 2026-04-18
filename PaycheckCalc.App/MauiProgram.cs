using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using PaycheckCalc.App.Services;
using PaycheckCalc.App.Storage;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.App.Views;
using PaycheckCalc.Core.Data;
using PaycheckCalc.Core.Geocoding;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Storage;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Arkansas;
using PaycheckCalc.Core.Tax.California;
using PaycheckCalc.Core.Tax.Colorado;
using PaycheckCalc.Core.Tax.Connecticut;
using PaycheckCalc.Core.Tax.Delaware;
using PaycheckCalc.Core.Tax.Illinois;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;
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

        // Single asset loader shared by every JSON-backed calculator below.
        builder.Services.AddSingleton<ITaxDataAssetLoader, MauiTaxDataAssetLoader>();

        builder.Services.AddSingleton<ArkansasFormulaCalculator>(sp =>
        {
            var json = sp.GetRequiredService<ITaxDataAssetLoader>()
                .ReadAllTextAsync("ar_withholding_2026.json").GetAwaiter().GetResult();

            return new ArkansasFormulaCalculator(json);
        });
        builder.Services.AddSingleton<OklahomaOw2PercentageCalculator>(sp =>
        {
            var json = sp.GetRequiredService<ITaxDataAssetLoader>()
                .ReadAllTextAsync("ok_ow2_2026_percentage.json").GetAwaiter().GetResult();

            return new OklahomaOw2PercentageCalculator(json);
        });
        builder.Services.AddSingleton<Irs15TPercentageCalculator>(sp =>
        {
            var json = sp.GetRequiredService<ITaxDataAssetLoader>()
                .ReadAllTextAsync("us_irs_15t_2026_percentage_automated.json").GetAwaiter().GetResult();

            return new Irs15TPercentageCalculator(json);
        });
        builder.Services.AddSingleton<CaliforniaPercentageCalculator>(sp =>
        {
            var json = sp.GetRequiredService<ITaxDataAssetLoader>()
                .ReadAllTextAsync("ca_method_b_2026.json").GetAwaiter().GetResult();

            return new CaliforniaPercentageCalculator(json);
        });
        builder.Services.AddSingleton<ColoradoWithholdingCalculator>(sp =>
        {
            var json = sp.GetRequiredService<ITaxDataAssetLoader>()
                .ReadAllTextAsync("co_dr0004_2026.json").GetAwaiter().GetResult();

            return new ColoradoWithholdingCalculator(json);
        });
        builder.Services.AddSingleton<ConnecticutWithholdingCalculator>(sp =>
        {
            var json = sp.GetRequiredService<ITaxDataAssetLoader>()
                .ReadAllTextAsync("connecticut_withholding_2026.json").GetAwaiter().GetResult();

            return new ConnecticutWithholdingCalculator(json);
        });

        builder.Services.AddSingleton(new FicaCalculator());

        // Data-driven state calculator registry (schema + validation + calculation)
        builder.Services.AddSingleton<StateCalculatorRegistry>(sp =>
        {
            var registry = new StateCalculatorRegistry();

            // Alabama — unique filing statuses, dependents, federal withholding
            registry.Register(new AlabamaWithholdingCalculator());

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

            // Illinois — flat 4.95% with IL-W-4 basic and additional allowances
            registry.Register(new IllinoisWithholdingCalculator());

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
            {
                var loader = sp.GetRequiredService<ITaxDataAssetLoader>();
                var paJson = loader.ReadAllTextAsync("pa_eit_2026.json").GetAwaiter().GetResult();
                registry.Register(new PaEitCalculator(new PaEitRateTable(paJson)));
            }
            registry.Register(new PaLstCalculator());

            // New York City
            {
                var loader = sp.GetRequiredService<ITaxDataAssetLoader>();
                var nycJson = loader.ReadAllTextAsync("nyc_withholding_2026.json").GetAwaiter().GetResult();
                registry.Register(new NycWithholdingCalculator(nycJson));
            }

            // Ohio RITA + CCA
            {
                var loader = sp.GetRequiredService<ITaxDataAssetLoader>();
                var ritaJson = loader.ReadAllTextAsync("oh_rita_2026.json").GetAwaiter().GetResult();
                registry.Register(new OhRitaCalculator(ritaJson));
            }
            {
                var loader = sp.GetRequiredService<ITaxDataAssetLoader>();
                var ccaJson = loader.ReadAllTextAsync("oh_cca_2026.json").GetAwaiter().GetResult();
                registry.Register(new OhCcaCalculator(ccaJson));
            }

            // Maryland county surtax
            {
                var loader = sp.GetRequiredService<ITaxDataAssetLoader>();
                var mdJson = loader.ReadAllTextAsync("md_county_surtax_2026.json").GetAwaiter().GetResult();
                registry.Register(new MdCountyCalculator(mdJson));
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
            var json = sp.GetRequiredService<ITaxDataAssetLoader>()
                .ReadAllTextAsync("federal_1040_brackets_2026.json").GetAwaiter().GetResult();
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

        builder.Services.AddSingleton<CalculatorViewModel>();
        builder.Services.AddSingleton<SavedPaychecksViewModel>();
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
