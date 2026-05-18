using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PaycheckCalc.App.Auth;

/// <summary>
/// Typed wrapper for the Identity API endpoints exposed at /api/auth/* by
/// <c>MapIdentityApi&lt;ApplicationUser&gt;()</c> on the Blazor server.
///
/// Uses a plain <see cref="HttpClient"/> (no <see cref="AuthenticatingHttpHandler"/>)
/// because login / register / refresh don't accept a bearer token — that
/// would be circular.
///
/// Note on user identifiers: ASP.NET Core Identity's
/// <see cref="BearerTokenAuthenticationHandler"/> issues opaque tokens
/// (not JWTs), so we can't introspect them client-side to recover the
/// server user id. We use the email instead — Identity is configured with
/// RequireUniqueEmail = true, so the email is a stable per-user identifier
/// suitable for naming the local cache folder.
/// </summary>
public sealed class AuthApiClient
{
    private readonly HttpClient _http;

    public AuthApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient(ApiConfiguration.AuthHttpClientName);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var request = new { email, password };
        using var response = await _http.PostAsJsonAsync("/api/auth/login", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, ct);
            return AuthResult.Failure($"Login failed ({(int)response.StatusCode}): {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<IdentityTokenResponse>(cancellationToken: ct);
        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            return AuthResult.Failure("Login succeeded but no token returned.");

        return AuthResult.Success(BuildTokens(payload, email));
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var request = new { email, password };
        using var response = await _http.PostAsJsonAsync("/api/auth/register", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, ct);
            return AuthResult.Failure($"Registration failed ({(int)response.StatusCode}): {body}");
        }

        // Identity's /register returns 200 OK with no body. Sign in immediately
        // to materialize tokens.
        return await LoginAsync(email, password, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string email, CancellationToken ct = default)
    {
        var request = new { refreshToken };
        using var response = await _http.PostAsJsonAsync("/api/auth/refresh", request, ct);
        if (!response.IsSuccessStatusCode)
            return AuthResult.Failure($"Token refresh failed ({(int)response.StatusCode}).");

        var payload = await response.Content.ReadFromJsonAsync<IdentityTokenResponse>(cancellationToken: ct);
        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            return AuthResult.Failure("Refresh returned no token.");

        return AuthResult.Success(BuildTokens(payload, email));
    }

    private static AuthTokens BuildTokens(IdentityTokenResponse payload, string email)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
        // Email serves as both the user-facing identifier and the local
        // cache-folder key; see class doc for rationale.
        return new AuthTokens(payload.AccessToken, payload.RefreshToken, expiresAt, UserId: email, Email: email);
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch { return ""; }
    }

    private sealed record IdentityTokenResponse(
        [property: JsonPropertyName("tokenType")]    string TokenType,
        [property: JsonPropertyName("accessToken")]  string AccessToken,
        [property: JsonPropertyName("expiresIn")]    int ExpiresIn,
        [property: JsonPropertyName("refreshToken")] string RefreshToken);
}

public sealed record AuthResult(bool IsSuccess, AuthTokens? Tokens, string? Error)
{
    public static AuthResult Success(AuthTokens tokens) => new(true, tokens, null);
    public static AuthResult Failure(string error) => new(false, null, error);
}
