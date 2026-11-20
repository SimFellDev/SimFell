using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 593: each Fire Frog conjured has a chance to instead conjure a giant Fire Toad that explodes onto the
/// target and enemies near it, dealing Fire Frog damage to the main target and to enemies near the main target (area
/// damage is reduced beyond 1 target). Fire Frogs always conjures one Fire Toad in addition to its normal Fire Frogs. Fire
/// Toad benefits from all bonuses that affect Fire Frogs. The game data has no record of the toad's own. Combat logs
/// (GWChJ8P7tn6fHgV3 fights 14-18, 453 explosions):
/// <list type="bullet">
/// <item>the explosion is logged as a Fire Frogs hit (effect 1827), a multiple of a frog's hit on the main target;</item>
/// <item>the enemies near it take a share of that, falling off with N, the enemies it hits, the main target among them;</item>
/// <item>it leaves the Fire Frogs burn: the target's ticks grow far above a frog's after one;</item>
/// <item>every Fire Frogs cast has one, shortly after it, as its frogs' first attacks.</item>
/// </list>
/// UNVERIFIED: the chance of a toad (the replay cannot tell a rolled toad from the cast's own; both are Fire Frogs hits), and
/// that a Pyrophibian Frenzy frog can come as a toad.
/// </summary>
public partial class FireToad : Legendary
{
    public override void Bind(Hero hero)
    {
        var frogs = hero.GetAbility<FireFrogs>();
        if (frogs == null) return;
        frogs.ToadChance = Constants.FireToadSpawnChance;
        frogs.ToadsPerCast = Constants.BonusFireToadNumToads;
        // The toad's damage is the frog's, with every bonus a frog's hit has and its burn.
        frogs.ToadExplosion = (ctx, target) =>
        {
            frogs.Attack.DealDamage(ctx, target, damageMultiplier: Constants.FireToadDamageScaler);
            var nearby = ctx.Outcomes.SelectTargets(frogs.Attack.DamageLogId, ctx.ActiveEnemiesExcept(target), ctx);
            int struck = nearby.Count + 1;
            double threshold = Constants.FireToadTargetCountDamageScalingThreshold;
            double falloff = struck > threshold ? Math.Sqrt(threshold / struck) : 1.0;
            foreach (var enemy in nearby) frogs.Attack.DealDamage(ctx, enemy, damageMultiplier: Constants.FireToadAoeDamageMultiplier * falloff);
        };
    }
}
