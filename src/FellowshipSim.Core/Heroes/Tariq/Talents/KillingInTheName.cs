using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 82: "Your Culling Strike has increased Critical Strike chance." UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class KillingInTheName : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<CullingStrike>() is { } cullingStrike) cullingStrike.CritChanceBonus += AddedCritChance;
    }
}
