namespace Crecisol.Domain.Tenancy;

/// <summary>
/// Opaque tenant identity. A known tenant identifier NEVER proves membership,
/// ownership, host verification, or permission to read tenant data.
/// </summary>
public sealed record TenantId
{
    private TenantId(Guid value) => Value = value;
    public Guid Value { get; }

    public static TenantId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidOperationException("A tenant identifier is required.");
        return new TenantId(value);
    }
}
