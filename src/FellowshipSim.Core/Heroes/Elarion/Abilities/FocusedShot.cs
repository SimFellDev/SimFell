namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1315: "Fire a well-aimed arrow at target enemy, dealing physical damage. Generates 20 Focus." 1.5s cast. Its
/// hits grant Celestial Impetus (Celestial Shot's tooltip: "a chance ... (2.0 PPM)").
/// </summary>
public class FocusedShot : ElarionAbility
{
    public const int AbilityId = 1315;
    public const string AbilityName = "Focused Shot";
    /// <summary>Placeholder FocusedShot: 161.64% of Agility.</summary>
    public const double Coefficient = 1.6164;
    public const double CastTimeSeconds = 1.5;
    /// <summary>Bowguy.CastedProjectileDamage.FocusGain 20.</summary>
    public const double FocusGain = 20.0;

    /// <summary>Multiplier on the Focus gained (Deadly Focus).</summary>
    public double FocusGainMultiplier { get; set; } = 1.0;

    /// <summary>The arrow hit an enemy (Celestial Impetus, Focused Expanse).</summary>
    public event Action<SimulationContext, EnemyTarget>? Landed;

    public FocusedShot(Elarion owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Basic)
    {
        BaseCoefficient = Coefficient;
        CastTime = SimTime.FromSeconds(CastTimeSeconds);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        // Combat logs: the Focus comes as the cast completes, before the arrow lands.
        Elarion.GainFocus(FocusGain * FocusGainMultiplier, ctx, target);
        DealDamage(ctx, target, onLanded: (c, dealt) =>
        {
            if (dealt > 0) Landed?.Invoke(c, target);
        });
    }
}
