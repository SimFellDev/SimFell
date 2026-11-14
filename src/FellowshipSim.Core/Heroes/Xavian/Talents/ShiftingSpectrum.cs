namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 576: "While at 40% health or below, you take 20% reduced damage." Effect 2653 (IncomingDamageMultiplier x0.8).
/// </summary>
public partial class ShiftingSpectrum : Talent
{

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
