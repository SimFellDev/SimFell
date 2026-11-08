using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 128: Heartseeker Barrage's channel time and critical strike chance are increased.
/// </summary>
public partial class Fusillade : Talent
{
    public const double CritChance = 0.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeartseekerBarrage>() is not { } barrage) return;
        barrage.ExtendChannel(IncreasedChannelDurationAmount);
        barrage.ArrowCritChance += CritChance;
    }
}
