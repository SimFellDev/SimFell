namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 576: "While at 40% health or below, you take 20% reduced damage." Effect 2653 (IncomingDamageMultiplier x0.8).
/// No damage.
/// </summary>
public class ShiftingSpectrum : Talent
{
    public const int TalentId = 576;
    public const string TalentName = "Shifting Spectrum";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) { }
}
