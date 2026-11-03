namespace FellowshipSim.Core.Import;

/// <summary>An item worn in a gear slot: its game ID (a <see cref="GearItem"/> of the build's hero) and its tempers.</summary>
public sealed record GearPiece(GearSlot Slot, int ItemId, int Tempers);
