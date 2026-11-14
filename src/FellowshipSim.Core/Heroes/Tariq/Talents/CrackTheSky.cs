namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 77: "Your Lightning damage has increased critical strike chance." (Tooltip value
/// on every lightning hit.) Combat logs: Thunder Call: Heavy Strike
/// does not gain it, a developer bug the engine models until the game fixes it (HeavyStrike).
/// </summary>
public partial class CrackTheSky : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is Tariq tariq) tariq.LightningCritChance += Constants.TalentPassiveLightningCritChanceIncrease;
    }
}
