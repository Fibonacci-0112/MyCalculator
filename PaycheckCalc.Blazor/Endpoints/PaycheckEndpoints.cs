using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Endpoints;

/// <summary>
/// Minimal API endpoints for saved-paycheck CRUD. Bound to
/// <see cref="IPaycheckRepository"/>; the Phase 2 DI swap points that at
/// <c>EfPaycheckRepository</c>. The MAUI client consumes this surface.
/// </summary>
public static class PaycheckEndpoints
{
    public static IEndpointConventionBuilder MapPaycheckEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/paychecks")
            .WithTags("Paychecks")
            .RequireAuthorization();

        group.MapGet("/", async (IPaycheckRepository repo) =>
            Results.Ok(await repo.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, IPaycheckRepository repo) =>
        {
            var paycheck = await repo.GetByIdAsync(id);
            return paycheck is null ? Results.NotFound() : Results.Ok(paycheck);
        });

        group.MapPut("/{id:guid}", async (Guid id, SavedPaycheck paycheck, IPaycheckRepository repo) =>
        {
            if (paycheck.Id != id)
                return Results.BadRequest("Body id does not match URL id.");
            await repo.SaveAsync(paycheck);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, IPaycheckRepository repo) =>
        {
            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        return group;
    }
}
