using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Core.Storage;

/// <summary>
/// Persistence abstraction for a user's in-progress session state across
/// the calculator, self-employment, and annual planner hubs. Peer to
/// <see cref="IPaycheckRepository"/> — defined in Core so it remains
/// UI-agnostic; implementations live in the Blazor head.
///
/// The repository operates on the current authenticated user implicitly
/// (resolved via an IUserContext in the implementation). Returns null
/// when no snapshot has been saved yet for the current user.
/// </summary>
public interface ISessionStateRepository
{
    Task<SessionStateSnapshot?> GetAsync();
    Task SaveAsync(SessionStateSnapshot snapshot);
}
