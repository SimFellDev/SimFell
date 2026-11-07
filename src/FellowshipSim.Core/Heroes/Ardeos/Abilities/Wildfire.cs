namespace FellowshipSim.Core.Heroes.Ardeos.Abilities;

/// <summary>
/// Ability 1267 / effect 1825: all damage over time effects from the hero's abilities tick faster for a duration. Can be
/// used during Global Cooldown.
/// </summary>
public class Wildfire : ArdeosAbility
{
    public const int AbilityId = 1267;
    public const string AbilityName = "Wildfire";
    public const double CooldownSeconds = 45.0;
    public const int BuffEffectId = 1825;
    public const double BuffSeconds = 9.0;
    public const double TickRateMultiplier = 1.3;

    public static readonly AuraDef Buff = AuraDef.Effect("Wildfire", BuffEffectId, BuffSeconds);

    public Wildfire(Ardeos owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
        owner.Dots.RateMultipliers.Add(now => owner.Auras.HasAura(Buff, now) ? TickRateMultiplier : 1.0);
        owner.Dots.RateAuraKeys.Add(Buff.Key);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
