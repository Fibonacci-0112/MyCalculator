using Microsoft.AspNetCore.Identity;

namespace PaycheckCalc.Blazor.Data;

/// <summary>
/// PaycheckCalc's ASP.NET Core Identity user. Extends <see cref="IdentityUser"/>
/// with a creation timestamp; everything else (email, password hash, external
/// logins, security stamp, ...) is inherited from Identity.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
