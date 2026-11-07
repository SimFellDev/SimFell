namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Effect 1585: "Backstab - Caustic Poison": instant poison damage, Combo Point generation and a Critical Strike chance
/// bonus on Caustic Poison (Brooding Shadows' text). Creeping Death adds damage per Haste.
/// Combat logs (Fc8v6yWxD14GQMnV fight 19): all 37 hits crit.
/// </summary>
public class CausticPoison : MaraAbility
{
    public const int EffectId = 1585;
    public const string EffectName = "Caustic Poison";
    public const double Coefficient = 4.7893;
    public const double CritChance = 1.0;

    public CausticPoison(Mara owner) : base(owner, LogIds.Effect(EffectId), EffectName)
    {
        BaseCoefficient = Coefficient;
        CritChanceBonus = CritChance;
        HasCreepingDeath = true;
    }

    public void Strike(SimulationContext ctx, EnemyTarget target) => DealDamage(ctx, target);

    public override void Execute(SimulationContext ctx, EnemyTarget target) { }
}
