namespace Crecisol.Domain.Common;

/// <summary>
/// Pure immutable aggregate revision. Not a permission, persistence lock,
/// transaction, concurrency guarantee or publication command.
/// </summary>
public sealed record DomainRevision
{
    private DomainRevision(long value) => Value = value;
    public long Value { get; }

    public static DomainRevision From(long value)
    {
        if (value < 1)
            throw new InvalidOperationException("Domain revision must be positive.");
        return new DomainRevision(value);
    }

    /// <summary>
    /// Domain-only sequence check. Infrastructure must still enforce atomic writes.
    /// </summary>
    public static DomainRevision RequireNewerThan(long candidate, long expectedCurrent)
    {
        if (expectedCurrent < 0)
            throw new InvalidOperationException("Expected revision cannot be negative.");
        var revision = From(candidate);
        if (candidate <= expectedCurrent)
            throw new InvalidOperationException("Proposed revision must advance.");
        return revision;
    }

    public DomainRevision Successor()
    {
        if (Value == long.MaxValue)
            throw new InvalidOperationException("Domain revision overflow.");
        return new DomainRevision(Value + 1);
    }
}
