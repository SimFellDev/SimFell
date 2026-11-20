using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 201: Heartseeker Barrage arrows ricochet to an additional nearby enemy. Ricochets have a share of the power of
/// the initial hit. The ricochets are logged under Heartseeker Barrage's ID.
/// </summary>
public partial class PiercingSeekers : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeartseekerBarrage>() is { } barrage) barrage.RicochetMultiplier = Constants.RiccochetDamageScale;
    }
}
