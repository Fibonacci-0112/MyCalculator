using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.Core.Geocoding;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.App.Services;

/// <summary>
/// <see cref="IGeocodingService"/> implementation backed by the Google Maps
/// Geocoding HTTP API (<c>https://maps.googleapis.com/maps/api/geocode/json</c>).
/// <para>
/// The API key is supplied at runtime by <see cref="IGoogleMapsApiKeyProvider"/>
/// so it can live in platform-specific secure storage (Android <c>SecureStorage</c>,
/// Windows user-scoped config). When no key is configured, this service returns
/// <c>null</c> so callers can fall back to manual locality entry.
/// </para>
/// <para>
/// Responses are cached through <see cref="IGeocodingCache"/> to save API quota
/// when the same address is looked up repeatedly.
/// </para>
/// </summary>
public sealed class GoogleMapsGeocodingService : IGeocodingService
{
    private const string Endpoint = "https://maps.googleapis.com/maps/api/geocode/json";

    private readonly HttpClient _http;
    private readonly IGoogleMapsApiKeyProvider _keyProvider;
    private readonly IGeocodingCache _cache;

    public GoogleMapsGeocodingService(
        HttpClient http,
        IGoogleMapsApiKeyProvider keyProvider,
        IGeocodingCache cache)
    {
        _http = http;
        _keyProvider = keyProvider;
        _cache = cache;
    }

    public async Task<GeocodeResult?> GeocodeAsync(AddressInput address, CancellationToken cancellationToken = default)
    {
        var key = await _keyProvider.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key))
            return null; // Force fallback to manual entry; do not throw.

        var cacheKey = address.ToSingleLine().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cacheKey))
            return null;

        if (_cache.TryGet(cacheKey, out var cached) && cached is not null)
            return cached;

        var uri = $"{Endpoint}?address={Uri.EscapeDataString(cacheKey)}&key={Uri.EscapeDataString(key)}&components=country:US";
        GoogleResponse? payload;
        try
        {
            payload = await _http.GetFromJsonAsync<GoogleResponse>(uri, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }

        if (payload is null || !string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase)
            || payload.Results is null || payload.Results.Count == 0)
        {
            return null;
        }

        var top = payload.Results[0];
        var parsed = ExtractComponents(top);
        var result = new GeocodeResult(
            Latitude: top.Geometry?.Location?.Lat ?? 0,
            Longitude: top.Geometry?.Location?.Lng ?? 0,
            NormalizedAddress: parsed.Address,
            State: parsed.State,
            County: parsed.County,
            City: parsed.City,
            PostalCode: parsed.PostalCode,
            PlaceId: top.PlaceId);

        _cache.Set(cacheKey, result);
        return result;
    }

    private static (AddressInput Address, UsState? State, string? County, string? City, string? PostalCode)
        ExtractComponents(GoogleResult result)
    {
        string? city = null, county = null, postal = null, stateCode = null, line1 = null;

        foreach (var c in result.AddressComponents ?? new List<GoogleComponent>())
        {
            if (c.Types is null) continue;
            if (c.Types.Contains("locality")) city = c.LongName;
            else if (c.Types.Contains("administrative_area_level_2")) county = c.LongName;
            else if (c.Types.Contains("administrative_area_level_1")) stateCode = c.ShortName;
            else if (c.Types.Contains("postal_code")) postal = c.ShortName;
            else if (c.Types.Contains("street_number") || c.Types.Contains("route"))
                line1 = (line1 is null ? string.Empty : line1 + " ") + c.LongName;
        }

        UsState? state = null;
        if (!string.IsNullOrEmpty(stateCode)
            && Enum.TryParse<UsState>(stateCode, ignoreCase: true, out var parsed))
        {
            state = parsed;
        }

        var address = new AddressInput(
            Line1: line1,
            City: city,
            StateCode: stateCode,
            PostalCode: postal,
            County: county,
            Country: "US");

        return (address, state, county, city, postal);
    }

    // DTOs for Google Maps Geocoding response
    private sealed class GoogleResponse
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("results")] public List<GoogleResult>? Results { get; set; }
    }

    private sealed class GoogleResult
    {
        [JsonPropertyName("place_id")] public string? PlaceId { get; set; }
        [JsonPropertyName("address_components")] public List<GoogleComponent>? AddressComponents { get; set; }
        [JsonPropertyName("geometry")] public GoogleGeometry? Geometry { get; set; }
    }

    private sealed class GoogleComponent
    {
        [JsonPropertyName("long_name")] public string? LongName { get; set; }
        [JsonPropertyName("short_name")] public string? ShortName { get; set; }
        [JsonPropertyName("types")] public List<string>? Types { get; set; }
    }

    private sealed class GoogleGeometry
    {
        [JsonPropertyName("location")] public GoogleLocation? Location { get; set; }
    }

    private sealed class GoogleLocation
    {
        [JsonPropertyName("lat")] public double Lat { get; set; }
        [JsonPropertyName("lng")] public double Lng { get; set; }
    }
}

/// <summary>
/// Supplies the Google Maps API key at runtime. Implementations on Android should
/// use <c>Microsoft.Maui.Storage.SecureStorage</c>; Windows should use user-scoped
/// config. A missing key is a normal, supported state — the geocoding service
/// returns null and the UI falls back to manual locality entry.
/// </summary>
public interface IGoogleMapsApiKeyProvider
{
    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);
    Task SetApiKeyAsync(string? key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default key provider backed by <c>Microsoft.Maui.Storage.SecureStorage</c> on
/// platforms that support it (Android), and in-memory otherwise (Windows test host).
/// </summary>
public sealed class SecureStorageGoogleMapsApiKeyProvider : IGoogleMapsApiKeyProvider
{
    internal const string StorageKey = "GoogleMapsApiKey";

    private string? _inMemoryKey;

    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stored))
                return stored;
        }
        catch (Exception)
        {
            // SecureStorage is not available on some platforms (e.g. Windows unpackaged) or in unit tests.
        }

        return _inMemoryKey;
    }

    public async Task SetApiKeyAsync(string? key, CancellationToken cancellationToken = default)
    {
        _inMemoryKey = key;
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                Microsoft.Maui.Storage.SecureStorage.Default.Remove(StorageKey);
            else
                await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(StorageKey, key).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Ignore secure-storage failures; in-memory fallback already captured.
        }
    }
}
