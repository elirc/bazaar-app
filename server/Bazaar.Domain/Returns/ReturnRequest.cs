using Bazaar.Domain.Common;

namespace Bazaar.Domain.Returns;

/// <summary>
/// A return/refund request (RMA) against a fulfilled order. Aggregates the lines being returned and,
/// once approved, records the discount/tax-adjusted refund amount and gateway reference.
/// </summary>
public class ReturnRequest
{
    private readonly List<ReturnLine> _lines = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid? CustomerId { get; set; }

    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    public string? Reason { get; set; }

    /// <summary>Refund actually issued on approval (discount/tax-adjusted). Zero until approved.</summary>
    public Money RefundAmount { get; set; } = Money.Zero();
    public string? RefundReference { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ReturnLine> Lines => _lines;

    public ReturnLine AddLine(ReturnLine line)
    {
        line.ReturnRequestId = Id;
        _lines.Add(line);
        return line;
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
