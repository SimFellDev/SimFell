using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 581: "Your Sun Strike applies 2 stacks of Sunstruck to the target. Your Omnistrike deals 25% more damage to all
/// enemies for every stack of Sunstruck it consumes." The game data constant
/// Sune.SingleTargetFillerMelee.Talent.ExtraDebuffFromCrit.ExtraDebuffStack 1 is the second stack, on a Sun Strike crit
/// alone. UNVERIFIED: no combat log has this talent.
/// </summary>
public class GleamingStrikes : Talent
{
    public const int TalentId = 581;
    public const string TalentName = "Gleaming Strikes";
    public const int ExtraStacksOnCrit = 1;
    public const double DamagePerStack = 0.25;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        hero.GetAbility<SunStrike>()!.ExtraSunstruckOnCrit = ExtraStacksOnCrit;
        hero.GetAbility<Omnistrike>()!.DamagePerSunstruckStack = DamagePerStack;
    }
}
