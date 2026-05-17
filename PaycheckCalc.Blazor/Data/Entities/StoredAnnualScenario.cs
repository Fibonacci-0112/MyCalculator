using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Blazor.Data.Entities;

/// <summary>
/// EF Core entity that backs a <see cref="SavedAnnualScenario"/>. The
/// <see cref="TaxYearProfile"/> and optional <see cref="AnnualTaxResult"/>
/// are persisted as JSON columns via value converters.
/// </summary>
public class StoredAnnualScenario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public required TaxYearProfile Profile { get; set; }

    /// <summary>Optional cached computation result; advisory only.</summary>
    public AnnualTaxResult? Result { get; set; }
}
