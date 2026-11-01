namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 569: "Enemies standing in Shining Halo have 20% reduced Attack Speed and 35% reduced Movement Speed." Effect 2657.
/// No damage.
/// </summary>
public class ImposingPresence : Talent
{
    public const int TalentId = 569;
    public const string TalentName = "Imposing Presence";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
