using System.Globalization;
using System.Text.Json;

using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons;

using static FellowshipSim.Engine.Replay.RawFightValue;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// Reads the dungeon a logged run was from its fight with one player's damage events
/// (<c>FellowshipLogsReports.GetDungeonFightAsync</c>):
/// <list type="bullet">
/// <item>The enemies are the fight's enemy NPCs that player hit or was hit by (<c>enemy_contacts</c>), each actor instance
/// once, and the enemy NPCs dying without contact (<c>enemy_deaths</c>). An enemy is in combat from its first contact to its
/// death, or to its last contact when it has no death.</item>
/// <item>A pack is a group of the dungeon's creature placements in game data (<see cref="DungeonPacks"/>). The
/// log's x and y are the placement's y and x. In the order of first contact, an enemy takes the nearest free placement of
/// its creature within <see cref="PlacementRange"/> units of its first contact, or else a free placement of its creature in
/// a pack another enemy took in the <see cref="PackOpenSeconds"/> before. An enemy with neither is a spawn: the spawns of
/// one creature met within <see cref="SpawnSeconds"/> of each other are one group. An enemy dying without contact is in the
/// pack or spawn group in combat nearest its death.</item>
/// <item>Each pack and spawn group is a pull, numbered by its start: the first contact of its enemies. Its duration runs
/// from there to its enemies' last death. A pull starting while the pulls before it are in combat is taken in by the pull
/// before it with <c>pull_with</c> at the offset of its start; any other pull starts its downtime after those pulls end.</item>
/// <item>A group is enemies of one creature in a pull, with the creature's name, base health, Spirit Points and rank
/// (<see cref="GameCreatures.ById"/>). A boss is a creature of rank Boss, and the last pull with a boss is final. An enemy
/// whose gameID has no creature is left out and named in the dungeon's comment.</item>
/// <item>Each enemy has its own time in combat, in seconds after its pull's start: <c>first_seen</c> at its first contact
/// and <c>dies</c> at its death. An enemy dying without contact is in its pull from the pull's start. A pull's enemies of
/// one creature within <see cref="LineSeconds"/> of each other in both are one group with the mean of each, and each boss
/// is a group of its own. A group's value is in the text when it differs from its default, 0 for <c>first_seen</c> and the
/// pull's duration for <c>dies</c>, at the text's 0.1 s.</item>
/// <item>The difficulty is the Eternal level of the fight's <c>difficultyLevel</c>, the game's difficulty number
/// (<see cref="DifficultyScaling.WithDifficulty"/>), and the name is its <c>gameZone</c>'s.</item>
/// <item>The time limit is <see cref="TimeLimitShare"/> of the run's length, and at least
/// <see cref="DungeonProfile.DefaultTimeLimitSeconds"/>.</item>
/// </list>
/// </summary>
public static class DungeonReader
{
    /// <summary>An imported dungeon's time limit over the run's length, rounded up to a whole minute.</summary>
    public const double TimeLimitShare = 1.5;

    /// <summary>
    /// The most distance from an enemy's first contact to the placement it takes. Source: FellowshipLogs report
    /// T2qLPpmMkrAtJ9B1, fight 18: a tank's first hit on a pack is next to its placements, and the enemies dragged
    /// before that tank's first hit are far from them.
    /// </summary>
    public const double PlacementRange = 4000;

    /// <summary>The seconds after a pack's first contact in which an enemy without a placement in range joins that pack.</summary>
    public const double PackOpenSeconds = 30;

    /// <summary>The most seconds between the first contacts of two spawns of one group.</summary>
    public const double SpawnSeconds = 3;

    /// <summary>
    /// The most seconds between the first contacts, and between the deaths, of two enemies of one line. Source: FellowshipLogs
    /// report Ab1yCxVcNBHPz6t8, fight 7, a run of 26 minutes: its 125 enemies are 92 lines at 2 seconds, 121 lines when a
    /// line's enemies have the same times, and 69 lines when a line is a pull's enemies of one creature. At 2 seconds its
    /// 52 pulls' last deaths are 8 seconds early in all, since a line has the mean of its enemies' deaths.
    /// </summary>
    public const double LineSeconds = 2;

