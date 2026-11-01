using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 14: "While Siegebreaker is active you deal 25% more damage and 50% your damage reduction from Toughness is shared
/// with all allies. Your Spirit is increased by +5% and each time you use your Shield Throw abililty you generate 2 Spirit
/// Points." Game data: Warmaster.BouncyProjectile.Talent.SpiritPointGain.NumPoints 2.
/// </summary>
public class MartialCommand : Talent
{
    public const int TalentId = 14;
    public const string TalentName = "Martial Command";
    public const double SiegebreakerDamage = 1.25;
    public const double Spirit = 0.05;
    public const double SpiritPointsPerShieldThrow = 2.0;

    public static readonly AuraDef Passive = AuraDef.Internal(TalentName, 0, modifiers: StatModifier.Add(StatType.Spirit, Spirit));

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.PassiveAuras.Add(Passive);
        hero.AbilityDamageMultipliers.Add((_, now) => hero.Auras.HasAura(Siegebreaker.Buff, now) ? SiegebreakerDamage : 1.0);
        if (hero.GetAbility<ShieldThrow>() is { } shieldThrow)
        {
            shieldThrow.Thrown += ctx => hero.GainResource(Hero.SpiritPoints, SpiritPointsPerShieldThrow, ctx);
        }
    }
}
