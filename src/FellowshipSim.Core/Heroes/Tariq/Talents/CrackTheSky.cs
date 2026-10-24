namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 77: "Your Lightning damage has +20% increased critical strike chance." (Placeholder constant
/// Tariq.Talent.PassiveLightningCritChanceIncrease 0.2 on every lightning hit.)
/// </summary>
public class CrackTheSky : Talent
{
    public const int TalentId = 77;
    public const string TalentName = "Crack the Sky";
    public const double CritChance = 0.20;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is Tariq tariq) tariq.LightningCritChance += CritChance;
    }
}
