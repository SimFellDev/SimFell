namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1724: Hemorrhaging Strike's bleed, dealing Agility damage on a period; each tick of the bleed replenishes
/// Energy. Creeping Death adds damage per Haste.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 32 bleeds): ticks are shortened by Haste, with a partial tick as it runs out;
/// a refresh keeps the tick timing, and up to <see cref="RefreshCarryover"/> of the new duration of the time left carries over (@ 21.458).
/// Every enemy's bleed pays Energy at each tick, a partial one too, with Maiden of Death's
/// multiplier: game data reads no partial tick factor for the Energy, and
/// the signal to the source ability runs in the partial tick's executions.
/// </summary>
public class Hemorrhage : MaraDot
{
    public const int EffectId = 1724;
    public const string EffectName = "Hemorrhage";
    public const double Coefficient = 0.9841;
    public const double TickSeconds = 3.0;
    public const double BaseDurationSeconds = 12.0;
    public const double EnergyPerTick = 2.0;
    public const double RefreshCarryover = 0.3;

    public Hemorrhage(Mara owner) : base(owner, EffectId, EffectName, Coefficient, TickSeconds, BaseDurationSeconds)
    {
        HasCreepingDeath = true;
        RefreshCarryoverFraction = RefreshCarryover;
    }

    public void ApplyTo(SimulationContext ctx, EnemyTarget target, double durationSeconds) => Apply(ctx, target, durationSeconds);

    protected override void DealTick(SimulationContext ctx, Instance dot, double fraction)
    {
        Mara.GainEnergyWithMaiden(EnergyPerTick, ctx);
        base.DealTick(ctx, dot, fraction);
    }

    /// <summary>The damage of one full tick on <paramref name="target"/> now, before crit (Hemotoxin).</summary>
    public double TickDamage(SimulationContext ctx, EnemyTarget target) => BaseDamage(ctx, target);

    /// <summary>Ticks in <paramref name="durationSeconds"/> at the tick rate now (Hemotoxin).</summary>
    public double TicksIn(double durationSeconds, SimTime now) => durationSeconds * (1.0 + Owner.GetHaste(now)) / HastedPeriod(now);
}
