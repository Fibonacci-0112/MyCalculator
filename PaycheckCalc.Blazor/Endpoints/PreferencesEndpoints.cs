using PaycheckCalc.Core.Models;
using PaycheckCalc.Core.Storage;

namespace PaycheckCalc.Blazor.Endpoints;

/// <summary>
/// Minimal API endpoints for per-user preferences.
/// </summary>
public static class PreferencesEndpoints
{
    public static IEndpointConventionBuilder MapPreferencesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/preferences")
            .WithTags("Preferences")
            .RequireAuthorization();

        group.MapGet("/", async (IUserPreferencesRepository repo) =>
        {
            var prefs = await repo.GetAsync();
            return prefs is null ? Results.NoContent() : Results.Ok(prefs);
        });

        group.MapPut("/", async (UserPreferences prefs, IUserPreferencesRepository repo) =>
        {
            await repo.SaveAsync(prefs);
            return Results.NoContent();
        });

        return group;
    }
}
