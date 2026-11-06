namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1212: Agility damage to all targets in a radius around Mara. The damage is increased per combo
/// point spent. Costs Energy; damage falls off with the target count beyond <see cref="TargetThreshold"/> (game data targetCountFalloff). During
/// Matriarch Macabre shadowy clones repeat it (<see cref="MatriarchMacabre"/>).
/// Combat logs (the 23 Mara fights of `data/fights`): a clone's hit on an enemy is a fixed fraction of the cast's hit on that enemy
/// at every target count over 858 casts, so it has the cast's falloff, and nearly all clone hits over
/// 1,104 casts are on the enemies the cast struck.
/// Game data: the cast writes the damage scale for its target count into its spec, and passes that spec and the
/// cast's target data to the clone spawner. Each
/// clone shuffles that list and has no target query of its own, so a clone's hit takes the falloff of the cast's target count.
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
        var struck = ctx.Outcomes.SelectTargets(DamageLogId, EnemiesAround(ctx, target), ctx).ToArray();
        double falloff = struck.Length > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Length) : 1.0;
        // Read before the cast's own hits and their procs.
        var clones = Mara.Matriarch.KeepCast(this, ctx, multiplier * falloff * CastDamageMultiplier, CurrentCast?.CritChance ?? 0.0);
        foreach (var enemy in struck) DealDamage(ctx, enemy, damageMultiplier: multiplier * falloff);
        // A clone strikes the enemies the cast struck, one per server frame, with the cast's falloff.
        Mara.Matriarch.Imitate(ctx, struck.Length, (c, _) =>
        {
            for (int i = 0; i < struck.Length; i++)
            {
                var enemy = struck[i];
                c.Schedule(SimTime.FromSeconds(i * PeriodicEffect.ServerFrameSeconds), cc =>
                {
                    if (enemy.IsSleeping) return;
                    // A replay strikes the log's next enemy: each clone's order is its own.
                    var next = cc.Outcomes.ChooseTarget(DamageLogId, struck, cc, preferred: enemy) ?? enemy;
                    DealKeptHit(cc, next, Mara.Matriarch.StrikeNumbers(cc, clones, next), MatriarchMacabre.CloneHitName(Name));
                }, Name);
            }
        });
    }

    private static IReadOnlyList<EnemyTarget> EnemiesAround(SimulationContext ctx, EnemyTarget? target) =>
        ctx.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : ctx.ActiveEnemies;
}
