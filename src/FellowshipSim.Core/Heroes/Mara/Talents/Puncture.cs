using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>Talent 123: "Your Widow's Bite has +100% increased critical strike chance." UNVERIFIED: no combat log has this talent.</summary>
public class Puncture : Talent
{
    public const int TalentId = 123;
    public const string TalentName = "Puncture";
    public const double CritChance = 1.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var widowsBite = hero.GetAbility<WidowsBite>();
        if (widowsBite != null) widowsBite.CritChanceBonus += CritChance;
    }
}
