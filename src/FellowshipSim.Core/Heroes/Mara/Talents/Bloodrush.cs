namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>Talent 163: "Your Hemorrhaging Strike bleed ticks faster." UNVERIFIED: no combat log has this talent.</summary>
public partial class Bloodrush : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Mara)hero).Hemorrhage.TickRateBonuses.Add(_ => Constants.IncreasedTickRateWhileSingleTickRateIncrease - 1.0);
}
