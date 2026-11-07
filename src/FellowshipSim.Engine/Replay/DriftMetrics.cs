namespace FellowshipSim.Engine.Replay;

/// <summary>A resource's or aura's numbers in a <see cref="Finding"/>, in log minus engine.</summary>
/// <param name="Checks">Steps with a check of the entity.</param>
/// <param name="Cancelling">Issues cancelling on adjacent steps.</param>
/// <param name="Abilities">The abilities of the steps before the issues, largest net first.</param>
public sealed record DriftMetrics(int Checks, double Net, double Gross, int Cancelling, IReadOnlyList<DriftAbility> Abilities);
