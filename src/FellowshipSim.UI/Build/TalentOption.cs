using FellowshipSim.Core.Domain;

namespace FellowshipSim.UI.Build;

/// <summary>A talent in its place on the tree.</summary>
public sealed record TalentOption(TalentSlot Slot, BuildOption Talent);
