using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Data.Entities;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// EF Core + SQLite implementation of <see cref="IAnnualScenarioRepository"/>,
/// scoped to the current authenticated user. Peer to
/// <see cref="EfPaycheckRepository"/>.
/// </summary>
public sealed class EfAnnualScenarioRepository : IAnnualScenarioRepository
{
    private readonly AppDbContext _db;
    private readonly IUserContext _userContext;

    public EfAnnualScenarioRepository(AppDbContext db, IUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<SavedAnnualScenario>> GetAllAsync()
    {
        // Anonymous users get an empty list rather than an exception so the
        // Annual Results page renders gracefully when logged out.
        var userId = await _userContext.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
            return Array.Empty<SavedAnnualScenario>();

        var entities = await _db.AnnualScenarios
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
        return entities.Select(ToSavedScenario).ToList().AsReadOnly();
    }

    public async Task<SavedAnnualScenario?> GetByIdAsync(Guid id)
    {
        var userId = await _userContext.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
            return null;

        var entity = await _db.AnnualScenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id);
        return entity is null ? null : ToSavedScenario(entity);
    }

    public async Task SaveAsync(SavedAnnualScenario scenario)
    {
        var userId = await RequireUserIdAsync();
        var existing = await _db.AnnualScenarios
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Id == scenario.Id);

        if (existing is null)
        {
            _db.AnnualScenarios.Add(new StoredAnnualScenario
            {
                Id = scenario.Id,
                UserId = userId,
                Name = scenario.Name,
                CreatedAt = scenario.CreatedAt,
                UpdatedAt = scenario.UpdatedAt,
                Profile = scenario.Profile,
                Result = scenario.Result
            });
        }
        else
        {
            existing.Name = scenario.Name;
            existing.UpdatedAt = scenario.UpdatedAt;
            existing.Profile = scenario.Profile;
            existing.Result = scenario.Result;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var userId = await RequireUserIdAsync();
        var entity = await _db.AnnualScenarios
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Id == id);
        if (entity is null) return;

        _db.AnnualScenarios.Remove(entity);
        await _db.SaveChangesAsync();
    }

    private async Task<string> RequireUserIdAsync()
    {
        var userId = await _userContext.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("No authenticated user.");
        return userId;
    }

    private static SavedAnnualScenario ToSavedScenario(StoredAnnualScenario e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        Profile = e.Profile,
        Result = e.Result
    };
}
