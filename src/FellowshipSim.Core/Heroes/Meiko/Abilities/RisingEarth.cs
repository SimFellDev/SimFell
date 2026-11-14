namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1104, a Fist Technique finisher (Earth Fist then Spirit Palm): "Uppercut target enemy, dealing 485.44% of
/// Agility. Grants you 2 charges of Earthfall. Earthfall: Your next Earthfist Barrage deals 100% increased damage. Can be
/// used during Global Cooldown". Logged under 1104.
/// </summary>
public class RisingEarth : MeikoFinisher
{
    public const int AbilityId = 1104;
    public const string AbilityName = "Rising Earth";
    /// <summary>Placeholder RisingEarthDmg: 485.44% of Agility.</summary>
    public const double Coefficient = 4.8544;
    /// <summary>Effect 1532 (maxStacks 2, infinite). Combat logs: each cast applies it at 2 stacks.</summary>
    public const int EarthfallEffectId = 1532;
    public const int EarthfallCharges = 2;

    public static readonly AuraDef Earthfall = AuraDef.Effect("Earthfall", EarthfallEffectId, maxStacks: EarthfallCharges);

    public RisingEarth(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Core, Technique.Fist, Technique.Palm)
    {
        TargetedOffensive = true;
        BaseCoefficient = Coefficient;
    }

    protected override void Strike(SimulationContext ctx, EnemyTarget target)
    {
        DealDamage(ctx, target);
        Owner.Auras.Apply(Earthfall, ctx.CurrentTime, EarthfallCharges);
    }
}
