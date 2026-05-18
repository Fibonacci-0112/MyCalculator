using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PaycheckCalc.Blazor.Data;

namespace PaycheckCalc.Blazor.Endpoints;

/// <summary>
/// Form-bound minimal API endpoints that back the Blazor Account/Login,
/// Account/Register, and external-provider buttons. These cannot live inside
/// an interactive Blazor component because cookie sign-in modifies the
/// response headers, which is only possible during a regular HTTP request —
/// not during an interactive circuit update where the response is long gone.
///
/// The Blazor pages render plain HTML forms that POST here; this handler
/// calls SignInManager / UserManager and returns a redirect.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/account").WithTags("Account");

        group.MapPost("/login-cookie", LoginAsync);
        group.MapPost("/register-cookie", RegisterAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapPost("/external-login", StartExternalLogin);
        group.MapGet("/external-callback", ExternalCallbackAsync);

        return routes;
    }

    private static async Task<IResult> LoginAsync(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl,
        SignInManager<ApplicationUser> signInManager)
    {
        var result = await signInManager.PasswordSignInAsync(
            email, password, isPersistent: true, lockoutOnFailure: false);

        if (result.Succeeded)
            return Results.LocalRedirect(SanitizeReturnUrl(returnUrl));

        var msg = result.IsLockedOut ? "Account locked." : "Invalid email or password.";
        return Results.Redirect($"/account/login?error={Uri.EscapeDataString(msg)}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    private static async Task<IResult> RegisterAsync(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var msg = string.Join(", ", createResult.Errors.Select(e => e.Description));
            return Results.Redirect($"/account/register?error={Uri.EscapeDataString(msg)}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.LocalRedirect(SanitizeReturnUrl(returnUrl));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.LocalRedirect("/");
    }

    private static IResult StartExternalLogin(
        [FromForm] string provider,
        [FromForm] string? returnUrl,
        SignInManager<ApplicationUser> signInManager)
    {
        var redirect = $"/account/external-callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirect);
        return Results.Challenge(properties, new[] { provider });
    }

    private static async Task<IResult> ExternalCallbackAsync(
        [FromQuery] string? returnUrl,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return Results.Redirect("/account/login?error=External+login+failed");

        var result = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (result.Succeeded)
            return Results.LocalRedirect(SanitizeReturnUrl(returnUrl));

        // New user — create from the external email and link the external login.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
            return Results.Redirect("/account/login?error=External+provider+did+not+return+an+email");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var create = await userManager.CreateAsync(user);
        if (!create.Succeeded)
        {
            var msg = string.Join(", ", create.Errors.Select(e => e.Description));
            return Results.Redirect($"/account/login?error={Uri.EscapeDataString(msg)}");
        }

        var link = await userManager.AddLoginAsync(user, info);
        if (!link.Succeeded)
        {
            var msg = string.Join(", ", link.Errors.Select(e => e.Description));
            return Results.Redirect($"/account/login?error={Uri.EscapeDataString(msg)}");
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.LocalRedirect(SanitizeReturnUrl(returnUrl));
    }

    /// <summary>
    /// Constrains return URLs to local paths. Returning anything else would
    /// let an attacker craft a phishing redirect after a successful login.
    /// </summary>
    private static string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)) return "/";
        if (!returnUrl.StartsWith("/")) return "/";
        if (returnUrl.StartsWith("//")) return "/";
        return returnUrl;
    }
}
