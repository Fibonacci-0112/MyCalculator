using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Blazor.Endpoints;

/// <summary>
/// Minimal API endpoints for saved-paycheck CRUD. Bound to
/// <see cref="EfPaycheckRepository"/> explicitly (not <c>IPaycheckRepository</c>)
/// because Phase 1 leaves the Blazor UI on the legacy localStorage repo —
/// these endpoints are the API the MAUI client will consume.
/// </summary>
public static class PaycheckEndpoints
{
    public static IEndpointConventionBuilder MapPaycheckEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/paychecks")
            .WithTags("Paychecks")
            .RequireAuthorization();

        group.MapGet("/", async (EfPaycheckRepository repo) =>
            Results.Ok(await repo.GetAllAsync()));

        group.MapGet("/{id:guid}", async (Guid id, EfPaycheckRepository repo) =>
        {
            var paycheck = await repo.GetByIdAsync(id);
            return paycheck is null ? Results.NotFound() : Results.Ok(paycheck);
        });

        group.MapPut("/{id:guid}", async (Guid id, SavedPaycheck paycheck, EfPaycheckRepository repo) =>
        {
            if (paycheck.Id != id)
                return Results.BadRequest("Body id does not match URL id.");
            await repo.SaveAsync(paycheck);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}", async (Guid id, EfPaycheckRepository repo) =>
        {
            await repo.DeleteAsync(id);
            return Results.NoContent();
        });

        return group;
    }
}
