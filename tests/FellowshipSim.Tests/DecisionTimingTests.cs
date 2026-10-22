using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>The times a Monte Carlo fight decides and casts at.</summary>
public class DecisionTimingTests
{
    [Fact]
    public void After_An_Instant_Cast_The_Next_Decision_Is_At_The_Hasted_Global_Cooldown()
    {
        var hero = HeroImporter.Import(Fixture());
        var starts = new List<(SimTime At, Ability Ability, double Haste)>();
        hero.OnCastStarted += (ability, _, now) => starts.Add((now, ability, hero.GetHaste(now)));
        // Frost Bolt is always ready, so every decision casts.
        Fight(hero, "actions+=/cold_snap\nactions+=/frost_bolt", seconds: 60);

        var afterInstants = starts.Zip(starts.Skip(1)).Where(p => p.First.Ability is ColdSnap).ToList();
        Assert.NotEmpty(afterInstants);
        foreach (var (cast, next) in afterInstants)
        {
            Assert.Equal(cast.At + SimTime.FromSeconds(hero.GlobalCooldownSeconds / (1.0 + cast.Haste)), next.At);
        }
    }

    private static void Fight(Hero hero, string apl, int seconds, int targets = 1) =>
        // One thread: the simulator fights on the hero given.
        new MonteCarloSimulator(() => hero, AplParser.ParseScript(apl))
            .Run(SimTime.FromSeconds(seconds), iterations: 1, targets, degreeOfParallelism: 1);

    private static CharacterBuild Fixture() => System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
}
