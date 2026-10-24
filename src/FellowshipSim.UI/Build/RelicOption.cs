namespace FellowshipSim.UI.Build;

/// <summary>A relic item a build can wear in a relic slot, with the name of the passive it grants.</summary>
public sealed record RelicOption(int ItemId, string Name, string PassiveName);
