using FellowshipSim.Logs;

namespace FellowshipSim.Engine.Replay;

/// <summary>A finished replay with the script it followed.</summary>
public sealed record ReviewedFight(ReplayResult Result, ReplayScript Script, int PlayerId)
{
    public string ReviewId => ReplayLibrary.ReviewId(Script.ReportCode, Script.FightId, PlayerId);
}
