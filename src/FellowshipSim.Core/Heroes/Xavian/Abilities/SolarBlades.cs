namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1862: "Conjure blades of pure sunlight to fall from above upon your target, dealing magical damage to it and
/// other enemies within a 700 radius. The main target takes 100% more damage from Solar Blades. Can be used during Global
/// Cooldown. Solar Blades has a 100% chance to grant you 1 stack of Swift Reprieval."
/// </summary>
public class SolarBlades : XavianAbility
{
    public const int AbilityId = 1862;
    public const string AbilityName = "Solar Blades";
    /// <summary>Placeholder SolarBladesDmg: 161% of Strength.</summary>
    public const double Coefficient = 1.61;
    /// <summary>Tooltip: the main target takes 100% more damage.</summary>
    public const double MainTargetMultiplier = 2.0;
    /// <summary>Game data cooldown 9, its duration scaled by Haste.</summary>
    public const double CooldownSeconds = 9.0;
    /// <summary>Game data cost 55 Mana.</summary>
    public const double Cost = 55;
    /// <summary>Game data targetCountFalloff: effect 2576 (Solar Blades), threshold 5.</summary>
    public const double TargetThreshold = 5;

    /// <summary>Raised once per cast, with the main target and the damage dealt to it (Solar Burn).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? MainTargetHit;

    /// <summary>Raised once per cast, with every enemy struck (Solar Glare).</summary>
    public event Action<SimulationContext, IReadOnlyList<EnemyTarget>>? Struck;

    public SolarBlades(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core)
    {
        BaseCoefficient = Coefficient;
        SetCooldown(CooldownSeconds, scalesWithHaste: true);
        ManaCost = Cost;
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        PayMana(ctx);
        IReadOnlyList<EnemyTarget> enemies = ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
        // Combat logs (16 Xavian players, 27,127 hits): the main target takes its 200% whatever the count; every other
        // enemy loses damage as sqrt(5/N) over all N enemies struck, the main target counted (log/engine 0.99-1.01 at
        // every N from 2 to 17).
        double falloff = Falloff(struck.Count, TargetThreshold);
        for (int i = 0; i < struck.Count; i++)
        {
            var enemy = struck[i];
            if (enemy == target)
            {
                DealDamage(ctx, enemy, MainTargetMultiplier, onLanded: (c, dealt) => MainTargetHit?.Invoke(c, enemy, dealt));
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
