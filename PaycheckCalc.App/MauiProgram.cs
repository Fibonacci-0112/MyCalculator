using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using PaycheckCalc.App.ViewModels;
using PaycheckCalc.App.Views;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Pay;
using PaycheckCalc.Core.Tax.Fica;
using PaycheckCalc.Core.Tax.Alabama;
using PaycheckCalc.Core.Tax.Oklahoma;
using PaycheckCalc.Core.Tax.Pennsylvania;
using PaycheckCalc.Core.Tax.Federal;
using PaycheckCalc.Core.Tax.State;

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

        builder.Services.AddSingleton(new FicaCalculator());

        builder.Services.AddSingleton<StateTaxCalculatorFactory>(sp =>
        {
            var factory = new StateTaxCalculatorFactory();

            // Alabama
            var alCalc = new AlabamaFormulaCalculator();
            factory.Register(new AlabamaStateTaxCalculator(alCalc));
            // Oklahoma (OW-2 percentage method)
            var okCalc = sp.GetRequiredService<OklahomaOw2PercentageCalculator>();
            factory.Register(new OklahomaStateTaxCalculator(okCalc));

            // Pennsylvania (flat 3.07%)
            factory.Register(new PennsylvaniaStateTaxCalculator());

            // States with no individual income tax
            UsState[] noTaxStates = [UsState.AK, UsState.FL, UsState.NV, UsState.NH, UsState.SD, UsState.TN, UsState.TX, UsState.WA, UsState.WY];
            foreach (var state in noTaxStates)
                factory.Register(new NoIncomeTaxCalculator(state));

            // All remaining states via the annualized percentage method
            foreach (var (state, config) in StateTaxConfigs2026.Configs)
                factory.Register(new PercentageMethodStateTaxCalculator(state, config));

            return factory;
        });

        builder.Services.AddSingleton<PayCalculator>();
        builder.Services.AddSingleton<CalculatorViewModel>();
        builder.Services.AddSingleton<InputsPage>();
        builder.Services.AddSingleton<ResultsPage>();
        builder.Services.AddSingleton<ComparePage>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
