namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1848: "Instantly shield the target, absorbing up to (2299.8% of Strength) damage. The shield lasts for up to 8
/// seconds. All damage dealt to Solar Shield is reduced by 50%. Can be used during Global Cooldown." Cooldown 15s, its
/// duration scaled by Haste; 182 Mana. The engine casts it on Xavian.
/// </summary>
public partial class SolarShield : XavianAbility
{
    /// <summary>Effect 2551: the shield, 8 seconds.</summary>
    public const int BuffEffectId = 2551;
    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, Constants.BuffDuration);

    /// <summary>Multipliers on the next shield's absorb, read as it is cast (Hallowed Shield).</summary>
    public List<Func<SimulationContext, double>> AbsorbMultipliers { get; } = new();

    /// <summary>Raised as a shield is cast, with its maximum absorb (Invictus, Hallowed Shield, Magic Ward).</summary>
    public event Action<SimulationContext, double>? Shielded;

    public SolarShield(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown, scalesWithHaste: true);
        ManaCost = AbilityCost;
        IgnoresGcd = true;
    }

    /// <summary>
    /// The shield's maximum absorb at <paramref name="ctx"/>'s time, with its absorb multipliers. The absorb scales with
    /// Expertise.
    /// </summary>
    public double MaxAbsorb(SimulationContext ctx)
    {
        var now = ctx.CurrentTime;
        double absorb = Constants.AbsorbStrengthCoefficient * Owner.PrimaryAttribute(now) * (1.0 + Owner.Expertise(now));
        foreach (var multiplier in AbsorbMultipliers) absorb *= multiplier(ctx);
        return absorb;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        PayMana(ctx);
        double absorb = MaxAbsorb(ctx);
        Xavian.Auras.Apply(Buff, ctx.CurrentTime);
        Shielded?.Invoke(ctx, absorb);
    }
}
