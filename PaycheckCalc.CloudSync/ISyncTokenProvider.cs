namespace PaycheckCalc.CloudSync;

public interface ISyncTokenProvider
{
    /// <summary>Returns the stored token, or creates and persists a new UUID if none exists.</summary>
    Task<string> GetOrCreateTokenAsync();
    /// <summary>Returns the stored token, or null if not yet created.</summary>
    Task<string?> GetTokenAsync();
    /// <summary>Persists a specific token (e.g. when a user imports one from another device).</summary>
    Task SetTokenAsync(string token);
}
