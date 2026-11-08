using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 14: "While Siegebreaker is active you deal 25% more damage and 50% your damage reduction from Toughness is shared
/// with all allies. Your Spirit is increased by +5% and each time you use your Shield Throw abililty you generate 2 Spirit
/// Points." Game data: Warmaster.BouncyProjectile.Talent.SpiritPointGain.NumPoints 2; the damage is effect 1239's
/// DamageScale x1.15 (codex), not the tooltip's 25%. Combat logs (17 fights): hits under Siegebreaker deal 0.92-0.94 of
/// what x1.25 gives.
/// </summary>
public partial class MartialCommand : Talent
{
    public const double SiegebreakerDamage = 1.15;

    public static readonly AuraDef Passive = AuraDef.Internal(TalentName, 0, modifiers: StatModifier.Add(StatType.Spirit, AddedSpiritPassive));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.PassiveAuras.Add(Passive);
        hero.AbilityDamageMultipliers.Add((_, now) => hero.Auras.HasAura(Siegebreaker.Buff, now) ? SiegebreakerDamage : 1.0);
        if (hero.GetAbility<ShieldThrow>() is { } shieldThrow)
        {
            shieldThrow.Thrown += ctx => hero.GainResource(Hero.SpiritPoints, SpiritPointGainNumPoints, ctx);
        }
    }
}
