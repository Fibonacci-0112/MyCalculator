using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// HTTP-backed <see cref="IAnnualScenarioRepository"/>; peer to
/// <see cref="HttpPaycheckRepository"/>.
/// </summary>
public sealed class HttpAnnualScenarioRepository : IAnnualScenarioRepository
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpAnnualScenarioRepository(IHttpClientFactory factory)
    {
        _http = factory.CreateClient(ApiConfiguration.ApiHttpClientName);
    }

    public async Task<IReadOnlyList<SavedAnnualScenario>> GetAllAsync()
    {
        using var response = await _http.GetAsync("/api/annual-scenarios");
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return Array.Empty<SavedAnnualScenario>();
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<SavedAnnualScenario>>(JsonOptions);
        return list ?? new List<SavedAnnualScenario>();
    }

    public async Task<SavedAnnualScenario?> GetByIdAsync(Guid id)
    {
        using var response = await _http.GetAsync($"/api/annual-scenarios/{id}");
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SavedAnnualScenario>(JsonOptions);
    }

    public async Task SaveAsync(SavedAnnualScenario scenario)
    {
        using var response = await _http.PutAsJsonAsync($"/api/annual-scenarios/{scenario.Id}", scenario, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        using var response = await _http.DeleteAsync($"/api/annual-scenarios/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        response.EnsureSuccessStatusCode();
    }
}
