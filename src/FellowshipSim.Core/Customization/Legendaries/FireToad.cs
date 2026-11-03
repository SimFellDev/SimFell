using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 593 (Ardeos's Pyromancer Helm, item 5899): "Each time you conjure a Fire Frog you have a 20% chance to
/// instead conjure a giant Fire Toad that explodes onto the target and enemies near it, dealing 900% Fire Frog damage to
/// the main target and 500% Fire Frog damage to enemies near the main target. (area damage is reduced beyond 1 target)
/// Your Fire Frogs ability always conjures 1 Fire Toad in addition to it's normal number of Fire Frogs. Fire Toad benefits
/// from all bonuses that affect Fire Frogs." The datamine has no record of the toad's own. Combat logs
/// (GWChJ8P7tn6fHgV3 fights 14-18, 453 explosions):
/// <list type="bullet">
/// <item>the explosion is logged as a Fire Frogs hit (effect 1827), 8.5 times a frog's hit on the main target;</item>
/// <item>the enemies near it take 500% times sqrt(1/N), N the enemies it hits, the main target among them (2 hit: 3.4
/// times a frog's hit, 6: 1.9, 12: 1.3);</item>
/// <item>it leaves the Fire Frogs burn: the target's ticks grow from a frog's ~150 to thousands after one;</item>
/// <item>every Fire Frogs cast has one, 1.5-5.4s after it, as its frogs' first attacks.</item>
/// </list>
/// UNVERIFIED: the 20% chance (the replay cannot tell a rolled toad from the cast's own; both are Fire Frogs hits), and
/// that a Pyrophibian Frenzy frog can come as a toad.
/// </summary>
public class FireToad : Legendary
{
    public const int PowerId = 593;
    public const int HelmItemId = 5899;
    public const double Chance = 0.20;
    public const int ToadsPerCast = 1;
    public const double MainTargetMultiplier = 9.0;
    public const double NearbyMultiplier = 5.0;
    public const double TargetThreshold = 1;

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Fire Toad";
    public override string HeroName => "Ardeos";
    public override IReadOnlyList<int> ItemIds => [HelmItemId];

    public override void Bind(Hero hero)
    {
        var frogs = hero.GetAbility<FireFrogs>();
        if (frogs == null) return;
        frogs.ToadChance = Chance;
        frogs.ToadsPerCast = ToadsPerCast;
        // The toad's damage is the frog's, with every bonus a frog's hit has and its burn.
        frogs.ToadExplosion = (ctx, target) =>
        {
            frogs.Attack.DealDamage(ctx, target, damageMultiplier: MainTargetMultiplier);
            var nearby = ctx.Outcomes.SelectTargets(frogs.Attack.DamageLogId, ctx.ActiveEnemiesExcept(target), ctx);
            int struck = nearby.Count + 1;
            double falloff = struck > TargetThreshold ? Math.Sqrt(TargetThreshold / struck) : 1.0;
            foreach (var enemy in nearby) frogs.Attack.DealDamage(ctx, enemy, damageMultiplier: NearbyMultiplier * falloff);
        };
    }
}
