using System.Net.Http;
using PaycheckCalc.Core.Data;

namespace PaycheckCalc.Web.Services;

/// <summary>
/// Browser implementation of <see cref="ITaxDataAssetLoader"/>. Fetches
/// JSON tax tables from <c>wwwroot/data/&lt;assetName&gt;</c> via
/// <see cref="HttpClient"/>.
/// </summary>
public sealed class HttpClientTaxDataAssetLoader : ITaxDataAssetLoader
{
    private readonly HttpClient _http;

    public HttpClientTaxDataAssetLoader(HttpClient http)
    {
        _http = http;
    }

    public Task<string> ReadAllTextAsync(string assetName) =>
        _http.GetStringAsync($"data/{assetName}");
}
