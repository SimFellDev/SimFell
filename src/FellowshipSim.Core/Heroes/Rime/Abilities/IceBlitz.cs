using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Core.Heroes.Rime.Abilities;

/// <summary>Ability 1032 / effect 1397: increases damage, extended per Frost Swallow hit. Usable while casting.</summary>
public partial class IceBlitz : RimeAbility
{
    public const int BuffEffectId = 1397;
    public const double BuffDamage = 1.2;
    public const double ExtensionPerSwallowSeconds = 0.1;

    public static readonly AuraDef Buff = AuraDef.Effect("Ice Blitz", BuffEffectId, Constants.BuffDuration,
        modifiers: StatModifier.More(StatType.DamageScale, BuffDamage));

    public IceBlitz(Rime owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        SetCooldown(AbilityCooldown);
        IgnoresGcd = true;
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        Rime.Auras.Apply(Buff, ctx.CurrentTime);
        TriggerCooldown(ctx);
    }

    /// <summary>Tooltip: Ice Blitz's duration is extended each time a Frost Swallow hits an enemy (Flight of the Navir's too).</summary>
    public override void Connect()
    {
        var swallows = new FrostSwallows?[] { Rime.GetAbility<FrostSwallows>(), Rime.GetAbility<FlightOfTheNavir>()?.Swallows };
        foreach (var s in swallows.OfType<FrostSwallows>())
        {
            s.Hit += (in _) => Rime.Auras.Extend(Buff, SimTime.FromSeconds(ExtensionPerSwallowSeconds));
        }
    }
}
