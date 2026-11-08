using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>Talent 123: "Your Widow's Bite has increased critical strike chance." UNVERIFIED: no combat log has this talent.</summary>
public partial class Puncture : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var widowsBite = hero.GetAbility<WidowsBite>();
        if (widowsBite != null) widowsBite.CritChanceBonus += IncreasedCritChanceAdditionalCritChance;
    }
}
