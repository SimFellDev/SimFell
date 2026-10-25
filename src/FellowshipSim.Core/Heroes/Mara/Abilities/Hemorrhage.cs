namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1724: Hemorrhaging Strike's bleed, "dealing 98.41% of Agility every 3 seconds"; "Each tick of Hemorrhaging
/// Strike's bleed replenishes 2 energy." Creeping Death adds 0.5% per 1% Haste.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19, 32 bleeds): ticks every 3s shortened by Haste, a partial tick as it runs out;
/// a refresh keeps the tick timing, and up to 30% of the new duration of the time left carries over (@ 21.458: 27.1s
/// left, a 30s refresh, gone 39.0s later).
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
        Mara.GainEnergy(EnergyPerTick, ctx);
        base.DealTick(ctx, dot, fraction);
    }

    /// <summary>The damage of one full tick on <paramref name="target"/> now, before crit (Hemotoxin).</summary>
    public double TickDamage(SimulationContext ctx, EnemyTarget target) => BaseDamage(ctx, target);

    /// <summary>Ticks in <paramref name="durationSeconds"/> at the tick rate now (Hemotoxin).</summary>
    public double TicksIn(double durationSeconds, SimTime now) => durationSeconds * (1.0 + Owner.GetHaste(now)) / HastedPeriod(now);
}
