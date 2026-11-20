namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 984: "Raise your shield, increasing your Toughness by 30% and increasing your Block chance by 25% for 12 sec.
/// Shields Up gains Cooldown Acceleration equal to your Haste. Each time you succesfully Block an attack, you generate 3%
/// Toughness. Can be used during Global Cooldown." 2 charges, 30s each. The buff is effect 1282.
/// </summary>
public partial class ShieldsUp : HelenaAbility
{
    public const int BuffEffectId = 1282;
    /// <summary>Toughness per cast, as a share of 770% of Strength (game data Warmaster.Toughness.DefenceBuff.StrengthScaler).</summary>
    public const double ToughnessShare = 0.3;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, AbilityDuration);

    /// <summary>Raised as the cast resolves (High Command).</summary>
    public event Action<SimulationContext>? Raised;

    public ShieldsUp(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, AbilityCharges, scalesWithHaste: true);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Helena.Auras.Apply(Buff, ctx.CurrentTime);
        Helena.GainToughness(ToughnessShare, ctx);
        Raised?.Invoke(ctx);
    }
}
