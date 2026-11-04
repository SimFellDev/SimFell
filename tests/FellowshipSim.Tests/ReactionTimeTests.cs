using System.Globalization;
using System.Text;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Engine.Replay;
using FellowshipSim.Logs;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>
/// How long players take to cast once they can, for one hero over every downloaded fight (data/fights) of the patch the
/// engine models. An exact replay casts at the log's times, so at each cast on the global cooldown the engine knows what
/// held the hero last: the global cooldown, a cast time, a channel or the ability's own cooldown. The delay is the log's
/// cast time less the end of that hold. REACTION_HERO names the hero; without it the test does nothing. The result is
/// saved as data/accuracy/&lt;Hero&gt;/reaction.txt.
/// Run with: REACTION_HERO=Rime dotnet test -c Release --filter ReactionTimeTests
/// </summary>
public class ReactionTimeTests(ITestOutputHelper output)
{
    /// <summary>A delay longer than this is the player doing something else (moving, waiting), not reacting.</summary>
    private const double IdleSeconds = 1.0;

    private sealed record Delay(string Hold, string Ability, double Seconds, string Fight, double At);

    [Fact]
    public async Task Delay_After_Each_Hold_Over_Current_Patch_Fights()
    {
        if (Environment.GetEnvironmentVariable("REACTION_HERO") is not { Length: > 0 } heroName)
        {
            output.WriteLine("REACTION_HERO is not set; nothing to measure.");
            return;
        }
        var library = new ReplayLibrary(Path.Combine(TestPaths.RepositoryRoot, "data"));
        var delays = new List<Delay>();
        int fights = 0;
        foreach (var (reportCode, fightId) in library.ListDownloadedFights())
        {
            byte[] raw = await library.GetRawFightBytesAsync(null, reportCode, fightId);
            foreach (var player in ReplayScriptBuilder.Players(raw).Where(p => p.Hero.Equals(heroName, StringComparison.OrdinalIgnoreCase)))
            {
                var script = ReplayScript.FromJson(ReplayScriptBuilder.Build(raw, player.Id));
                // A fight without the player's character (no combatantinfo) has no hero to replay.
                if (script.FightDate is not { } date || !GamePatch.Current.Covers(date, null) || script.Character == null) continue;
                var hero = ReplayDriver.CreateHero(script);
                var driver = new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false });
                Watch(hero, $"{reportCode} #{fightId}", delays);
                driver.Run();
                fights++;
            }
        }
        if (fights == 0)
        {
            output.WriteLine($"No {heroName} fight of patch {GamePatch.Current.Name} in {library.FightsDirectory}.");
            return;
        }

        string text = Format(heroName, fights, delays);
        string folder = Path.Combine(library.DataDirectory, "accuracy", heroName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "reaction.txt"), text);
        output.WriteLine(text);
        Assert.NotEmpty(delays);
    }

    /// <summary>Records, at each cast on the global cooldown, the delay after whatever held the hero last.</summary>
    private static void Watch(Hero hero, string fight, List<Delay> delays)
    {
        SimTime gcdEnd = SimTime.FromSeconds(-1), castEnd = SimTime.FromSeconds(-1), channelEnd = SimTime.FromSeconds(-1);
        var readyAt = new Dictionary<string, SimTime>();
        hero.OnCastStarted += (ability, _, now) =>
        {
            if (ability.IgnoresGcd || ability.UsableDuringGcd) return;
            var holds = new List<(string Hold, SimTime End)> { ("global cooldown", gcdEnd), ("cast time", castEnd), ("channel", channelEnd) };
            if (readyAt.TryGetValue(ability.Name, out var ready)) holds.Add(("cooldown", ready));
            var last = holds.MaxBy(h => h.End);
            // The global cooldown this cast starts, for the next one.
            gcdEnd = hero.GlobalCooldownEndTime;
            if (last.End.TotalSeconds < 0) return;
            delays.Add(new Delay(last.Hold, ability.Name, (now - last.End).TotalSeconds, fight, now.TotalSeconds));
        };
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (!ability.IsInstant) castEnd = now;
            if (ability.Cooldown > SimTime.Zero && hero.Cooldowns.TryGetValue(ability.Name, out var cooldown))
            {
                readyAt[ability.Name] = cooldown.ChargesAt(now) == 0 ? now + SimTime.FromSeconds(hero.GetCooldownRemains(ability.Name, now)) : now;
            }
        };
        hero.OnChannelEnded += (_, _, now, clipped) => { if (!clipped) channelEnd = now; };
    }

    private static string Format(string hero, int fights, List<Delay> delays)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{hero}: delay from the end of the last hold to the next cast on the global cooldown, {fights} fights, patch {GamePatch.Current.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Statistics over delays under {IdleSeconds:0.0}s; 'idle' counts the longer ones. 1 frame = {PeriodicEffect.ServerFrameSeconds:0.000}s.");
        sb.AppendLine();
        Table(sb, delays.GroupBy(d => d.Hold).OrderByDescending(g => g.Count()).Select(g => (g.Key, g.ToList())));
        sb.AppendLine();
        sb.AppendLine("After a channel, by the next ability:");
        Table(sb, delays.Where(d => d.Hold == "channel").GroupBy(d => d.Ability).OrderByDescending(g => g.Count()).Select(g => (g.Key, g.ToList())));
        sb.AppendLine();
        sb.AppendLine("After the global cooldown, by the next ability:");
        Table(sb, delays.Where(d => d.Hold == "global cooldown").GroupBy(d => d.Ability).OrderByDescending(g => g.Count()).Take(12).Select(g => (g.Key, g.ToList())));
        return sb.ToString();
    }

    private static void Table(StringBuilder sb, IEnumerable<(string Name, List<Delay> Delays)> groups)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"{"",-22}{"n",6}{"idle",6}{"<=1fr",7}{"min",8}{"p10",8}{"p25",8}{"median",8}{"p75",8}{"p90",8}{"mean",8}");
        foreach (var (name, all) in groups)
        {
            var s = all.Where(d => d.Seconds < IdleSeconds).Select(d => d.Seconds).Order().ToList();
            if (s.Count == 0) continue;
            double P(double q) => s[(int)Math.Round(q * (s.Count - 1))];
            int frame = s.Count(x => x <= PeriodicEffect.ServerFrameSeconds + 0.005);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{name,-22}{all.Count,6}{all.Count - s.Count,6}{frame,7}{s[0],8:0.000}{P(0.1),8:0.000}{P(0.25),8:0.000}{P(0.5),8:0.000}{P(0.75),8:0.000}{P(0.9),8:0.000}{s.Average(),8:0.000}");
        }
    }
}
