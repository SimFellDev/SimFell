namespace FellowshipSim.Core.Heroes.Helena.Abilities;

/// <summary>
/// Ability 973: "Your Toughness can not be reduced for 12 seconds. Can be used during Global Cooldown." Cooldown 90s. The
/// buff is effect 1254.
/// </summary>
public class IronWall : HelenaAbility
{
    public const int AbilityId = 973;
    public const string AbilityName = "Iron Wall";
    public const double CooldownSeconds = 90.0;
    public const int BuffEffectId = 1254;
    public const double BuffSeconds = 12.0;

    public static readonly AuraDef Buff = AuraDef.Effect(AbilityName, BuffEffectId, BuffSeconds);

    public IronWall(Helena owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Helena.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
