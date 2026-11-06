using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 82: "Your Culling Strike has increased Critical Strike chance." UNVERIFIED: no combat log has this talent.
/// </summary>
public class KillingInTheName : Talent
{
    public const int TalentId = 82;
    public const string TalentName = "Killing in the Name";
    public const double CritChance = 0.50;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<CullingStrike>() is { } cullingStrike) cullingStrike.CritChanceBonus += CritChance;
    }
}
