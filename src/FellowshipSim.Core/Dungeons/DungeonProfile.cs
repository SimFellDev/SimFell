using System.Globalization;
using System.Text;

namespace FellowshipSim.Core.Dungeons;

/// <summary>
/// A dungeon fight: its pulls, the hero's share of the party's damage in percent, the time limit in seconds and the
/// difficulty, an Eternal level (0 for Base). In simc style, one <c>key=value</c> per line:
/// <code>
/// dungeon=Default
/// difficulty=10
/// hero_share=37.5
/// time_limit=1800
/// pull.1=/count=5,name=Imp,spirit_points=3,hit_points=7857
/// pull.1+=/count=1,name="Ogre Captain",spirit_points=10,priority=1,hit_points=11546,interrupt_ability="Big Cast",cooldown=20,cast_time=2.5
/// pull.ghouls=/pull_duration=45,downtime=20
/// pull.ghouls+=/count=4,name=Ghoul
/// pull.last_boss=/final=1,pull_with=ghouls,offset_pct=50
/// pull.last_boss+=/boss.1,name="Ancient Koros",spirit_points=45,hit_points=64029
/// </code>
/// A pull has a name of letters, digits and '_'. Pulls run in the order of their <c>pull.X=/</c> lines.
/// Lines starting with '#' are comments. A comment above a <c>pull.X+=/</c> enemy group line belongs to that group
/// (<see cref="EnemyGroup.Comment"/>), a comment above any other pull line or after the last pull to the pull
/// (<see cref="DungeonPull.Comment"/>), and any other comment to the dungeon (<see cref="Comment"/>). A pull's text starts
/// with its options line when it has options, or with an empty <c>pull.X=/</c> line when its first group has a comment.
/// <c>pull.X=/</c> starts pull X and <c>pull.X+=/</c> adds a line to it. A line is
/// an enemy group (<c>count=</c>), a boss (<c>boss.N</c>) or the pull's options; a group line can also have pull options.
/// Group values: <c>count</c>, <c>name</c>, <c>spirit_points</c>, <c>priority</c>, <c>hit_points</c>,
/// <c>interrupt_ability</c>, <c>cooldown</c> and <c>cast_time</c>. Pull options: <c>pull_duration</c>, <c>downtime</c>,
/// <c>final</c>, <c>chain_with</c> and <c>pull_with</c> with one trigger at most (<c>offset_ms</c>, <c>offset_pct</c>,
/// <c>after_deaths</c>, <c>after_deaths_total</c>). A name with a comma is quoted.
/// </summary>
public sealed record DungeonProfile(string Name, double HeroShare, double TimeLimitSeconds, IReadOnlyList<DungeonPull> Pulls,
    int Difficulty = DungeonProfile.DefaultDifficulty, string? Comment = null)
{
    /// <summary>The hero's share of the party's damage, in percent, of a dungeon whose text names none: the middle of 30-45%.</summary>
    public const double DefaultHeroShare = 37.5;

    /// <summary>The time limit of a dungeon whose text names none, in seconds.</summary>
    public const double DefaultTimeLimitSeconds = 1800;

    /// <summary>The Eternal level of a dungeon whose text names none.</summary>
    public const int DefaultDifficulty = 10;

    /// <summary>The difficulty's health and damage scale; null when <see cref="Difficulty"/> is no Eternal level.</summary>
    public DifficultyLevel? Level => DifficultyScaling.At(Difficulty);

    /// <summary>The enemies' health over their hit points, at <see cref="Difficulty"/>.</summary>
    public double HealthScale => Level?.Health ?? throw new InvalidOperationException(Problem());

    /// <summary>The enemies' damage over their game data damage, at <see cref="Difficulty"/>.</summary>
    public double DamageScale => Level?.Damage ?? throw new InvalidOperationException(Problem());

    /// <summary>The pull named <paramref name="name"/>, or null when there is none.</summary>
    public DungeonPull? Pull(string name) => Pulls.FirstOrDefault(p => p.Name == name);

    /// <summary>Host pull of pull <paramref name="name"/>: the pull with <c>pull_with</c> naming it; null when none names it.</summary>
    public DungeonPull? TakenInBy(string name) => Pulls.FirstOrDefault(p => p.Join?.Pull == name);

    /// <summary>
    /// The pulls that run on their own, in the order they run: the order of <see cref="Pulls"/>, and each pull with
    /// <c>chain_with=X</c> right after the run with pull X. A pull another pull takes in runs inside it. Null when a
    /// reference is circular.
    /// </summary>
    public IReadOnlyList<DungeonPull>? RunOrder()
    {
        var roots = Pulls.Where(p => TakenInBy(p.Name) == null).ToList();
        var order = new List<DungeonPull>();
        var emitted = new HashSet<string>();

        void Emit(DungeonPull root)
        {
            order.Add(root);
            emitted.Add(root.Name);
            var members = RunMembers(root);
            foreach (var chained in roots)
            {
                if (chained.ChainWith is { } x && !emitted.Contains(chained.Name) && members.Any(m => m.Name == x)) Emit(chained);
            }
        }

        foreach (var root in roots)
        {
            if (root.ChainWith == null && !emitted.Contains(root.Name)) Emit(root);
        }
        return order.Count == roots.Count && Pulls.All(p => RootOf(p) != null) ? order : null;
    }

    /// <summary>The pull <paramref name="root"/> and the pulls its <c>pull_with</c> takes in, in turn.</summary>
    public IReadOnlyList<DungeonPull> RunMembers(DungeonPull root)
    {
        var members = new List<DungeonPull> { root };
        for (var pull = root; pull.Join is { } join && Pull(join.Pull) is { } joined && !members.Contains(joined); pull = joined)
        {
            members.Add(joined);
        }
        return members;
    }

    // The pull that runs on its own with pull's enemies in it; null when pull_with goes round in a circle.
    private DungeonPull? RootOf(DungeonPull pull)
    {
        var seen = new HashSet<string>();
        while (TakenInBy(pull.Name) is { } host)
        {
            if (!seen.Add(pull.Name)) return null;
            pull = host;
        }
        return pull;
    }

    /// <summary>The problem with the profile, or null when it is valid.</summary>
    public string? Problem()
    {
        if (Pulls.Count == 0) return "A dungeon has at least one pull.";
        if (!(HeroShare > 0 && HeroShare <= 100)) return "The hero share is more than 0% and at most 100%.";
        if (!(TimeLimitSeconds > 0)) return "The time limit is more than 0 seconds.";
        if (Level == null) return $"The difficulty is an Eternal level from 0 (Base) to {DifficultyScaling.Levels[^1].Eternal}.";
        foreach (var pull in Pulls)
        {
            if (Pulls.Count(p => p.Name == pull.Name) > 1) return $"Pull {pull.Name}: two pulls have this name.";
            if (pull.Problem() is { } problem) return $"Pull {pull.Name}: {problem}.";
        }
        foreach (var pull in Pulls)
        {
            if (pull.Join is { } join && ReferenceProblem(pull, join.Pull, "pull_with") is { } joinProblem) return joinProblem;
            if (pull.ChainWith is { } chain && ReferenceProblem(pull, chain, "chain_with") is { } chainProblem) return chainProblem;
            int takers = Pulls.Count(p => p.Join?.Pull == pull.Name);
            if (takers > 1) return $"Pull {pull.Name}: two pulls take it in with pull_with.";
            if (takers == 1 && pull.ChainWith != null) return $"Pull {pull.Name}: a pull taken in with pull_with has no chain_with.";
            if (pull.Join is { AfterDeaths: { Percent: false } deaths } && deaths.Value > pull.EnemyCount)
                return $"Pull {pull.Name}: after_deaths is at most the pull's {pull.EnemyCount} enemies.";
        }
        if (RunOrder() == null) return "The pulls refer to each other in a circle through pull_with and chain_with.";
        return null;
    }

    private string? ReferenceProblem(DungeonPull pull, string other, string key) =>
        other == pull.Name ? $"Pull {pull.Name}: {key} names the pull itself."
        : Pull(other) == null ? $"Pull {pull.Name}: {key} names pull {other}, and there is no pull {other}."
        : null;

    public static DungeonProfile Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>The profile <paramref name="text"/> writes; a <see cref="FormatException"/> names the line at fault.</summary>
    public static DungeonProfile Parse(string text)
    {
        string? name = null;
        double heroShare = DefaultHeroShare, timeLimit = DefaultTimeLimitSeconds;
        int difficulty = DefaultDifficulty;
        var pulls = new OrderedDictionary<string, PullText>();
        var dungeonComment = new List<string>();
        var comment = new List<string>();
        PullText? lastPull = null;
        int lineNumber = 0;
        foreach (string raw in text.Split('\n'))
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('#'))
            {
                comment.Add(line[1..].TrimStart());
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) throw Error(lineNumber, line, "expected key=value");
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            try
            {
                switch (key)
                {
                    case "dungeon":
                        name = Unquote(value);
                        break;
                    case "hero_share":
                        heroShare = Number(key, value);
                        break;
                    case "time_limit":
                        timeLimit = Number(key, value);
                        break;
                    case "difficulty":
                        difficulty = Integer(key, value);
                        break;
                    default:
                        if (!key.StartsWith("pull.", StringComparison.Ordinal))
                            throw new FormatException($"'{key}' is not a dungeon option (dungeon, difficulty, hero_share, time_limit, pull.X)");
                        bool append = key.EndsWith('+');
                        string pullName = PullName(key[5..(append ? ^1 : ^0)]);
                        if (append && !pulls.ContainsKey(pullName)) throw new FormatException($"pull {pullName} has no pull.{pullName}= line before this one");
                        if (!append && pulls.ContainsKey(pullName)) throw new FormatException($"pull {pullName} starts twice");
                        if (!append) pulls[pullName] = new PullText();
                        lastPull = pulls[pullName];
                        int groups = lastPull.Groups.Count;
                        ParsePullLine(lastPull, value.StartsWith('/') ? value[1..] : value);
                        if (append && lastPull.Groups.Count > groups && comment.Count > 0)
                            lastPull.Groups[^1] = lastPull.Groups[^1] with { Comment = JoinComment(comment) };
                        else lastPull.Comment.AddRange(comment);
                        comment.Clear();
                        break;
                }
            }
            catch (FormatException e)
            {
                throw Error(lineNumber, line, e.Message);
            }
            dungeonComment.AddRange(comment);
            comment.Clear();
        }
        (lastPull?.Comment ?? dungeonComment).AddRange(comment);
        if (string.IsNullOrWhiteSpace(name)) throw new FormatException("The dungeon has no dungeon=<name> line.");
        if (pulls.Count == 0) throw new FormatException("The dungeon has no pull line.");
        var profile = new DungeonProfile(name, heroShare, timeLimit, pulls.Select(p => p.Value.Pull(p.Key)).ToList(), difficulty,
            JoinComment(dungeonComment));
        foreach (var (pullName, pull) in pulls)
        {
            if (pull.TriggerWithoutJoin) throw new FormatException($"Pull {pullName}: a trigger needs pull_with.");
        }
        if (profile.Problem() is { } problem) throw new FormatException(problem);
        return profile;
    }

    // One pull's lines as they are read.
    private sealed class PullText
    {
        public readonly List<EnemyGroup> Groups = [];
        public double? PullDuration;
        public double Downtime;
        public string? ChainWith;
        public bool Final;
        public string? PullWith;
        public double? OffsetMs, OffsetPct;
        public DeathCount? AfterDeaths, AfterDeathsTotal;
        public readonly List<string> Comment = [];

        public bool TriggerWithoutJoin => PullWith == null && (OffsetMs != null || OffsetPct != null || AfterDeaths != null || AfterDeathsTotal != null);

        public DungeonPull Pull(string name) => new(name, Groups, PullDuration, Downtime, ChainWith, Final,
            PullWith is { } with ? new PullJoin(with, OffsetMs, OffsetPct, AfterDeaths, AfterDeathsTotal) : null, JoinComment(Comment));
    }

    // A comment's lines as one text; null when there are none.
    private static string? JoinComment(List<string> lines) => lines.Count > 0 ? string.Join("\n", lines) : null;

    // A comment's '#' lines, its blank lines left out.
    private static void AppendComment(StringBuilder text, string? comment)
    {
        foreach (string line in (comment ?? "").Split('\n').Select(c => c.TrimEnd()).Where(c => c.Length > 0))
        {
            text.Append("# ").Append(line).Append('\n');
        }
    }

    private static void ParsePullLine(PullText pull, string value)
    {
        // An empty pull.N=/ line: the start of a pull whose first group has a comment.
        if (value.Trim().Length == 0) return;
        var parts = SplitOptions(value);
        bool boss = parts[0].StartsWith("boss.", StringComparison.Ordinal);
        if (boss)
        {
            if (!int.TryParse(parts[0][5..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                throw new FormatException($"'{parts[0]}' is not boss.N");
            parts.RemoveAt(0);
        }
        int? count = boss ? 1 : null;
        string? name = null;
        double spirit = 0, cooldown = 0, castTime = EnemyCast.DefaultCastSeconds;
        double? hitPoints = null;
        bool priority = false, groupValues = false;
        string? ability = null;
        foreach (string option in parts)
        {
            int eq = option.IndexOf('=');
            if (eq <= 0) throw new FormatException($"expected option=value, not '{option}'");
            string key = option[..eq].Trim();
            string v = option[(eq + 1)..].Trim();
            switch (key)
            {
                case "count" when boss: throw new FormatException("a boss line is one boss, with no count");
                case "count": count = Integer(key, v); break;
                case "name": name = Unquote(v); groupValues = true; break;
                case "spirit_points": spirit = Number(key, v); groupValues = true; break;
                case "priority": priority = Integer(key, v) != 0; groupValues = true; break;
                case "hit_points": hitPoints = Number(key, v); groupValues = true; break;
                case "interrupt_ability": ability = Unquote(v); groupValues = true; break;
                case "cooldown": cooldown = Number(key, v); groupValues = true; break;
                case "cast_time": castTime = Number(key, v); groupValues = true; break;
                case "pull_duration": pull.PullDuration = Number(key, v); break;
                case "downtime": pull.Downtime = Number(key, v); break;
                case "final": pull.Final = Integer(key, v) != 0; break;
                case "chain_with": pull.ChainWith = PullName(v); break;
                case "pull_with": pull.PullWith = PullName(v); break;
                case "offset_ms": pull.OffsetMs = Number(key, v); break;
                case "offset_pct": pull.OffsetPct = Number(key, v); break;
                case "after_deaths": pull.AfterDeaths = DeathCount.Parse(key, v); break;
                case "after_deaths_total": pull.AfterDeathsTotal = DeathCount.Parse(key, v); break;
                default:
                    throw new FormatException($"'{key}' is not a pull option (count, name, spirit_points, priority, hit_points, " +
                        "interrupt_ability, cooldown, cast_time, pull_duration, downtime, final, chain_with, pull_with, offset_ms, " +
                        "offset_pct, after_deaths, after_deaths_total)");
            }
        }
        if (count is not { } n)
        {
            if (groupValues) throw new FormatException("an enemy group line starts with count= or boss.N");
            return;
        }
        if ((cooldown > 0 || castTime != EnemyCast.DefaultCastSeconds) && ability == null)
            throw new FormatException("cooldown and cast_time need interrupt_ability");
        pull.Groups.Add(new EnemyGroup(n, name ?? "", spirit, priority, hitPoints, boss,
            ability != null ? new EnemyCast(ability, cooldown, castTime) : null));
    }

    /// <summary>The profile's text, which <see cref="Parse"/> reads back to the same profile.</summary>
    public string Format()
    {
        var text = new StringBuilder();
        AppendComment(text, Comment);
        text.Append("dungeon=").Append(Quote(Name)).Append('\n');
        text.Append("difficulty=").Append(N(Difficulty)).Append('\n');
        text.Append("hero_share=").Append(N(HeroShare)).Append('\n');
        text.Append("time_limit=").Append(N(TimeLimitSeconds)).Append('\n');
        foreach (var pull in Pulls)
        {
            AppendComment(text, pull.Comment);
            var options = PullOptions(pull);
            bool first = true;
            if (options.Count > 0 || pull.Groups is [{ Comment: { } c }, ..] && !string.IsNullOrWhiteSpace(c))
            {
                text.Append("pull.").Append(pull.Name).Append("=/").Append(string.Join(',', options)).Append('\n');
                first = false;
            }
            int boss = 0;
            foreach (var g in pull.Groups)
            {
                if (!first) AppendComment(text, g.Comment);
                text.Append("pull.").Append(pull.Name).Append(first ? "=/" : "+=/");
                first = false;
                if (g.Boss) text.Append("boss.").Append(N(++boss));
                else text.Append("count=").Append(N(g.Count));
                text.Append(",name=").Append(Quote(g.Name));
                if (g.SpiritPoints != 0) text.Append(",spirit_points=").Append(N(g.SpiritPoints));
                if (g.Priority) text.Append(",priority=1");
                if (g.HitPoints is { } hp) text.Append(",hit_points=").Append(N(hp));
                if (g.Cast is { } cast)
                {
                    text.Append(",interrupt_ability=").Append(Quote(cast.Ability))
                        .Append(",cooldown=").Append(N(cast.CooldownSeconds))
                        .Append(",cast_time=").Append(N(cast.CastSeconds));
                }
                text.Append('\n');
            }
        }
        return text.ToString();
    }

    // The pull options of a pull's text.
    private static List<string> PullOptions(DungeonPull pull)
    {
        var options = new List<string>();
        if (pull.PullDurationSeconds is { } duration) options.Add($"pull_duration={N(duration)}");
        if (pull.DowntimeSeconds != 0) options.Add($"downtime={N(pull.DowntimeSeconds)}");
        if (pull.ChainWith is { } chain) options.Add($"chain_with={chain}");
        if (pull.Final) options.Add("final=1");
        if (pull.Join is { } join)
        {
            options.Add($"pull_with={join.Pull}");
            if (join.OffsetMs is { } ms) options.Add($"offset_ms={N(ms)}");
            if (join.OffsetPct is { } pct) options.Add($"offset_pct={N(pct)}");
            if (join.AfterDeaths is { } deaths) options.Add($"after_deaths={deaths}");
            if (join.AfterDeathsTotal is { } total) options.Add($"after_deaths_total={total}");
        }
        return options;
    }

    // A line's options: split at each comma outside quotes.
    private static List<string> SplitOptions(string value)
    {
        var parts = new List<string>();
        var part = new StringBuilder();
        bool quoted = false;
        foreach (char c in value)
        {
            if (c == '"') quoted = !quoted;
            if (c == ',' && !quoted)
            {
                parts.Add(part.ToString().Trim());
                part.Clear();
            }
            else part.Append(c);
        }
        if (quoted) throw new FormatException("a quote has no closing quote");
        parts.Add(part.ToString().Trim());
        return parts;
    }

    private static string N(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static double Number(string key, string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
            ? n : throw new FormatException($"{key} is a number, not '{value}'");

    private static int Integer(string key, string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
            ? n : throw new FormatException($"{key} is a whole number, not '{value}'");

    private static string PullName(string value) =>
        DungeonPull.IsName(value) ? value : throw new FormatException($"'{value}' is not a pull name (letters, digits and '_')");

    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static string Quote(string value) => value.Contains(',') || value.Contains('"') || value != value.Trim() ? $"\"{value}\"" : value;

    private static FormatException Error(int lineNumber, string line, string problem) =>
        new($"Dungeon line {lineNumber} '{line}': {problem}.");
}
