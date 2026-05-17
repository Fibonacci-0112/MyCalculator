using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Data.Entities;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// EF Core + SQLite implementation of <see cref="ISessionStateRepository"/>.
/// Persists the three in-progress hub snapshots as opaque JSON strings; the
/// Blazor session-state services own the (de)serialization of their own
/// snapshot records.
/// </summary>
public sealed class EfSessionStateRepository : ISessionStateRepository
{
    private readonly AppDbContext _db;
    private readonly IUserContext _userContext;

    public EfSessionStateRepository(AppDbContext db, IUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<SessionStateSnapshot?> GetAsync()
    {
        var userId = await RequireUserIdAsync();
        var entity = await _db.SessionStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);
        if (entity is null) return null;
        return new SessionStateSnapshot(
            entity.CalculatorState,
            entity.SelfEmploymentState,
            entity.AnnualTaxState,
            entity.UpdatedAt);
    }

    public async Task SaveAsync(SessionStateSnapshot snapshot)
    {
        var userId = await RequireUserIdAsync();
        var existing = await _db.SessionStates
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (existing is null)
        {
            _db.SessionStates.Add(new UserSessionStateEntity
            {
                UserId = userId,
                CalculatorState = snapshot.CalculatorState,
                SelfEmploymentState = snapshot.SelfEmploymentState,
                AnnualTaxState = snapshot.AnnualTaxState,
                UpdatedAt = snapshot.UpdatedAt
            });
        }
        else
        {
            existing.CalculatorState = snapshot.CalculatorState;
            existing.SelfEmploymentState = snapshot.SelfEmploymentState;
            existing.AnnualTaxState = snapshot.AnnualTaxState;
            existing.UpdatedAt = snapshot.UpdatedAt;
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
