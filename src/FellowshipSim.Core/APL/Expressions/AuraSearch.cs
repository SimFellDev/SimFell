namespace FellowshipSim.Core.APL.Expressions;

/// <summary>
/// The search of one aura container for the active aura of one action-list name. The result holds until an aura of the
/// container is added, removed or replaced (<see cref="AuraContainer.Membership"/>), so a search runs again only then.
/// </summary>
internal struct AuraSearch
{
    private AuraContainer? _auras;
    private int _membership;
    private Aura? _found;

    /// <summary>The active aura of <paramref name="auras"/> whose action-list name or key is <paramref name="slug"/>, or null.</summary>
    public Aura? Find(AuraContainer auras, string slug, SimTime now)
    {
        auras.CleanExpired(now);
        if (!ReferenceEquals(auras, _auras) || auras.Membership != _membership)
        {
            _found = auras.FindActive(slug, now);
            _auras = auras;
            _membership = auras.Membership;
        }
        return _found;
    }
}
