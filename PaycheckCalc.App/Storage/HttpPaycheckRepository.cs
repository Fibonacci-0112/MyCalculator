using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaycheckCalc.App.Auth;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.App.Storage;

/// <summary>
/// HTTP-backed <see cref="IPaycheckRepository"/> that talks to the server's
/// /api/paychecks endpoints. The bearer token is attached automatically by
/// <see cref="AuthenticatingHttpHandler"/>; this class only deals with the
/// JSON envelope.
///
/// Returns an empty list / null on 401 so a logged-out user composed
/// through <see cref="SyncingPaycheckRepository"/> doesn't crash; the
/// syncing wrapper falls back to the local cache instead.
/// </summary>
public sealed class HttpPaycheckRepository : IPaycheckRepository
{
    private readonly HttpClient _http;

    // Same options as the server (camelCase + JsonStringEnumConverter) so the
    // wire format matches existing JSON-shape tests in the Core test suite.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpPaycheckRepository(IHttpClientFactory factory)
    {
        _http = factory.CreateClient(ApiConfiguration.ApiHttpClientName);
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        using var response = await _http.GetAsync("/api/paychecks");
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return Array.Empty<SavedPaycheck>();
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<SavedPaycheck>>(JsonOptions);
        return list ?? new List<SavedPaycheck>();
    }

    public async Task<SavedPaycheck?> GetByIdAsync(Guid id)
    {
        using var response = await _http.GetAsync($"/api/paychecks/{id}");
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SavedPaycheck>(JsonOptions);
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        using var response = await _http.PutAsJsonAsync($"/api/paychecks/{paycheck.Id}", paycheck, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        using var response = await _http.DeleteAsync($"/api/paychecks/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        response.EnsureSuccessStatusCode();
    }
}
