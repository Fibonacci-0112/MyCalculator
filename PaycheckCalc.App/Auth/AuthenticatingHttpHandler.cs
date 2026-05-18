using System.Net;
using System.Net.Http.Headers;

namespace PaycheckCalc.App.Auth;

/// <summary>
/// <see cref="DelegatingHandler"/> that attaches the current bearer token
/// to every outbound API request and, on a 401, attempts one refresh
/// against /api/auth/refresh before giving up. Failures clear the stored
/// tokens so the UI's auth state correctly reflects "signed out".
///
/// Composed onto the named <c>"api"</c> HttpClient in MauiProgram so
/// every per-feature repository (paychecks, scenarios, sessions,
/// preferences) inherits the auth behavior without code duplication.
/// </summary>
public sealed class AuthenticatingHttpHandler : DelegatingHandler
{
    private readonly AuthTokenStore _tokenStore;
    private readonly AuthApiClient _authClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthenticatingHttpHandler(AuthTokenStore tokenStore, AuthApiClient authClient)
    {
        _tokenStore = tokenStore;
        _authClient = authClient;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.GetAsync();
        if (tokens is not null && !string.IsNullOrEmpty(tokens.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
        if (tokens is null || string.IsNullOrEmpty(tokens.RefreshToken)) return response;

        // Single-flight refresh: while one request is refreshing, others wait.
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring lock — another waiter may have refreshed.
            var current = await _tokenStore.GetAsync();
            if (current is null) return response;

            AuthTokens? refreshed = null;
            if (current.AccessToken == tokens.AccessToken)
            {
                var result = await _authClient.RefreshAsync(current.RefreshToken, current.Email, cancellationToken);
                if (result.IsSuccess && result.Tokens is not null)
                {
                    refreshed = result.Tokens;
                    await _tokenStore.SaveAsync(refreshed);
                }
                else
                {
                    // Refresh failed — sign out so the UI prompts to log back in.
                    await _tokenStore.ClearAsync();
                    return response;
                }
            }
            else
            {
                refreshed = current;
            }

            response.Dispose();
            var retry = await CloneAsync(request, cancellationToken);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
            return await base.SendAsync(retry, cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
