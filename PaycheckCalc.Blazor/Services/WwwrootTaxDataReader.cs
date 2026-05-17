using PaycheckCalc.Core.DependencyInjection;

namespace PaycheckCalc.Blazor.Services;

public sealed class WwwrootTaxDataReader : ITaxDataReader
{
    private static readonly string Root =
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "data");

    public string ReadAllText(string logicalName) =>
        File.ReadAllText(Path.Combine(Root, logicalName));
}
