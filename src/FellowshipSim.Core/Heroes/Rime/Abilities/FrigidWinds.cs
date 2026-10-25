namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>
/// Ability 1021: "Blast enemies in front of you with arctic winds, Knocking Back enemies and Slowing them by 50% for 12
/// sec. Can be used during Global Cooldown." Cooldown 60s; effect 1385 "Frigid Winds", 12s, MoveSpeed x0.5 (game data).
/// No damage. The enemies in front are the target alone.
/// </summary>
public class FrigidWinds : RimeAbility
{
    public const int AbilityId = 1021;
    public const string AbilityName = "Frigid Winds";
    public const double CooldownSeconds = 60.0;
    public const int DebuffEffectId = 1385;
    public const double DebuffSeconds = 12.0;

    public static readonly AuraDef Debuff = AuraDef.Effect(AbilityName, DebuffEffectId, DebuffSeconds);

    public FrigidWinds(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Control)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        target?.Auras.Apply(Debuff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }
}
