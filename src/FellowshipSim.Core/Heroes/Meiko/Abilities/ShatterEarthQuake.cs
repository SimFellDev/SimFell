namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Shatter Earth's quaking earth (effect 1556, logged as such): ShatterEarthTickDmg 142% of Agility to every enemy in it,
/// every hasted second (TickInterval 1) for 5.3 seconds (ability 1115 duration 5.3). Combat logs (7Zmfx9gq6HjvThzc fight 2
/// @ 17.331): the first tick comes one hasted period after the slam (0.77s at 30% Haste). The quake has no target falloff
/// in the game data. Not one of Meiko's abilities: Shatter Earth starts it.
/// </summary>
public class ShatterEarthQuake : MeikoAbility
{
    public const int EffectId = 1556;
    public const string QuakeName = "Shatter Earth (quake)";
    /// <summary>Placeholder ShatterEarthTickDmg (Dot.AgilityCoefficient): 142% of Agility.</summary>
    public const double Coefficient = 1.42;
    public const double DurationSeconds = 5.3;
    public const double PeriodSeconds = 1.0;

    public ShatterEarthQuake(Meiko owner) : base(owner, LogIds.Effect(EffectId), QuakeName)
    {
        BaseCoefficient = Coefficient;
        DamageLogId = LogIds.Effect(EffectId);
    }

    /// <summary>The quake starts where the slam landed.</summary>
    public void Begin(SimulationContext ctx) =>
        PeriodicEffect.BeginHasted(ctx, Owner, Name, DurationSeconds, PeriodSeconds, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) => DealAoeDamage(c, c.ActiveEnemies), damageLogId: DamageLogId);

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
