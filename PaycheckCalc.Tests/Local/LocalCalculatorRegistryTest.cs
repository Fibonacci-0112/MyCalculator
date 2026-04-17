using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Tax.Local;
using PaycheckCalc.Core.Tax.State;
using Xunit;

namespace PaycheckCalc.Tests.Local;

public class LocalCalculatorRegistryTest
{
    private sealed class StubCalculator : ILocalWithholdingCalculator
    {
        public StubCalculator(LocalityId id) { Locality = id; }
        public LocalityId Locality { get; }
        public IReadOnlyList<StateFieldDefinition> GetInputSchema() => Array.Empty<StateFieldDefinition>();
        public IReadOnlyList<string> Validate(LocalInputValues values) => Array.Empty<string>();
        public LocalWithholdingResult Calculate(CommonLocalWithholdingContext ctx, LocalInputValues v) =>
            new() { LocalityName = Locality.Name };
    }

    [Fact]
    public void Register_StoresCalculatorByCode()
    {
        var registry = new LocalCalculatorRegistry();
        var calc = new StubCalculator(new LocalityId(UsState.PA, "PA-EIT", "PA EIT"));
        registry.Register(calc);

        Assert.True(registry.TryGetCalculator("PA-EIT", out var found));
        Assert.Same(calc, found);
    }

    [Fact]
    public void Register_ReplacesExistingForSameCode()
    {
        var registry = new LocalCalculatorRegistry();
        registry.Register(new StubCalculator(new LocalityId(UsState.PA, "PA-EIT", "Old")));
        var newer = new StubCalculator(new LocalityId(UsState.PA, "PA-EIT", "New"));
        registry.Register(newer);

        Assert.True(registry.TryGetCalculator("PA-EIT", out var found));
        Assert.Same(newer, found);
    }

    [Fact]
    public void IsSupported_ReturnsFalseWhenNoCalculatorForState()
    {
        var registry = new LocalCalculatorRegistry();
        registry.Register(new StubCalculator(new LocalityId(UsState.PA, "PA-EIT", "PA EIT")));

        Assert.True(registry.IsSupported(UsState.PA));
        Assert.False(registry.IsSupported(UsState.OK));
    }

    [Fact]
    public void GetCalculatorsForState_FiltersByState()
    {
        var registry = new LocalCalculatorRegistry();
        registry.Register(new StubCalculator(new LocalityId(UsState.PA, "PA-EIT", "PA EIT")));
        registry.Register(new StubCalculator(new LocalityId(UsState.PA, "PA-LST", "PA LST")));
        registry.Register(new StubCalculator(new LocalityId(UsState.NY, "NY-NYC", "NYC")));

        var pa = registry.GetCalculatorsForState(UsState.PA);
        Assert.Equal(2, pa.Count);
        Assert.Contains(pa, c => c.Locality.Code == "PA-EIT");
        Assert.Contains(pa, c => c.Locality.Code == "PA-LST");
    }

    [Fact]
    public void TryGetCalculator_NullOrEmptyCodeReturnsFalse()
    {
        var registry = new LocalCalculatorRegistry();
        registry.Register(new StubCalculator(new LocalityId(UsState.PA, "PA-EIT", "PA EIT")));

        Assert.False(registry.TryGetCalculator(null, out _));
        Assert.False(registry.TryGetCalculator("", out _));
        Assert.False(registry.TryGetCalculator("UNKNOWN", out _));
    }

    [Fact]
    public void GetCalculator_ThrowsForUnknownCode()
    {
        var registry = new LocalCalculatorRegistry();
        Assert.Throws<KeyNotFoundException>(() => registry.GetCalculator("UNKNOWN"));
    }
}
