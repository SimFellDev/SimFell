using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 214: "Your Shield Throw has +20% additional critical strike chance and deals 100% increased damage to the first
/// target it hits."
/// </summary>
public class SkullCracker : Talent
{
    public const int TalentId = 214;
    public const string TalentName = "Skull Cracker";
    public const double CritChance = 0.2;
    public const double FirstTargetDamage = 2.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ShieldThrow>() is not { } shieldThrow) return;
        shieldThrow.CritChanceBonus += CritChance;
        shieldThrow.FirstHitMultipliers.Add(_ => FirstTargetDamage);
    }
}
