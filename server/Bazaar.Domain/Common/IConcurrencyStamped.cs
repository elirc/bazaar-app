namespace Bazaar.Domain.Common;

/// <summary>
/// Marks an aggregate that carries an optimistic-concurrency token. The stamp is refreshed on every
/// update (in the DbContext) and included in the UPDATE predicate, so a stale write raises a
/// concurrency conflict instead of silently overwriting a newer state.
/// </summary>
public interface IConcurrencyStamped
{
    Guid ConcurrencyStamp { get; set; }
}
