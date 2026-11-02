namespace FellowshipSim.Core.APL.Debugging;

/// <summary>An ability's charges and the seconds until its next charge (0 when every charge is ready).</summary>
public sealed record AplCooldownValue(string Ability, int Charges, int MaxCharges, double Remains);