    /// <summary>The dungeon of a fight with one player's damage events, as <c>FellowshipLogsReports.GetDungeonFightAsync</c> returns it.</summary>
    public static DungeonProfile FromFight(JsonElement dungeonFight)
    {
        var raw = new RawFightValue(dungeonFight);
        var fight = raw["fight"];
        if (fight.Element.ValueKind != JsonValueKind.Object) throw new ArgumentException("The fight is missing.");
        double start = Num(fight["startTime"]), end = Num(fight["endTime"]);
        int gameDifficulty = Int(fight["difficultyLevel"]) ?? throw new ArgumentException("The fight has no difficulty level.");
        int difficulty = DifficultyScaling.WithDifficulty(gameDifficulty)?.Eternal
            ?? throw new ArgumentException($"The run's difficulty level {gameDifficulty} is no Eternal level.");
        double Seconds(RawFightValue time) => (Num(time) - start) / 1000;

        // The fight's enemies by actor: the deaths table also has NPCs of the party's, such as a summoned Sibling of Earth.
        var creatureOf = fight["enemyNPCs"].Items.ToDictionary(n => (int)Num(n["id"]), n => Int(n["gameID"]) ?? 0);
        var leftOut = new SortedSet<string>(StringComparer.Ordinal);
        var deaths = new Dictionary<(int, int), double>();
        var names = new Dictionary<(int, int), string>();
        foreach (var death in raw["enemy_deaths"].Items.OrderBy(d => Num(d["timestamp"])))
        {
            var key = ((int)Num(death["id"]), Int(death["instance"]) ?? 1);
            if (!creatureOf.ContainsKey(key.Item1) || !deaths.TryAdd(key, Seconds(death["timestamp"]))) continue;
            // The table names an enemy with its instance number: "Frozen Remnant 2".
            names[key] = Str(death["name"]).TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        }
        CodexCreature? Creature((int Id, int Instance) key)
        {
            if (GameCreatures.ById(creatureOf[key.Id]) is { } creature) return creature;
            leftOut.Add(names.TryGetValue(key, out var name) && name.Length > 0 ? name : $"actor {key.Id}");
            return null;
        }

        var placements = GameDungeon.ById(Int(fight["gameZone"]["id"]) ?? 0)?.Placements ?? [];
        var packOf = DungeonPacks.Of(placements);
        var taken = new bool[placements.Count];
        var packOpened = new Dictionary<int, double>();
        var groups = new Dictionary<string, Group>();
        var lastSpawn = new Dictionary<int, (double At, string Group)>();
        var met = new HashSet<(int, int)>();
        foreach (var contact in raw["enemy_contacts"].Items.OrderBy(c => Num(c["first"])))
        {
            var key = ((int)Num(contact["id"]), Int(contact["instance"]) ?? 1);
            if (!creatureOf.ContainsKey(key.Item1) || !met.Add(key) || Creature(key) is not { } creature) continue;
            double first = Seconds(contact["first"]);
            double last = deaths.TryGetValue(key, out double died) ? died : Seconds(contact["last"]);
            // The log's x and y are the placement's y and x.
            double x = Num(contact["y"]), y = Num(contact["x"]);
            int nearest = -1, open = -1;
            double nearestDistance = PlacementRange, openDistance = double.MaxValue;
            for (int i = 0; i < placements.Count; i++)
            {
                if (taken[i] || placements[i].CreatureId != creature.Id) continue;
                double distance = DungeonPacks.Distance(placements[i].X, placements[i].Y, x, y);
                if (distance <= nearestDistance) (nearest, nearestDistance) = (i, distance);
                if (packOpened.TryGetValue(packOf[i], out double opened) && first - opened <= PackOpenSeconds && distance < openDistance)
                {
                    (open, openDistance) = (i, distance);
                }
            }
            string group;
            if ((nearest >= 0 ? nearest : open) is var placement and >= 0)
            {
                taken[placement] = true;
                packOpened.TryAdd(packOf[placement], first);
                group = $"pack {packOf[placement]}";
            }
            else
            {
                group = lastSpawn.TryGetValue(creature.Id, out var spawn) && first - spawn.At <= SpawnSeconds ? spawn.Group : $"spawn {key}";
                lastSpawn[creature.Id] = (first, group);
            }
            if (!groups.TryGetValue(group, out var g)) groups[group] = g = new Group(first);
            g.Add(creature, first, last);
        }
        if (groups.Count == 0) throw new ArgumentException("The fight has no enemy the player met.");
        // An enemy dying without contact is in the group in combat nearest its death, from the group's start.
        foreach (var (key, died) in deaths)
        {
            if (met.Contains(key) || Creature(key) is not { } creature) continue;
            groups.Values.MinBy(g => g.Distance(died))!.Add(creature, null, died);
        }

        var pulls = groups.Values.OrderBy(g => g.Start).ToList();
        int lastBossPull = pulls.FindLastIndex(p => p.Enemies.Any(e => IsBoss(e.Creature)));
        var profilePulls = new List<DungeonPull>();
        double inCombatUntil = double.NegativeInfinity;
        for (int i = 0; i < pulls.Count; i++)
        {
            var pull = pulls[i];
            string name = (i + 1).ToString(CultureInfo.InvariantCulture);
            double duration = Math.Max(0.1, Math.Round(pull.End - pull.Start, 1));
            var creatures = pull.Enemies.GroupBy(e => e.Creature.Id).Select(g => (g.First().Creature, Enemies: g.ToList())).ToList();
            var enemyGroups = creatures
                .SelectMany(c => Lines(c.Creature, c.Enemies.Select(e => ((e.First ?? pull.Start) - pull.Start, e.Last - pull.Start)), duration))
                .OrderBy(g => g.Boss)
                .ToList();
            double downtime = 0;
            if (i > 0 && pull.Start < inCombatUntil)
            {
                // A pull starting in combat: the pull before takes it in at its start.
                double offset = Math.Round((pull.Start - pulls[i - 1].Start) * 1000);
                profilePulls[^1] = profilePulls[^1] with { Join = new PullJoin(name, OffsetMs: offset) };
            }
            else if (i > 0) downtime = Math.Round(pull.Start - inCombatUntil, 1);
            inCombatUntil = Math.Max(inCombatUntil, pull.End);
            var bosses = creatures.Where(c => IsBoss(c.Creature)).Select(c => c.Creature.Name).Distinct().ToList();
            string label = bosses.Count > 0 ? string.Join(" and ", bosses) : string.Join(", ", creatures.Select(c => $"{c.Enemies.Count} {c.Creature.Name}"));
            profilePulls.Add(new DungeonPull(name, enemyGroups, duration, downtime,
                Final: i == lastBossPull, Comment: $"{label}, {Clock(pull.Start)} in the log"));
        }

        double seconds = (end - start) / 1000;
        // Room for the length variance: a fight of longer pulls still ends before its time limit.
        double timeLimit = Math.Max(DungeonProfile.DefaultTimeLimitSeconds, Math.Ceiling(seconds * TimeLimitShare / 60) * 60);
        string zone = Str(fight["gameZone"]["name"]);
        string comment = $"From FellowshipLogs report {Str(raw["report_code"])}, fight {Num(fight["id"])} ({Clock(seconds)}), " +
                         $"with {Str(raw["contact_player"]["name"])}'s damage events.";
        if (leftOut.Count > 0) comment += $"\nEnemies without game data: {string.Join(", ", leftOut)}.";
        return new DungeonProfile(zone.Length > 0 ? zone : Str(fight["name"]), timeLimit, profilePulls, difficulty, comment);
    }

