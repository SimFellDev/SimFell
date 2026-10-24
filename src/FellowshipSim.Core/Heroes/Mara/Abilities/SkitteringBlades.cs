namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1213: "Spins and throws daggers in all direction, dealing 80.6% of Agility to all nearby enemies. Generates 1
/// Combo Point for each enemy hit. Critical Strikes generate 2 Combo Points." Costs 35 Energy; damage falls off as
/// sqrt(8/N) beyond 8 targets (game data targetCountFalloff). In Stealth the Stealth Skittering Blades
/// (<see cref="StealthSkitteringBlades"/>) replaces it.
/// </summary>
public class SkitteringBlades : MaraAbility
{
    public const int AbilityId = 1213;
    public const string AbilityName = "Skittering Blades";
    public const double Coefficient = 0.806;
    public const double Cost = 35;
    public const double ComboPointsPerHit = 1;
    public const double ComboPointsOnCrit = 2;
    public const double TargetThreshold = 8;

    public SkitteringBlades(Mara owner) : this(owner, AbilityId, AbilityName)
    {
        UnusableInStealth = true;
    }

    /// <summary>For the Stealth Skittering Blades: it is built on Skittering Blades.</summary>
    protected SkitteringBlades(Mara owner, int id, string name) : base(owner, id, name, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        EnergyCost = Cost;
        AoeTargetThreshold = TargetThreshold;
        BreaksStealth = true;
        Hit += (in hit) =>
        {
            if (hit.SimContext is { } ctx) GenerateComboPoints(ctx, hit.IsCrit ? ComboPointsOnCrit : ComboPointsPerHit, hit.Target);
        };
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Pay(ctx);
        GenerateMaidenComboPoints(ctx, target);
        DealAoeDamage(ctx, EnemiesAround(ctx, target));
    }

    /// <summary>The enemies around Mara: every enemy in combat, or the target alone when none is.</summary>
    protected static IReadOnlyList<EnemyTarget> EnemiesAround(SimulationContext ctx, EnemyTarget? target) =>
        ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
}
