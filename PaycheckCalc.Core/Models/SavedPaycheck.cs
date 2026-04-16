namespace PaycheckCalc.Core.Models;

/// <summary>
/// Persistable unit representing a named paycheck calculation.
/// Wraps a <see cref="PaycheckInput"/> and <see cref="PaycheckResult"/>
/// with identity and user-facing metadata.
/// </summary>
public sealed class SavedPaycheck
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public required PaycheckInput Input { get; init; }
    public required PaycheckResult Result { get; init; }
}
