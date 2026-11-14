namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 76: "You generate Fury over time and your movement speed is increased. You no longer lose Fury
/// while out of combat." Effect 2424 (every <see cref="Constants.PassiveResourceGenerationPeriod"/>: a share of maximum Fury). The seconds are not
/// hasted (confirmed in game, 2026-11-01; in game the Fury comes in smaller fractional ticks adding up to the same). The
/// Fury comes in combat alone (combat logs, 1p7AXmHfwV9CYMxR fight 30: none between pulls, and none lost). Combat logs
/// show whole Fury steps on a hasted period while Tariq casts nothing (NhBnFXmy3bJg9Cav fight 6, 301.0-320.6), which the
/// in-game timing overrules: the logs' Fury is unreliable.
/// </summary>
public partial class Bloodline : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is Tariq tariq) tariq.FuryPerSecond += Constants.PassiveResourceGenerationFuryPerTick * Tariq.Fury.Max;
    }
}
