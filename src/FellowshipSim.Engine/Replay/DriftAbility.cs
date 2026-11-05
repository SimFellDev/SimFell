namespace FellowshipSim.Engine.Replay;

/// <summary>Log minus engine built up after one ability's steps.</summary>
public sealed record DriftAbility(string Ability, int Steps, double Net);
