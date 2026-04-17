using PaycheckCalc.Core.Geocoding;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.Local.Maryland;
using PaycheckCalc.Core.Tax.Local.NewYork;
using PaycheckCalc.Core.Tax.Local.Ohio;
using PaycheckCalc.Core.Tax.Local.Pennsylvania;

namespace PaycheckCalc.App.Services;

/// <summary>
/// Default <see cref="IJurisdictionService"/> implementation. Given a geocoded
/// address, returns the candidate <see cref="LocalityId"/> values the user most
/// likely owes local tax to.
/// <para>
/// This is intentionally heuristic rather than GIS-precise — it uses the
/// <see cref="GeocodeResult.State"/> / <see cref="GeocodeResult.City"/> /
/// <see cref="GeocodeResult.County"/> fields Google returns to pick one or more
/// of the registered calculators. The UI always lets the user confirm or
/// override the choice before the paycheck is calculated.
/// </para>
/// </summary>
public sealed class JurisdictionResolver : IJurisdictionService
{
    public Task<JurisdictionResult> ResolveAsync(GeocodeResult geocode, CancellationToken cancellationToken = default)
    {
        if (geocode.State is null)
            return Task.FromResult(JurisdictionResult.None);

        var candidates = new List<LocalityId>();
        string? description = null;

        switch (geocode.State.Value)
        {
            case UsState.PA:
                // Every PA employee is subject to Act 32 EIT and may owe LST.
                candidates.Add(PaEitCalculator.LocalityKey);
                candidates.Add(PaLstCalculator.LocalityKey);
                description = "PA EIT and LST always apply; enter PSD codes for home and work.";
                break;

            case UsState.NY:
                if (IsNewYorkCity(geocode))
                {
                    candidates.Add(NycWithholdingCalculator.LocalityKey);
                    description = "New York City income tax applies to residents only.";
                }
                break;

            case UsState.OH:
                // We don't know which agency (RITA vs CCA) collects for which muni without a
                // full membership database; surface both and let the user pick.
                candidates.Add(OhRitaCalculator.LocalityKey);
                candidates.Add(OhCcaCalculator.LocalityKey);
                description = "Ohio: pick the appropriate collection agency (RITA or CCA) and municipality.";
                break;

            case UsState.MD:
                candidates.Add(MdCountyCalculator.LocalityKey);
                description = geocode.County is not null
                    ? $"Maryland county surtax applies. County detected: {geocode.County}."
                    : "Maryland county surtax applies.";
                break;
        }

        return Task.FromResult(new JurisdictionResult(candidates, description));
    }

    private static bool IsNewYorkCity(GeocodeResult g)
    {
        // NYC consists of the five boroughs whose counties are New York, Kings,
        // Queens, Bronx, and Richmond. Match on exact county or borough name —
        // a substring match would misclassify places like "New York Mills" (Oneida County).
        var nycCounties = new[]
        {
            "New York", "New York County",
            "Kings", "Kings County",
            "Queens", "Queens County",
            "Bronx", "Bronx County",
            "Richmond", "Richmond County"
        };
        if (g.County is not null && nycCounties.Any(c =>
                string.Equals(g.County, c, StringComparison.OrdinalIgnoreCase)))
            return true;

        var nycCities = new[]
        {
            "New York", "Manhattan", "Brooklyn", "Queens", "Bronx", "Staten Island"
        };
        return g.City is not null && nycCities.Any(c =>
            string.Equals(g.City, c, StringComparison.OrdinalIgnoreCase));
    }
}
