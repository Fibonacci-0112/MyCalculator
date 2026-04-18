namespace PaycheckCalc.Core.Models;

/// <summary>
/// Persistable unit representing a named annual Form 1040 scenario.
/// Wraps a <see cref="TaxYearProfile"/> and an optional cached
/// <see cref="AnnualTaxResult"/> with identity and user-facing metadata.
///
/// Peer to <see cref="SavedPaycheck"/> — annual scenarios are to the
/// Form 1040 engine what saved paychecks are to the per-period engine.
/// </summary>
public sealed class SavedAnnualScenario
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Full annual profile — every input Form 1040 needs.</summary>
    public required TaxYearProfile Profile { get; init; }

    /// <summary>
    /// Optional cached computation result. Rehydration callers may treat
    /// this as an advisory snapshot and recompute when needed.
    /// </summary>
    public AnnualTaxResult? Result { get; init; }
}
