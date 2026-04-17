namespace PaycheckCalc.Core.Geocoding;

/// <summary>
/// Raw address components as entered by the user. Intentionally loose — both
/// the single-line <see cref="Line1"/> and the structured component fields
/// are honored; address normalization and geocoding adapters may use whichever
/// fields are available.
/// </summary>
public sealed record AddressInput(
    string? Line1 = null,
    string? Line2 = null,
    string? City = null,
    string? StateCode = null,
    string? PostalCode = null,
    string? County = null,
    string? Country = "US")
{
    /// <summary>
    /// Joins the non-empty parts of the address into a single comma-separated string,
    /// which is the format accepted by Google Maps Geocoding.
    /// </summary>
    public string ToSingleLine()
    {
        var parts = new[]
        {
            Line1,
            Line2,
            City,
            string.IsNullOrWhiteSpace(StateCode) ? null : StateCode,
            PostalCode,
            Country
        };

        return string.Join(", ", parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim()));
    }
}
