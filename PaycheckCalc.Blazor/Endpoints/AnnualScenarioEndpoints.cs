using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Models;

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

        group.MapGet("/", async (EfAnnualScenarioRepository repo) =>
            Results.Ok(await repo.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, EfAnnualScenarioRepository repo) =>
        {
            var scenario = await repo.GetByIdAsync(id);
            return scenario is null ? Results.NotFound() : Results.Ok(scenario);
        });

        group.MapPut("/{id:guid}", async (Guid id, SavedAnnualScenario scenario, EfAnnualScenarioRepository repo) =>
        {
            if (scenario.Id != id)
                return Results.BadRequest("Body id does not match URL id.");
            await repo.SaveAsync(scenario);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, EfAnnualScenarioRepository repo) =>
        {
            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        return group;
    }
}
