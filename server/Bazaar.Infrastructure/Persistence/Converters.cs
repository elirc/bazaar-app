using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bazaar.Infrastructure.Persistence;

/// <summary>
/// SQLite cannot order or compare <see cref="DateTimeOffset"/> columns correctly, so we persist
/// them as UTC ticks (a plain <see cref="long"/>) which sorts and compares as expected.
/// </summary>
public sealed class DateTimeOffsetToUtcTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToUtcTicksConverter()
        : base(
            value => value.ToUniversalTime().Ticks,
            ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
    {
    }
}

/// <summary>
/// Persists a money amount (decimal) as integer minor units (cents, a <see cref="long"/>) so that
/// arithmetic and storage stay exact and free of SQLite's floating-point decimal handling.
/// </summary>
public sealed class DecimalToCentsConverter : ValueConverter<decimal, long>
{
    public DecimalToCentsConverter()
        : base(
            amount => (long)decimal.Round(amount * 100m, 0, MidpointRounding.ToEven),
            cents => cents / 100m)
    {
    }
}
