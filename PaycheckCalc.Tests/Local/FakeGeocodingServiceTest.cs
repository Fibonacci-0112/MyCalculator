using PaycheckCalc.Core.Geocoding;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using Xunit;

namespace PaycheckCalc.Tests.Local;

/// <summary>
/// Contract test for <see cref="IGeocodingService"/> — uses a fake implementation
/// so the suite has zero dependency on Google or any network. Proves that callers
/// of the interface can swap implementations cleanly.
/// </summary>
public class FakeGeocodingServiceTest
{
    private sealed class FakeGeocoder : IGeocodingService
    {
        private readonly GeocodeResult? _result;
        public int CallCount { get; private set; }

        public FakeGeocoder(GeocodeResult? result) { _result = result; }

        public Task<GeocodeResult?> GeocodeAsync(AddressInput address, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task Geocode_ReturnsConfiguredResult()
    {
        var expected = new GeocodeResult(
            40.7128, -74.0060,
            new AddressInput(City: "New York", StateCode: "NY"),
            UsState.NY, "New York", "New York", "10001");
        var fake = new FakeGeocoder(expected);

        var result = await fake.GeocodeAsync(new AddressInput(Line1: "1 Broadway"));

        Assert.Equal(expected, result);
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task Geocode_NullResult_IsSupported()
    {
        var fake = new FakeGeocoder(result: null);
        var result = await fake.GeocodeAsync(new AddressInput());
        Assert.Null(result);
    }

    [Fact]
    public void JurisdictionResult_None_HasNoPrimary()
    {
        Assert.Null(JurisdictionResult.None.Primary);
        Assert.Empty(JurisdictionResult.None.Candidates);
    }

    [Fact]
    public void JurisdictionResult_PrimaryReturnsFirstCandidate()
    {
        var first = new LocalityId(UsState.PA, "PA-EIT", "PA EIT");
        var second = new LocalityId(UsState.PA, "PA-LST", "PA LST");
        var result = new JurisdictionResult(new[] { first, second });
        Assert.Equal(first, result.Primary);
    }

    [Fact]
    public void AddressInput_ToSingleLine_JoinsNonEmptyParts()
    {
        var addr = new AddressInput(
            Line1: "123 Main St",
            City: "Philadelphia",
            StateCode: "PA",
            PostalCode: "19103");

        Assert.Equal("123 Main St, Philadelphia, PA, 19103, US", addr.ToSingleLine());
    }

    [Fact]
    public void AddressInput_ToSingleLine_SkipsEmpties()
    {
        var addr = new AddressInput(City: "Philadelphia");
        Assert.Equal("Philadelphia, US", addr.ToSingleLine());
    }
}
