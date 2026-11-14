namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1862: "Conjure blades of pure sunlight to fall from above upon your target, dealing magical damage to it and
/// other enemies within a 700 radius. The main target takes 100% more damage from Solar Blades. Can be used during Global
/// Cooldown. Solar Blades has a 100% chance to grant you 1 stack of Swift Reprieval."
/// </summary>
public partial class SolarBlades : XavianAbility
{
    /// <summary>Raised once per cast, with the main target and the damage dealt to it (Solar Burn).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? MainTargetHit;

    /// <summary>Raised once per cast, with every enemy struck (Solar Glare).</summary>
    public event Action<SimulationContext, IReadOnlyList<EnemyTarget>>? Struck;

    public SolarBlades(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.DamageStrengthCoefficient;
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        ManaCost = AbilityCost;
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        PayMana(ctx);
        var enemies = Area(ctx, target);
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        // Combat logs (16 Xavian players, 27,127 hits): the main target takes its 200% whatever the count; every other
        // enemy loses damage as sqrt(5/N) over all N enemies struck, the main target counted (log/engine 0.99-1.01 at
        // every N from 2 to 17).
        double falloff = Falloff(struck.Count, Constants.DamageScalingTargetCountThreshold);
        for (int i = 0; i < struck.Count; i++)
        {
            var enemy = struck[i];
            if (enemy == target)
            {
                DealDamage(ctx, enemy, Constants.MainTargetDamageScaler, onLanded: (c, dealt) => MainTargetHit?.Invoke(c, enemy, dealt));
            }
            else
            {
                DealDamage(ctx, enemy, falloff);
            }
        }
        Struck?.Invoke(ctx, struck);
        Xavian.GrantSwiftReprieval(ctx);
        Xavian.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
