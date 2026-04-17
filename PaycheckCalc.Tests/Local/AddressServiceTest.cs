using PaycheckCalc.Core.Geocoding;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class AddressServiceTest
{
    [Fact]
    public void Normalize_TrimsCasesAndZipTruncates()
    {
        var svc = new AddressService();
        var input = new AddressInput(
            Line1: "  123 main st  ",
            City: " philadelphia ",
            StateCode: "pa ",
            PostalCode: "19103-1234");

        var result = svc.Normalize(input, out var errors);

        Assert.Empty(errors);
        Assert.Equal("123 main st", result.Line1);
        Assert.Equal("Philadelphia", result.City);
        Assert.Equal("PA", result.StateCode);
        Assert.Equal("19103", result.PostalCode);
        Assert.Equal("US", result.Country);
    }

    [Fact]
    public void Normalize_UnknownStateProducesError()
    {
        var svc = new AddressService();
        var _ = svc.Normalize(new AddressInput(StateCode: "ZZ"), out var errors);
        Assert.Contains(errors, e => e.Contains("ZZ"));
    }

    [Fact]
    public void Normalize_ShortZipProducesError()
    {
        var svc = new AddressService();
        var _ = svc.Normalize(new AddressInput(PostalCode: "12"), out var errors);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void InMemoryCache_ReturnsStoredResult()
    {
        var cache = new InMemoryGeocodingCache();
        var gr = new GeocodeResult(40.0, -75.0,
            new AddressInput(City: "Philadelphia", StateCode: "PA"),
            PaycheckCalc.Core.Models.UsState.PA, null, "Philadelphia", "19103");

        cache.Set("philly", gr);
        Assert.True(cache.TryGet("philly", out var found));
        Assert.Equal(gr, found);
    }

    [Fact]
    public void InMemoryCache_MissesUnknownKey()
    {
        var cache = new InMemoryGeocodingCache();
        Assert.False(cache.TryGet("missing", out var found));
        Assert.Null(found);
    }
}
