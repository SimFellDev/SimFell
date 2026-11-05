namespace FellowshipSim.Engine.Replay;

/// <summary>A damage ID's matched hits divided by one condition, with the mean log/engine ratio on each side.</summary>
/// <param name="Condition">The condition, such as an aura on the hero, an aura on the target, an enemy or a partial tick.</param>
public sealed record DamageSplit(string Condition, int HitsWith, double RatioWith, int HitsWithout, double RatioWithout);
