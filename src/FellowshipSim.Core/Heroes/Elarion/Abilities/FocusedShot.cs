namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1315: "Fire a well-aimed arrow at target enemy, dealing physical damage. Generates Focus." Has a cast time. Its
/// hits grant Celestial Impetus by chance (Celestial Shot's tooltip).
/// </summary>
public partial class FocusedShot : ElarionAbility
{
    /// <summary>Multiplier on the Focus gained (Deadly Focus).</summary>
    public double FocusGainMultiplier { get; set; } = 1.0;

    /// <summary>The arrow hit an enemy (Celestial Impetus, Focused Expanse).</summary>
    public event Action<SimulationContext, EnemyTarget>? Landed;

    public FocusedShot(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = Constants.AgilityCoefficient;
        CastTime = SimTime.FromSeconds(AbilityCastTime);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        // Combat logs: the Focus comes as the cast completes, before the arrow lands.
        Elarion.GainFocus(Constants.FocusGain * FocusGainMultiplier, ctx, target);
        DealDamage(ctx, target, onLanded: (c, dealt) =>
        {
            if (dealt > 0) Landed?.Invoke(c, target);
        });
    }
}
