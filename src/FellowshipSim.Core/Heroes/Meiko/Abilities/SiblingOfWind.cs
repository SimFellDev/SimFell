namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// The Sibling of Wind of Twin Souls: Army of One (ability 1109, logged as effect 1548): 131.6% of Agility per strike, on one
/// nearby enemy. Combat logs (27 fights, 98 casts, 7,649 strikes): a strike at the cast, then one every 0.25 seconds, not
/// hasted, for the buff's 20 seconds: 80 strikes, not the tooltip's 40. Not one of Meiko's abilities: Army of One starts it.
/// </summary>
public class SiblingOfWind : MeikoAbility
{
    public const int EffectId = 1548;
    public const string SiblingName = "Sibling of Wind";
    /// <summary>Placeholder ArmyOneDmg (Meiko.DurationalMultipleTeleportDamage.AgilityCoefficient): 131.6% of Agility.</summary>
    public const double Coefficient = 1.316;
    public const double StrikePeriodSeconds = 0.25;
    public const int Strikes = 80;

    public SiblingOfWind(Meiko owner) : base(owner, LogIds.Effect(EffectId), SiblingName)
    {
        BaseCoefficient = Coefficient;
        DamageLogId = LogIds.Effect(EffectId);
    }

    /// <summary>The Sibling of Wind starts striking, first at <paramref name="target"/>.</summary>
    public void Begin(SimulationContext ctx, EnemyTarget target) =>
        PeriodicEffect.Begin(ctx, Name, (Strikes - 1) * StrikePeriodSeconds, StrikePeriodSeconds, tickOnStart: true, partialFinalTick: false,
            onTick: (c, _) =>
            {
                IReadOnlyList<EnemyTarget> nearby = c.ActiveEnemies.Count == 0 && target is { IsSleeping: false } ? [target] : c.ActiveEnemies;
                if (c.Outcomes.ChooseTarget(DamageLogId, nearby, c) is { } enemy) DealDamage(c, enemy);
            },
            damageLogId: DamageLogId);

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
