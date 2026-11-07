namespace FellowshipSim.Core.Heroes.Elarion.Abilities;

/// <summary>
/// Ability 1315: "Fire a well-aimed arrow at target enemy, dealing physical damage. Generates Focus." Has a cast time. Its
/// hits grant Celestial Impetus by chance (Celestial Shot's tooltip).
/// </summary>
public class FocusedShot : ElarionAbility
{
    public const int AbilityId = 1315;
    public const string AbilityName = "Focused Shot";
    /// <summary>Share of Agility.</summary>
    public const double Coefficient = 1.6164;
    public const double CastTimeSeconds = 1.5;
    /// <summary>Focus gained per cast, from game data.</summary>
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
