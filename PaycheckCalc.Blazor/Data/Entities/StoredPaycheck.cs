using PaycheckCalc.Core.Models;

namespace PaycheckCalc.Blazor.Data.Entities;

/// <summary>
/// EF Core entity that backs a <see cref="SavedPaycheck"/>. The deeply-nested
/// <see cref="PaycheckInput"/> and <see cref="PaycheckResult"/> are persisted
/// as JSON columns via value converters (see <c>AppDbContext.OnModelCreating</c>)
/// — they're never queried internally and contain a polymorphic
/// <c>StateInputValues</c> dictionary that would need a sidecar table to
/// model relationally.
///
/// A small set of flat columns (<see cref="NetPay"/>, <see cref="StateCode"/>)
/// are promoted out of the JSON so listing pages can render without
/// deserializing every row's full payload.
/// </summary>
public class StoredPaycheck
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Promoted from <see cref="PaycheckResult.NetPay"/> for indexed list queries.</summary>
    public decimal NetPay { get; set; }

    /// <summary>Promoted from <see cref="PaycheckResult.State"/> for indexed list queries.</summary>
    public string StateCode { get; set; } = "";

    public required PaycheckInput Input { get; set; }
    public required PaycheckResult Result { get; set; }
}
