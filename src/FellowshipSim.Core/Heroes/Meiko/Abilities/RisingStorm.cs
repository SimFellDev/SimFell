namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1103, a Kick Technique finisher (Wind Kick then Spirit Palm): "Perform a powerful jump kick, dealing 242.56% of
/// Agility damage to your target and nearby enemies. Grants you 2 charges of Stormfall. Stormfall: Your next Lashing
/// Stormkick deals 100% increased damage. Can be used during Global Cooldown". Logged under 1103.
/// </summary>
public class RisingStorm : MeikoFinisher
{
    public const int AbilityId = 1103;
    public const string AbilityName = "Rising Storm";
    /// <summary>Placeholder RisingStrmDmg: 242.56% of Agility.</summary>
    public const double Coefficient = 2.4256;
    /// <summary>Game data targetCountFalloff: effect 1531, threshold 5.</summary>
    public const double TargetThreshold = 5;
    /// <summary>Effect 1530 (maxStacks 2, infinite). Combat logs: each cast applies it at 2 stacks.</summary>
    public const int StormfallEffectId = 1530;
    public const int StormfallCharges = 2;

    public static readonly AuraDef Stormfall = AuraDef.Effect("Stormfall", StormfallEffectId, maxStacks: StormfallCharges);

    public RisingStorm(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core, Technique.Kick, Technique.Palm)
    {
        BaseCoefficient = Coefficient;
        AoeTargetThreshold = TargetThreshold;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        DealAoeDamage(ctx, ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies);
        Owner.Auras.Apply(Stormfall, ctx.CurrentTime, StormfallCharges);
    }
}
