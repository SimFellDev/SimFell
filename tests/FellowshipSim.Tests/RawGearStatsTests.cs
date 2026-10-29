using System.Text.Json.Nodes;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// A character read from a log holds the gear's raw stats; the engine adds the base stats, stat enchants, gem nodes,
/// sigils and relics. What it starts a fight with must be what the log's combatantinfo totals say, so a bonus changed in
/// its own class (a gem node's rating, say) shows here when it no longer matches the game. Fixture: four players of
/// patch 0.5.0.0 with no party buff on their Stamina (c6QH41C2dYq7P3zb fight 13, RCKJm2rFjYMkyLAv fight 10,
/// Rzg1nQNHpm2fhYtB fight 19, jHDC613ZzbTYR8wW fight 21).
/// </summary>
public class RawGearStatsTests
{
    public static TheoryData<int> Players() => new(Enumerable.Range(0, Fixture().Count));

    [Theory]
    [MemberData(nameof(Players))]
    public void The_Engine_Starts_From_The_Gear_With_The_Logs_Totals(int index)
    {
        var entry = Fixture()[index]!;
        var combatant = entry["combatant"]!.AsObject();
        var build = CharacterReader.FromCombatantInfo(combatant, "player", (string)entry["hero"]!);
        var hero = HeroImporter.Import(build);
        hero.ResetForIteration();
        var now = SimTime.Zero;
        double Rating(StatType type, double rating) => rating + hero.Auras.Fold(type, 0.0, now);

        string fight = (string)entry["fight"]!;
        double Log(string key) => (double)combatant[key]!;
        double logMain = new[] { "strength", "agility", "intellect" }.Max(k => combatant[k] is { } v ? (double)v : 0.0);
        Assert.True(Math.Abs(logMain - hero.PrimaryAttribute(now)) < 1.0, $"{fight}: Main Stat {hero.PrimaryAttribute(now):F1}, log {logMain}");
        Assert.True(Math.Abs(Log("stamina") - hero.Stats.TotalStamina) < 1.0, $"{fight}: Stamina {hero.Stats.TotalStamina:F1}, log {Log("stamina")}");
        Assert.Equal((fight, Log("crit")), (fight, Rating(StatType.CritRating, hero.Stats.CritRating)));
        Assert.Equal((fight, Log("haste")), (fight, Rating(StatType.HasteRating, hero.Stats.HasteRating)));
        Assert.Equal((fight, Log("expertise")), (fight, Rating(StatType.ExpertiseRating, hero.Stats.ExpertiseRating)));
        Assert.Equal((fight, Log("spirit")), (fight, Rating(StatType.SpiritRating, hero.Stats.SpiritRating)));
    }

    [Fact]
    public void A_Log_Character_Saves_Its_Raw_Gear_Stats_And_Reads_Back_The_Same()
    {
        var entry = Fixture()[0]!;
        var build = CharacterReader.FromCombatantInfo(entry["combatant"]!.AsObject(), "CJ", (string)entry["hero"]!);
        // CJ's crit: the gear's 89, before Berserker's Zeal II (24) and Banshee's Cunning (6) make the log's 119.
        Assert.Equal(89, build.CritRating);

        string text = new SimcProfile(build, "").Format();
        Assert.Contains("crit_rating=89", text);
        var read = SimcProfile.Parse(text).Build;
        var before = HeroImporter.Import(build);
        var after = HeroImporter.Import(read);
        before.ResetForIteration();
        after.ResetForIteration();
        Assert.Equal(before.PrimaryAttribute(SimTime.Zero), after.PrimaryAttribute(SimTime.Zero), 9);
        Assert.Equal(before.CritChance(SimTime.Zero), after.CritChance(SimTime.Zero), 9);
        Assert.Equal(before.GetHaste(SimTime.Zero), after.GetHaste(SimTime.Zero), 9);
        Assert.Equal(before.Expertise(SimTime.Zero), after.Expertise(SimTime.Zero), 9);
        Assert.Equal(before.SpiritPercent(SimTime.Zero), after.SpiritPercent(SimTime.Zero), 9);
    }

    private static JsonArray Fixture() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "raw_gear_combatants.json")))!.AsArray();
}
