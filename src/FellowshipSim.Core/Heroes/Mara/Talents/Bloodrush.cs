namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>Talent 163: "Your Hemorrhaging Strike bleed ticks faster." UNVERIFIED: no combat log has this talent.</summary>
public class Bloodrush : Talent
{
    public const int TalentId = 163;
    public const string TalentName = "Bloodrush";
    public const double TickRateBonus = 0.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Mara)hero).Hemorrhage.TickRateBonuses.Add(_ => TickRateBonus);
}
