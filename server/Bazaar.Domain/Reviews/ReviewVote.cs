namespace Bazaar.Domain.Reviews;

/// <summary>Records that a customer found a review helpful. Unique per (review, customer) to block double-voting.</summary>
public class ReviewVote
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ReviewId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
