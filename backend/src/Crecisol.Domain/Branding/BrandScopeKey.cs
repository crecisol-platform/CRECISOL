namespace Crecisol.Domain.Branding;

/// <summary>
/// Immutable brand aggregate identity. Equality is NOT authentication,
/// tenant membership, authorization, host verification or resource ownership.
/// </summary>
public sealed record BrandScopeKey
{
    private BrandScopeKey(BrandScope scope, Guid scopeId)
    {
        Scope = scope;
        ScopeId = scopeId;
    }

    public BrandScope Scope { get; }
    public Guid ScopeId { get; }

    public static BrandScopeKey Create(BrandScope scope, Guid scopeId)
    {
        if (!Enum.IsDefined(scope))
            throw new InvalidOperationException("Unknown brand scope.");
        if ((scope == BrandScope.Platform && scopeId != Guid.Empty) ||
            (scope == BrandScope.Tenant && scopeId == Guid.Empty))
            throw new InvalidOperationException("Brand scope and identifier disagree.");
        return new BrandScopeKey(scope, scopeId);
    }
}
