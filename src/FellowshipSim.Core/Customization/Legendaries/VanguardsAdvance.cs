using FellowshipSim.Core.Heroes.Helena;
using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 391 (Helena's Warmaster Greaves, item 5900): "Your Charge ability triggers a free Shockwave upon impact.
/// Additionally, enemies that take damage from your Shockwave deal 5% reduced physical damage to you for 5 seconds." The
/// debuff is effect 2429. UNVERIFIED: no log has it; the free Shockwave is taken to deal Shockwave's hits.
/// </summary>
public class VanguardsAdvance : Legendary
{
    public const int PowerId = 391;
    /// <summary>Warmaster Greaves.</summary>
    public const int GreavesItemId = 5900;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Vanguard's Advance";
    public override string HeroName => "Helena";
    public override IReadOnlyList<int> ItemIds => [GreavesItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Helena || hero.GetAbility<Charge>() is not { } charge || hero.GetAbility<Shockwave>() is not { } shockwave) return;
        charge.Impact += (ctx, target) => shockwave.Explode(ctx, target);
    }
}
