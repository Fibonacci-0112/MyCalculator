using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Blazor.Endpoints;

/// <summary>
/// Minimal API endpoints for the per-user session-state blob. The PUT
/// accepts the full <see cref="SessionStateSnapshot"/> in one round-trip
/// — we do not split the three hub blobs into separate routes because
/// the round-trip cost dwarfs the JSON size.
/// </summary>
public static class SessionEndpoints
{
    public static IEndpointConventionBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/session")
            .WithTags("Session")
            .RequireAuthorization();

        group.MapGet("/", async (EfSessionStateRepository repo) =>
        {
            var snapshot = await repo.GetAsync();
            return snapshot is null ? Results.NoContent() : Results.Ok(snapshot);
        });

        group.MapPut("/", async (SessionStateSnapshot snapshot, EfSessionStateRepository repo) =>
        {
            await repo.SaveAsync(snapshot);
            return Results.NoContent();
        });

        return group;
    }
}
