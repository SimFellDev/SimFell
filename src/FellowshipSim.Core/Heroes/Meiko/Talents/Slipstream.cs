namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 99: "Each time you cast Gust you release a burst of winds at your starting location, causing all enemies within
/// 750 radius to have 50% reduced movement speed for 6 seconds."
/// </summary>
public partial class Slipstream : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
