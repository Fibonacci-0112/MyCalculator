using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Geocoding;

/// <summary>Output of <see cref="IGeocodingService.GeocodeAsync"/>.</summary>
public sealed record GeocodeResult(
    double Latitude,
    double Longitude,
    AddressInput NormalizedAddress,
    UsState? State,
    string? County,
    string? City,
    string? PostalCode,
    string? PlaceId = null);

/// <summary>Output of <see cref="IJurisdictionService.ResolveAsync"/>.</summary>
public sealed record JurisdictionResult(
    IReadOnlyList<PaycheckCalc.Core.Tax.Local.LocalityId> Candidates,
    string? Description = null)
{
    /// <summary>Convenience accessor for the most likely locality (<c>Candidates[0]</c>).</summary>
    public PaycheckCalc.Core.Tax.Local.LocalityId? Primary => Candidates.Count > 0 ? Candidates[0] : null;

    /// <summary>Empty result meaning "no locality applies to this address".</summary>
    public static JurisdictionResult None { get; } =
        new(Array.Empty<PaycheckCalc.Core.Tax.Local.LocalityId>(), "No local taxing jurisdiction.");
}
