using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Storage;

/// <summary>
/// Persistence abstraction for per-user defaults (default state, filing
/// status, frequency, OT multiplier). Returns null when the user has not
/// saved any preferences yet — callers should fall back to global defaults.
/// </summary>
public interface IUserPreferencesRepository
{
    Task<UserPreferences?> GetAsync();
    Task SaveAsync(UserPreferences preferences);
}
