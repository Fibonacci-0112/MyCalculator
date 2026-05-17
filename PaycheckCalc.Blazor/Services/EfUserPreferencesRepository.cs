using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Data.Entities;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// EF Core + SQLite implementation of <see cref="IUserPreferencesRepository"/>.
/// One row per user; <see cref="GetAsync"/> returns null when no preferences
/// have been saved (callers fall back to global defaults).
/// </summary>
public sealed class EfUserPreferencesRepository : IUserPreferencesRepository
{
    private readonly AppDbContext _db;
    private readonly IUserContext _userContext;

    public EfUserPreferencesRepository(AppDbContext db, IUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<UserPreferences?> GetAsync()
    {
        var userId = await RequireUserIdAsync();
        var entity = await _db.Preferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (entity is null) return null;
        return new UserPreferences(
            entity.DefaultState,
            entity.DefaultFilingStatus,
            entity.DefaultFrequency,
            entity.DefaultOvertimeMultiplier,
            entity.UpdatedAt);
    }

    public async Task SaveAsync(UserPreferences preferences)
    {
        var userId = await RequireUserIdAsync();
        var existing = await _db.Preferences
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (existing is null)
        {
            _db.Preferences.Add(new UserPreferencesEntity
            {
                UserId = userId,
                DefaultState = preferences.DefaultState,
                DefaultFilingStatus = preferences.DefaultFilingStatus,
                DefaultFrequency = preferences.DefaultFrequency,
                DefaultOvertimeMultiplier = preferences.DefaultOvertimeMultiplier,
                UpdatedAt = preferences.UpdatedAt
            });
        }
        else
        {
            existing.DefaultState = preferences.DefaultState;
            existing.DefaultFilingStatus = preferences.DefaultFilingStatus;
            existing.DefaultFrequency = preferences.DefaultFrequency;
            existing.DefaultOvertimeMultiplier = preferences.DefaultOvertimeMultiplier;
            existing.UpdatedAt = preferences.UpdatedAt;
        }

        await _db.SaveChangesAsync();
    }

    private async Task<string> RequireUserIdAsync()
    {
        var userId = await _userContext.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("No authenticated user.");
        return userId;
    }
}
