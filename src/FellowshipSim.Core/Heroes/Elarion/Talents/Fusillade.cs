using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Core.Heroes.Elarion.Talents;

/// <summary>
/// Talent 128: "Your Heartseeker Barrage channel time is increased by 0.5 seconds and its critical strike chance is
/// increased by +20%."
/// </summary>
public class Fusillade : Talent
{
    public const int TalentId = 128;
    public const string TalentName = "Fusillade";
    public const double ExtraChannelSeconds = 0.5;
    public const double CritChance = 0.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<HeartseekerBarrage>() is not { } barrage) return;
        barrage.ExtendChannel(ExtraChannelSeconds);
        barrage.ArrowCritChance += CritChance;
    }
}
