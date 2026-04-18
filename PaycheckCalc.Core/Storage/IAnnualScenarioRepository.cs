using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Storage;

/// <summary>
/// Persistence abstraction for saved annual tax scenarios.
/// Defined in Core so it remains UI-agnostic; implementations live in the App project.
///
/// Mirrors <see cref="IPaycheckRepository"/>.
/// </summary>
public interface IAnnualScenarioRepository
{
    Task<IReadOnlyList<SavedAnnualScenario>> GetAllAsync();
    Task<SavedAnnualScenario?> GetByIdAsync(Guid id);
    Task SaveAsync(SavedAnnualScenario scenario);
    Task DeleteAsync(Guid id);
}
