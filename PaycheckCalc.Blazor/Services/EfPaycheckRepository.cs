using Microsoft.EntityFrameworkCore;
using PaycheckCalc.Blazor.Auth;
using PaycheckCalc.Blazor.Data;
using PaycheckCalc.Blazor.Data.Entities;
using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Services;

/// <summary>
/// EF Core + SQLite implementation of <see cref="IPaycheckRepository"/>,
/// scoped to the current authenticated user. All queries filter by the
/// resolved user id; passing this filter is the only thing preventing
/// cross-user data leaks, so the multi-user isolation test in
/// <c>EfPaycheckRepositoryTest</c> is non-negotiable.
/// </summary>
public sealed class EfPaycheckRepository : IPaycheckRepository
{
    private readonly AppDbContext _db;
    private readonly IUserContext _userContext;

    public EfPaycheckRepository(AppDbContext db, IUserContext userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    public async Task<IReadOnlyList<SavedPaycheck>> GetAllAsync()
    {
        var userId = await RequireUserIdAsync();
        var entities = await _db.Paychecks
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
        return entities.Select(ToSavedPaycheck).ToList().AsReadOnly();
    }

    public async Task<SavedPaycheck?> GetByIdAsync(Guid id)
    {
        var userId = await RequireUserIdAsync();
        var entity = await _db.Paychecks
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == id);
        return entity is null ? null : ToSavedPaycheck(entity);
    }

    public async Task SaveAsync(SavedPaycheck paycheck)
    {
        var userId = await RequireUserIdAsync();
        var existing = await _db.Paychecks
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == paycheck.Id);

        if (existing is null)
        {
            _db.Paychecks.Add(new StoredPaycheck
            {
                Id = paycheck.Id,
                UserId = userId,
                Name = paycheck.Name,
                CreatedAt = paycheck.CreatedAt,
                UpdatedAt = paycheck.UpdatedAt,
                NetPay = paycheck.Result.NetPay,
                StateCode = paycheck.Result.State.ToString(),
                Input = paycheck.Input,
                Result = paycheck.Result
            });
        }
        else
        {
            existing.Name = paycheck.Name;
            existing.UpdatedAt = paycheck.UpdatedAt;
            existing.NetPay = paycheck.Result.NetPay;
            existing.StateCode = paycheck.Result.State.ToString();
            existing.Input = paycheck.Input;
            existing.Result = paycheck.Result;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var userId = await RequireUserIdAsync();
        var entity = await _db.Paychecks
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == id);
        if (entity is null) return;

        _db.Paychecks.Remove(entity);
        await _db.SaveChangesAsync();
    }

    private async Task<string> RequireUserIdAsync()
    {
        var userId = await _userContext.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("No authenticated user.");
        return userId;
    }

    private static SavedPaycheck ToSavedPaycheck(StoredPaycheck e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        Input = e.Input,
        Result = e.Result
    };
}
