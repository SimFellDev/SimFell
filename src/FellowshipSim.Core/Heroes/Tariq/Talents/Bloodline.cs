namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 76: "You generate 1 Fury every 1 second and your movement speed is increased by 20%. You no longer lose Fury
/// while out of combat." Effect 2424 (every 1s: 1% of maximum Fury). The seconds are not hasted (confirmed in game,
/// 2026-11-01: 50 Fury over 50 seconds; in game the Fury comes in smaller fractional ticks adding up to the same). The
/// Fury comes in combat alone (combat logs, 1p7AXmHfwV9CYMxR fight 30: none between pulls, and none lost). Combat logs
/// show whole 1-Fury steps about every 1 / (1 + haste) seconds while Tariq casts nothing (NhBnFXmy3bJg9Cav fight 6,
/// 301.0-320.6: 25 Fury in 19.5s at about 27% haste), which the in-game timing overrules: the logs' Fury is unreliable.
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
