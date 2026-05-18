using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Endpoints;

/// <summary>
/// Minimal API endpoints for saved annual-scenario CRUD. Peer to
/// <see cref="PaycheckEndpoints"/>.
/// </summary>
public static class AnnualScenarioEndpoints
{
    public static IEndpointConventionBuilder MapAnnualScenarioEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/annual-scenarios")
            .WithTags("AnnualScenarios")
            .RequireAuthorization();

        group.MapGet("/", async (IAnnualScenarioRepository repo) =>
            Results.Ok(await repo.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, IAnnualScenarioRepository repo) =>
        {
            var scenario = await repo.GetByIdAsync(id);
            return scenario is null ? Results.NotFound() : Results.Ok(scenario);
        });

        group.MapPut("/{id:guid}", async (Guid id, SavedAnnualScenario scenario, IAnnualScenarioRepository repo) =>
        {
            if (scenario.Id != id)
                return Results.BadRequest("Body id does not match URL id.");
            await repo.SaveAsync(scenario);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, IAnnualScenarioRepository repo) =>
        {
            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        return group;
    }
}
