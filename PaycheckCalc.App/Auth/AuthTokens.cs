namespace PaycheckCalc.App.Auth;

/// <summary>
/// Tokens returned by the server's /api/auth/login and /api/auth/refresh
/// endpoints. Wraps the access + refresh tokens plus the access-token expiry
/// and a cached user id (parsed from the access token's NameIdentifier claim
/// on sign-in so the client doesn't have to decode JWTs).
/// </summary>
public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string UserId,
    string Email);
