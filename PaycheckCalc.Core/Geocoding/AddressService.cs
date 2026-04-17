using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Geocoding;

/// <summary>
/// Default in-process <see cref="IAddressService"/> implementation.
/// Performs trimming, title-casing of <see cref="AddressInput.City"/>,
/// upper-casing the USPS state code, 5-digit ZIP truncation, and validation
/// that the state code parses as a <see cref="UsState"/>.
/// </summary>
public sealed class AddressService : IAddressService
{
    public AddressInput Normalize(AddressInput address, out IReadOnlyList<string> errors)
    {
        var messages = new List<string>();

        var line1 = TrimOrNull(address.Line1);
        var line2 = TrimOrNull(address.Line2);
        var city = TitleCase(TrimOrNull(address.City));
        var stateCode = TrimOrNull(address.StateCode)?.ToUpperInvariant();
        var postal = TrimOrNull(address.PostalCode);
        var county = TitleCase(TrimOrNull(address.County));
        var country = string.IsNullOrWhiteSpace(address.Country) ? "US" : address.Country.Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(stateCode) && !Enum.TryParse<UsState>(stateCode, ignoreCase: false, out _))
            messages.Add($"Unknown US state code '{stateCode}'.");

        if (!string.IsNullOrEmpty(postal))
        {
            // Accept 5-digit or ZIP+4, store the 5-digit prefix for dictionary lookups.
            var digits = new string(postal.Where(char.IsDigit).ToArray());
            if (digits.Length >= 5)
                postal = digits[..5];
            else
                messages.Add($"ZIP code '{postal}' is not a 5-digit US postal code.");
        }

        errors = messages;

        return new AddressInput(
            Line1: line1,
            Line2: line2,
            City: city,
            StateCode: stateCode,
            PostalCode: postal,
            County: county,
            Country: country);
    }

    private static string? TrimOrNull(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? TitleCase(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return System.Globalization.CultureInfo.InvariantCulture
            .TextInfo.ToTitleCase(s.ToLowerInvariant());
    }
}

/// <summary>Thread-safe in-memory <see cref="IGeocodingCache"/>.</summary>
public sealed class InMemoryGeocodingCache : IGeocodingCache
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, GeocodeResult> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string normalizedKey, out GeocodeResult? result)
    {
        var found = _cache.TryGetValue(normalizedKey, out var value);
        result = value;
        return found;
    }

    public void Set(string normalizedKey, GeocodeResult result)
    {
        _cache[normalizedKey] = result;
    }
}
