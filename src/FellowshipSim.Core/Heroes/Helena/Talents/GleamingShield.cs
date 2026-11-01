using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 641: "Each time you use Shield Throw, Shield Slam, Shockwave, and Shields Up, you gain 3.0% increased Armor for 3
/// seconds, stacking up to 10 times. Upon expiration, 1 stack is lost. Your Shield Slam deals additional damage to all
/// targets hit equal to 10% of your total Armor value." The buff is effect 3096; game data
/// Helena.Talent.IncreasedArmorFromAbilities.MeleeCleave.AdditionalDamageBasedOnArmor 0.1. Shield Slam's add is 10% of the
/// build's Armor with 3% more per stack.
/// </summary>
public class GleamingShield : Talent
{
    public const int TalentId = 641;
    public const string TalentName = "Gleaming Shield";
    public const int BuffEffectId = 3096;
    public const double BuffSeconds = 3.0;
    public const int MaxStacks = 10;
    public const double ArmorPerStack = 0.03;
    public const double ShieldSlamArmorShare = 0.1;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds, MaxStacks) with { ExpiryRemovesOneStack = true };

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Helena helena) return;
        helena.ArmorBonuses.Add(now => ArmorPerStack * hero.Auras.GetStacks(Buff, now));
        if (hero.GetAbility<ShieldSlam>() is { } shieldSlam) shieldSlam.ArmorDamageShare += ShieldSlamArmorShare;
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is ShieldThrow or ShieldSlam or Shockwave or ShieldsUp) hero.Auras.Apply(Buff, now);
        };
    }
}
