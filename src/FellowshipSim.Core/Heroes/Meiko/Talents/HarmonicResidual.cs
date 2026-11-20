namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 106: "While your Twin Souls: Bulwark is on cooldown your Stone Shield is reinforced, causing it to diminish by
/// 85% of the normal value when soaking damage."
/// </summary>
public partial class HarmonicResidual : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