    /// <summary>
    /// The lines of one creature's enemies in a pull <paramref name="duration"/> seconds long, from each enemy's seconds
    /// after the pull's start at its first contact and its death: a line for each boss, and a line for the other enemies
    /// within <see cref="LineSeconds"/> of each other in both, with the mean of each. A value is in the line when it
    /// differs from its default at the text's 0.1 s.
    /// </summary>
    private static IEnumerable<EnemyGroup> Lines(CodexCreature creature, IEnumerable<(double FirstSeen, double Dies)> enemies, double duration)
    {
        bool boss = IsBoss(creature);
        var lines = new List<List<(double FirstSeen, double Dies)>>();
        foreach (var enemy in enemies.OrderBy(e => e.FirstSeen).ThenBy(e => e.Dies))
        {
            var line = boss ? null : lines.FirstOrDefault(l => l.All(other =>
                Math.Abs(other.FirstSeen - enemy.FirstSeen) <= LineSeconds && Math.Abs(other.Dies - enemy.Dies) <= LineSeconds));
            if (line == null) lines.Add(line = []);
            line.Add(enemy);
        }
        foreach (var line in lines)
        {
            double firstSeen = Math.Round(line.Average(e => e.FirstSeen), 1), dies = Math.Min(duration, Math.Round(line.Average(e => e.Dies), 1));
            if (dies <= firstSeen)
            {
                // An enemy with one contact and no death, or dying as its pull starts: in combat for the text's 0.1 s.
                dies = Math.Max(dies, 0.1);
                firstSeen = Math.Min(firstSeen, Math.Round(dies - 0.1, 1));
            }
            yield return new EnemyGroup(line.Count, creature.Name, creature.SpiritPoints, HitPoints: Math.Round(creature.BaseHealth, 2), Boss: boss,
                FirstSeenSeconds: firstSeen, DiesSeconds: dies == duration ? null : dies);
        }
    }

    private static bool IsBoss(CodexCreature creature) => creature.Rank == CreatureRank.Boss;

    private static string Clock(double seconds) => $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}";

    /// <summary>
    /// A pack or spawn group in the log: its enemies, each with its creature, its first contact (null for an enemy dying
    /// without contact) and its death, and the group's first contact and last death, in seconds.
    /// </summary>
    private sealed class Group(double start)
    {
        public readonly List<(CodexCreature Creature, double? First, double Last)> Enemies = [];
        public double Start = start, End = start;

        public void Add(CodexCreature creature, double? first, double last)
        {
            Enemies.Add((creature, first, last));
            Start = Math.Min(Start, first ?? last);
            End = Math.Max(End, last);
        }

        /// <summary>The seconds from the group's time in combat to time t; 0 within it.</summary>
        public double Distance(double t) => t < Start ? Start - t : t > End ? t - End : 0;
    }
}
