using System.Globalization;
using System.Text.Json;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Enemies;
using static FellowshipSim.Engine.Replay.RawFightValue;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Reads the dungeon a logged run was from its fight without events (<c>FellowshipLogsReports.GetDungeonFightAsync</c>):
/// <list type="bullet">
/// <item>The enemies are the deaths of the fight's enemy NPCs (<c>enemy_deaths</c> of <c>fight.enemyNPCs</c>), each actor
/// instance once.</item>
/// <item>The pulls are FellowshipLogs' pulls of the fight (<c>fight.dungeonPulls</c>), numbered by their start. An enemy is
/// in the pull it died in, or the pull nearest its death, and a pull without one is left out.</item>
/// <item>A pull's duration is its end minus its start in the log. Its downtime is the time from the pull before it ending to
/// its start; a pull starting before that end joins it with <c>pull_with</c> at the offset of its start.</item>
/// <item>A group is the enemies of one creature in a pull, the creature being the death's gameID in the game data
/// (<see cref="GameCreatures.ById"/>): its name, base health, Spirit Points and rank. A boss is a creature of rank Boss,
/// and the last pull with a boss is final. An enemy whose gameID has no creature is left out and named in the dungeon's
/// comment.</item>
/// <item>The difficulty is the fight's <c>difficulty</c>, and the name its <c>gameZone</c>'s.</item>
/// <item>The time limit is <see cref="TimeLimitShare"/> of the run's length, and at least
/// <see cref="DungeonProfile.DefaultTimeLimitSeconds"/>.</item>
/// </list>
/// </summary>
public static class DungeonReader
{
    /// <summary>An imported dungeon's time limit over the run's length, rounded up to a whole minute.</summary>
    public const double TimeLimitShare = 1.5;

