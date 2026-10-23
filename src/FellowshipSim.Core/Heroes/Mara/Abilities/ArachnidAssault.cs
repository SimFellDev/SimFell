namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1212: "Deal 117.22% of Agility to all targets in a radius around Mara. The damage is increased per combo
/// point spent." Costs 45 Energy; damage falls off as sqrt(8/N) beyond 8 targets (game data targetCountFalloff). During
/// Matriarch Macabre two shadowy clones repeat it (<see cref="MatriarchMacabre"/>).
/// </summary>
public class ArachnidAssault : Finisher
{
    public const int AbilityId = 1212;
    public const string AbilityName = "Arachnid Assault";
    public const double Coefficient = 1.1722;
    public const double Cost = 45;
    public const double TargetThreshold = 8;

    public ArachnidAssault(Mara owner) : base(owner, AbilityId, AbilityName)
    {
        BaseCoefficient = Coefficient;
        EnergyCost = Cost;
        AoeTargetThreshold = TargetThreshold;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target, int points)
    {
        double multiplier = ComboPointMultiplier(points);
        DealAoeDamage(ctx, EnemiesAround(ctx, target), damageMultiplier: multiplier);
        double castDamage = CastDamageMultiplier, castCrit = CurrentCast?.CritChance ?? 0.0;
        // A clone strikes the enemies present as it starts one per server frame, with the falloff of all of them.
        Mara.Matriarch.Imitate(ctx, ctx.ActiveEnemies.Count, (c, power) =>
        {
            var enemies = EnemiesAround(c, target).ToArray();
            double falloff = enemies.Length > TargetThreshold ? Math.Sqrt(TargetThreshold / enemies.Length) : 1.0;
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                c.Schedule(SimTime.FromSeconds(i * PeriodicEffect.ServerFrameSeconds), cc =>
                {
                    if (!enemy.IsSleeping) StrikeOne(cc, enemy, multiplier * castDamage * power * falloff, castCrit);
                }, Name);
            }
        });
    }

    /// <summary>One hit of a clone's sweep, on <paramref name="enemy"/> alone (a replay strikes the log's next enemy).</summary>
    private void StrikeOne(SimulationContext ctx, EnemyTarget enemy, double damageMultiplier, double critChance) =>
        DealAoeDamage(ctx, [enemy], damageMultiplier: damageMultiplier, extraCritChance: critChance);

    private static IReadOnlyList<EnemyTarget> EnemiesAround(SimulationContext ctx, EnemyTarget? target) =>
        ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
}
