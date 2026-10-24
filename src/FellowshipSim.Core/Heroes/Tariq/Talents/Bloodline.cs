namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 76: "You generate 1 Fury every 1 second and your movement speed is increased by 20%. You no longer lose Fury
/// while out of combat." Effect 2424 (every 1s: 1% of maximum Fury). Combat logs (fXyzwGcQkTrLD34R fight 4): the seconds
/// are hasted, and the Fury comes in combat alone (1p7AXmHfwV9CYMxR fight 30: none between pulls, and none lost).
/// </summary>
public class Bloodline : Talent
{
    public const int TalentId = 76;
    public const string TalentName = "Bloodline";
    public const double FuryPerSecond = 1.0;
    public const double PeriodSeconds = 1.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is Tariq tariq) tariq.FuryPerSecond += FuryPerSecond;
    }
}