    /// <summary>The dungeon of a fight without events, as <c>FellowshipLogsReports.GetDungeonFightAsync</c> returns it.</summary>
    public static DungeonProfile FromFight(JsonElement dungeonFight)
    {
        var raw = new RawFightValue(dungeonFight);
        var fight = raw["fight"];
        if (fight.Element.ValueKind != JsonValueKind.Object) throw new ArgumentException("The fight is missing.");
        double start = Num(fight["startTime"]), end = Num(fight["endTime"]);
        int difficulty = Int(fight["difficulty"]) ?? throw new ArgumentException("The fight has no difficulty.");
        if (DifficultyScaling.At(difficulty) == null) throw new ArgumentException($"The run's difficulty {difficulty} is no Eternal level.");

        var logPulls = fight["dungeonPulls"].Items
            .Select(p => new LogPull(Str(p["name"]), (Num(p["startTime"]) - start) / 1000, (Num(p["endTime"]) - start) / 1000,
                p["enemyNPCs"].Items
                    .Select(n => ((int)Num(n["id"]), (int)Num(n["minimumInstanceID"]), (int)Num(n["maximumInstanceID"]))).ToList()))
            .OrderBy(p => p.Start)
            .ToList();
        if (logPulls.Count == 0) throw new ArgumentException("The fight has no pulls.");
        // The fight's enemies: the deaths table also has NPCs of the party's, such as a summoned Sibling of Earth.
        var enemyIds = fight["enemyNPCs"].Items.Select(n => (int)Num(n["id"])).ToHashSet();

        // Each enemy instance's first death, and its creature.
        var leftOut = new SortedSet<string>(StringComparer.Ordinal);
        var enemies = new List<(GameCreature Creature, LogPull Pull)>();
        var seen = new HashSet<(int, int)>();
        foreach (var death in raw["enemy_deaths"].Items.OrderBy(d => Num(d["timestamp"])))
        {
            int id = (int)Num(death["id"]), instance = Int(death["instance"]) ?? 1;
            if (!enemyIds.Contains(id) || !seen.Add((id, instance))) continue;
            if (GameCreatures.ById(Int(death["gameID"]) ?? 0) is not { } creature)
            {
                // The table names an enemy with its instance number: "Frozen Remnant 2".
                string name = Str(death["name"]).TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                leftOut.Add(name.Length > 0 ? name : $"actor {id}");
                continue;
            }
            double t = (Num(death["timestamp"]) - start) / 1000;
            var pull = logPulls.OrderBy(p => p.Distance(t)).ThenBy(p => p.Lists(id, instance) ? 0 : 1).First();
            enemies.Add((creature, pull));
        }
        if (enemies.Count == 0) throw new ArgumentException("The fight has no enemy deaths.");

        var pullOf = enemies.ToLookup(e => e.Pull, e => e.Creature);
        var pulls = logPulls.Where(p => pullOf[p].Any()).ToList();
        int lastBossPull = pulls.FindLastIndex(p => pullOf[p].Any(IsBoss));
        var profilePulls = new List<DungeonPull>();
        for (int i = 0; i < pulls.Count; i++)
        {
            var pull = pulls[i];
            string name = (logPulls.IndexOf(pull) + 1).ToString(CultureInfo.InvariantCulture);
            var groups = pullOf[pull]
                .GroupBy(c => c.Id)
                .Select(g => new EnemyGroup(g.Count(), g.First().Name, g.First().SpiritPoints, HitPoints: Math.Round(g.First().BaseHealth, 2),
                    Boss: IsBoss(g.First())))
                // A boss line is one boss.
                .SelectMany(g => g.Boss ? Enumerable.Repeat(g with { Count = 1 }, g.Count) : [g])
                .OrderBy(g => g.Boss)
                .ToList();
            double downtime = 0;
            if (i > 0 && pull.Start < pulls[i - 1].End)
            {
                // A pull starting in combat: the pull before takes it in at its start.
                double offset = Math.Round((pull.Start - pulls[i - 1].Start) * 1000);
                profilePulls[^1] = profilePulls[^1] with { Join = new PullJoin(name, OffsetMs: offset) };
            }
            else if (i > 0) downtime = Math.Round(pull.Start - pulls[i - 1].End, 1);
            // A boss pull's log name is in the language of the log's uploader; the creature names are English.
            var bosses = pullOf[pull].Where(IsBoss).Select(c => c.Name).Distinct().ToList();
            string label = bosses.Count > 0 ? string.Join(" and ", bosses) : pull.Name;
            profilePulls.Add(new DungeonPull(name, groups, Math.Max(0.1, Math.Round(pull.End - pull.Start, 1)), downtime,
                Final: i == lastBossPull, Comment: $"{label}, {Clock(pull.Start)} in the log"));
        }

        double seconds = (end - start) / 1000;
        // Room for the length variance: a fight of pulls 20% longer still ends before its time limit.
        double timeLimit = Math.Max(DungeonProfile.DefaultTimeLimitSeconds, Math.Ceiling(seconds * TimeLimitShare / 60) * 60);
        string zone = Str(fight["gameZone"]["name"]);
        string comment = $"From FellowshipLogs report {Str(raw["report_code"])}, fight {Num(fight["id"])} ({Clock(seconds)}).";
        if (leftOut.Count > 0) comment += $"\nEnemies without game data: {string.Join(", ", leftOut)}.";
        return new DungeonProfile(zone.Length > 0 ? zone : Str(fight["name"]), timeLimit, profilePulls, difficulty, comment);
    }

    private static bool IsBoss(GameCreature creature) => creature.Rank == CreatureRank.Boss;

    // One of FellowshipLogs' pulls of the fight: its name, its start and end in seconds into the fight, and its enemy
    // instance ranges by actor.
    private sealed record LogPull(string Name, double Start, double End, List<(int Id, int Min, int Max)> Enemies)
    {
        // Seconds from the pull to time t; 0 within it.
        public double Distance(double t) => t < Start ? Start - t : t > End ? t - End : 0;

        public bool Lists(int id, int instance) => Enemies.Any(n => n.Id == id && instance >= n.Min && instance <= n.Max);
    }

    private static string Clock(double seconds) => $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}";
}
