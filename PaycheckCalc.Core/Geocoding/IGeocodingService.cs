namespace PaycheckCalc.Core.Geocoding;

/// <summary>
/// Normalizes free-form address input (trimming, case-folding, USPS state validation).
/// Pure/in-process — no network dependency — so the default implementation lives in Core.
/// </summary>
public interface IAddressService
{
    /// <summary>
    /// Returns a canonical form of <paramref name="address"/>. Never throws;
    /// <paramref name="errors"/> collects any validation messages produced along the way.
    /// </summary>
    AddressInput Normalize(AddressInput address, out IReadOnlyList<string> errors);
}

/// <summary>
/// Geocodes a postal address to latitude/longitude and normalized components.
/// Implementations will typically call an external provider such as Google Maps.
/// </summary>
public interface IGeocodingService
{
    Task<GeocodeResult?> GeocodeAsync(AddressInput address, CancellationToken cancellationToken = default);
}

/// <summary>
/// Maps a geocoded address to the list of local taxing jurisdictions
/// (<see cref="PaycheckCalc.Core.Tax.Local.LocalityId"/>) that apply.
/// </summary>
public interface IJurisdictionService
{
    Task<JurisdictionResult> ResolveAsync(GeocodeResult geocode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple write-through cache in front of an <see cref="IGeocodingService"/>
/// to avoid re-calling the upstream API for an address the user has already looked up.
/// </summary>
public interface IGeocodingCache
{
    bool TryGet(string normalizedKey, out GeocodeResult? result);
    void Set(string normalizedKey, GeocodeResult result);
}
