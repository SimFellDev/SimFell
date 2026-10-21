using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1032 / effect 1397: +20% damage for 20s, extended 0.1s per Frost Swallow hit. Usable while casting.</summary>
public class IceBlitz : RimeAbility
{
    public const int AbilityId = 1032;
    public const string AbilityName = "Ice Blitz";
    public const double CooldownSeconds = 60.0;
    public const int BuffEffectId = 1397;
    public const double BuffSeconds = 20.0;
    public const double BuffDamage = 1.2;
    public const double ExtensionPerSwallowSeconds = 0.1;

    public static readonly AuraDef Buff = AuraDef.Effect("Ice Blitz", BuffEffectId, BuffSeconds,
        modifiers: StatModifier.More(StatType.DamageScale, BuffDamage));

    public IceBlitz(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Rime.Auras.Apply(Buff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }

    /// <summary>
    /// "The duration of Ice Blitz is extended by 0.1 seconds each time a Frost Swallow hits an enemy" (Flight of the Navir's too).
    /// GA_Rime_Passive_Talent_SpecifiedAbilityHitsReduceCooldown_HitMonitor extends only while
    /// <c>Buff.Duration - remaining - AddedDuration &gt;= 0</c>, so the time left is at most 20s
    /// (<c>Rime.AutoProjectileOnResourceGainBuff.Buff.Duration</c>). The time left excludes the expiry lag, since the
    /// game's remaining duration has none.
    /// </summary>
    public override void Connect()
    {
        var swallows = new FrostSwallows?[] { Rime.GetAbility<FrostSwallows>(), Rime.GetAbility<FlightOfTheNavir>()?.Swallows };
        foreach (var s in swallows.OfType<FrostSwallows>())
        {
            s.Hit += dmg => Extend(dmg.Timestamp);
        }
    }

    private void Extend(SimTime now)
    {
        var buff = Rime.Auras.Get(Buff, now);
        if (buff == null) return;
        double remaining = (buff.ExpiresAt - now).TotalSeconds - AuraContainer.ExpiryLagSeconds;
        if (BuffSeconds - remaining - ExtensionPerSwallowSeconds >= 0)
        {
            Rime.Auras.Extend(Buff, SimTime.FromSeconds(ExtensionPerSwallowSeconds));
        }
    }
}
