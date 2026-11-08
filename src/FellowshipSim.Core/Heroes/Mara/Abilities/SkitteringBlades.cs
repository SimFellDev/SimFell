namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1213: spins and throws daggers in all directions, dealing Agility damage to all nearby enemies. Generates
/// Combo Points for each enemy hit, more on a Critical Strike. Costs Energy; damage falls off with the target count beyond
/// a threshold (game data targetCountFalloff). In Stealth the Stealth Skittering Blades
/// (<see cref="StealthSkitteringBlades"/>) replaces it.
/// </summary>
public partial class SkitteringBlades : MaraAbility
{
    public SkitteringBlades(Mara owner) : this(owner, AbilityId, AbilityName)
    {
        UnusableInStealth = true;
    }

    /// <summary>For the Stealth Skittering Blades: it is built on Skittering Blades.</summary>
    protected SkitteringBlades(Mara owner, int id, string name) : base(owner, id, name, AbilityCategory)
    {
        BaseCoefficient = AgilityCoefficient;
        EnergyCost = AbilityCost;
        AoeTargetThreshold = NumTargetsDamageFalloffThreshold;
        BreaksStealth = true;
        Hit += (in hit) =>
        {
            if (hit.SimContext is { } ctx) GenerateComboPoints(ctx, hit.IsCrit ? ComboPointGainAtCrit : ComboPointGainPerHit, hit.Target);
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Pay(ctx);
        DealAoeDamage(ctx, EnemiesAround(ctx, target));
    }

    /// <summary>The enemies around Mara: every enemy in combat, or the target alone when none is.</summary>
    protected static IReadOnlyList<EnemyTarget> EnemiesAround(SimulationContext ctx, EnemyTarget? target) =>
        ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
}
