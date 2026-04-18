namespace PaycheckCalc.Core.Data;

/// <summary>
/// Platform-agnostic loader for the JSON tax-table assets shipped under
/// <c>PaycheckCalc.Core/Data/</c>. Each head supplies its own implementation:
/// the MAUI app reads from <c>FileSystem.OpenAppPackageFileAsync</c>, the
/// Blazor Web head fetches from <c>wwwroot/data/</c> via <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
/// <remarks>
/// Asset names are the logical file names (e.g. <c>"us_irs_15t_2026_percentage_automated.json"</c>)
/// — the same names used as <c>LogicalName</c> values in <c>PaycheckCalc.App.csproj</c>.
/// </remarks>
public interface ITaxDataAssetLoader
{
    /// <summary>
    /// Reads the entire contents of the named asset as a UTF-8 string.
    /// </summary>
    Task<string> ReadAllTextAsync(string assetName);
}
