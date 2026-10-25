namespace FellowshipSim.Core.Heroes.Tariq.Abilities;

/// <summary>
/// Ability 1235: "Call on thunder to charge your hammer with immense power, enhancing your abilities for 21 seconds. Can
/// be used during Global Cooldown." Effect 1772: "Your attacks are enhanced by lightning and you have access to your Chain
/// Lightning ability."
/// </summary>
public class ThunderCall : TariqAbility
{
    public const int AbilityId = 1235;
    public const string AbilityName = "Thunder Call";
    /// <summary>Game data cooldown 45, scaled by cooldown reduction and recovery only.</summary>
    public const double CooldownSeconds = 45.0;
    public const int BuffEffectId = 1772;
    public const double BuffSeconds = 21.0;

    public static readonly AuraDef Buff = AuraDef.Effect("Thunder Call", BuffEffectId, BuffSeconds);

    public ThunderCall(Tariq owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    /// <summary>
    /// "If your Thundercall is off cooldown, it will be unable to be cast for the duration of Raging Tempest"
    /// (icy-veins.com Tariq guide). FellowshipAnalyzer corpus (45 logs): no Thunder Call is applied during Raging Tempest.
    /// </summary>
    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (Owner.Auras.HasAura(RagingTempest.Buff, ctx.CurrentTime))
        {
            reason = explain ? $"{Name} cannot be cast during Raging Tempest" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        TriggerCooldown(ctx);
        Owner.Auras.Apply(Buff, ctx.CurrentTime);
    }
}
