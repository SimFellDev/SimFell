namespace FellowshipSim.Core.Heroes.Meiko.Abilities;

/// <summary>
/// Ability 1082: "Split your soul into the Sibling of Wind and the Sibling of Earth for up to 20 seconds. You are in control
/// of the Sibling of Wind. SIBLING OF EARTH: While active, the Sibling of Earth forces all enemies within a 1000 radius to
/// attack it." 180 second cooldown. Effect 1491 is on Meiko while the Sibling of Earth stands. Enemies' attacks are not
/// simulated: the sibling stands for its full duration.
/// </summary>
public class TwinSoulsBulwark : MeikoAbility
{
    public const int AbilityId = 1082;
    public const string AbilityName = "Twin Souls: Bulwark";
    public const double CooldownSeconds = 180.0;
    public const int BuffEffectId = 1491;
    /// <summary>Effect 1491 duration 20 (Meiko.TauntTotem.WindMode.Duration 20).</summary>
    public const double BuffSeconds = 20.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Twin Souls: Bulwark", BuffEffectId, BuffSeconds);

    /// <summary>The Sibling of Earth's duration (Shattering Obelisk raises it).</summary>
    public double SiblingSeconds { get; set; } = BuffSeconds;

    public TwinSoulsBulwark(Meiko owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        // Combat logs (7Zmfx9gq6HjvThzc fight 2 @ 14.326): Spirit Palm follows 0.57s after the cast.
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(SiblingSeconds == BuffSeconds ? Buff : Buff.WithDuration(SiblingSeconds), ctx.CurrentTime);
    }
}
