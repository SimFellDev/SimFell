namespace FellowshipSim.Core.Dungeons;

/// <summary>What a segment of a dungeon fight is: a trash pull, one boss, or a council of bosses at once.</summary>
public enum DungeonSegmentKind
{
    Trash,
    Boss,
    Council,
}
