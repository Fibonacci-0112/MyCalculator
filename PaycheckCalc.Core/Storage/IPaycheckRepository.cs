using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Storage;

/// <summary>
/// Persistence abstraction for saved paychecks.
/// Defined in Core so it remains UI-agnostic; implementations live in the App project.
/// </summary>
public interface IPaycheckRepository
{
    Task<IReadOnlyList<SavedPaycheck>> GetAllAsync();
    Task<SavedPaycheck?> GetByIdAsync(Guid id);
    Task SaveAsync(SavedPaycheck paycheck);
    Task DeleteAsync(Guid id);
}
