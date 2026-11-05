using FellowshipSim.Core.Domain;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// A dungeon's packs from its Codex placements: a patrol's placements are one pack, and every other placement is in one
/// pack with each placement within <see cref="Spacing"/> units of it.
/// </summary>
public static class DungeonPacks
{
    /// <summary>
    /// The most distance between two placements of one pack. Source: the Codex placements of Cithrel's Fall against the
    /// pulls of FellowshipLogs report T2qLPpmMkrAtJ9B1, fight 18.
    /// </summary>
    public const double Spacing = 1200;

    /// <summary>Each placement's pack, numbered from 0 in the order of the placements.</summary>
    public static int[] Of(IReadOnlyList<CodexPlacement> placements)
    {
        var pack = new int[placements.Count];
        Array.Fill(pack, -1);
        int packs = 0;
        var patrols = new Dictionary<int, int>();
        for (int i = 0; i < placements.Count; i++)
        {
            if (pack[i] >= 0) continue;
            if (placements[i].Patrol is { } patrol)
            {
                if (!patrols.TryGetValue(patrol, out int p)) patrols[patrol] = p = packs++;
                pack[i] = p;
                continue;
            }
            pack[i] = packs;
            var open = new Stack<int>([i]);
            while (open.Count > 0)
            {
                var at = placements[open.Pop()];
                for (int j = 0; j < placements.Count; j++)
                {
                    if (pack[j] >= 0 || placements[j].Patrol != null) continue;
                    if (Distance(placements[j].X, placements[j].Y, at.X, at.Y) > Spacing) continue;
                    pack[j] = packs;
                    open.Push(j);
                }
            }
            packs++;
        }
        return pack;
    }

    /// <summary>The distance between two points of the map.</summary>
    public static double Distance(double x1, double y1, double x2, double y2) => Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
}
