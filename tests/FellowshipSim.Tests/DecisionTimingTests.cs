using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>The times a Monte Carlo fight decides, casts and runs auras out at.</summary>
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

    [Fact]
    public void An_Action_Whose_Cooldown_Is_Ready_At_T_Casts_At_T()
    {
        // Strike's cooldown is longer than the global cooldown, so the hero waits with nothing to cast.
        var hero = Tester.Create();
        var starts = CastStarts(hero);
        Fight(hero, "actions+=/strike", seconds: 20);
        Assert.Equal([0L, 4250, 8500, 12750, 17000], starts.Select(s => s.At.Milliseconds));
    }

    [Fact]
    public void A_Resource_Threshold_Crossed_At_T_Casts_At_T()
    {
        // Gather's Focus arrives 2.345 s after its cast, while the hero waits with nothing to cast.
        var hero = Tester.Create();
        var starts = CastStarts(hero);
        Fight(hero, "actions+=/strike,if=focus>=5\nactions+=/gather", seconds: 5);
        Assert.Equal([("Gather", 0L), ("Strike", 2345L)], starts.Select(s => (s.Ability.Name, s.At.Milliseconds)));
    }

    [Fact]
    public void A_Remaining_Time_Passing_Its_Threshold_At_T_Casts_At_T()
    {
        var hero = Tester.Create();
        var starts = CastStarts(hero);
        SimTime? markExpires = null;
        hero.Auras.OnGained += (aura, _, _) => { if (aura.Def == Tester.Mark) markExpires = aura.ExpiresAt; };
        Fight(hero, "actions+=/strike,if=buff.mark.up&buff.mark.remains<2\nactions+=/mark", seconds: 6);
        // The remaining time is below 2 s from the first millisecond after 2 s before the expiry.
        Assert.Equal([("Mark", 0L), ("Strike", markExpires!.Value.Milliseconds - 1999)], starts.Select(s => (s.Ability.Name, s.At.Milliseconds)));
    }

    [Fact]
    public void A_Product_Of_Remaining_Times_Passing_Its_Threshold_At_T_Casts_At_T()
    {
        var hero = Tester.Create();
        var starts = CastStarts(hero);
        SimTime? markExpires = null;
        hero.Auras.OnGained += (aura, _, _) => { if (aura.Def == Tester.Mark) markExpires = aura.ExpiresAt; };
        // After Mark and Strike the hero waits: the product falls as both remaining times fall.
        Fight(hero, "actions+=/mark\nactions+=/strike\nactions+=/gather,if=buff.mark.remains*cooldown.strike.remains<4", seconds: 5);

        long strike = starts.Single(s => s.Ability.Name == "Strike").At.Milliseconds;
        long expires = markExpires!.Value.Milliseconds, ready = strike + 4250;
        // The first millisecond after Strike's global cooldown at which the product is below 4.
        long expected = strike + 1500;
        while ((expires - expected) / 1000.0 * ((ready - expected) / 1000.0) >= 4) expected++;
        Assert.Equal(expected, starts.Single(s => s.Ability.Name == "Gather").At.Milliseconds);
    }

    [Fact]
    public void An_Aura_Runs_Out_At_Its_Expiry_While_Nothing_Reads_It()
    {
        var hero = Tester.Create();
        SimTime? markExpires = null;
        var runOut = new List<(SimTime Now, SimTime Clock)>();
        hero.Auras.OnGained += (aura, _, _) => { if (aura.Def == Tester.Mark) markExpires = aura.ExpiresAt; };
        hero.Auras.OnRemoved += (aura, now) => { if (aura.Def == Tester.Mark) runOut.Add((now, hero.SimContext!.CurrentTime)); };
        // Hold's 10 s cast covers Mark's expiry, and nothing reads the auras during it.
        Fight(hero, "actions+=/mark\nactions+=/hold", seconds: 12);
        Assert.Equal([(markExpires!.Value, markExpires.Value)], runOut);
    }

    private static List<(SimTime At, Ability Ability)> CastStarts(Hero hero)
    {
        var starts = new List<(SimTime, Ability)>();
        hero.OnCastStarted += (ability, _, now) => starts.Add((now, ability));
        return starts;
    }

    private static void Fight(Hero hero, string apl, int seconds, int targets = 1) =>
        // One thread: the simulator fights on the hero given.
        new MonteCarloSimulator(() => hero, AplParser.ParseScript(apl))
            .Run(SimTime.FromSeconds(seconds), iterations: 1, targets, degreeOfParallelism: 1);

    private static CharacterBuild Fixture() => System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;

    /// <summary>A hero with no stats, talents or gear, and four abilities that deal no damage.</summary>
    private sealed class Tester : Hero
    {
        public static readonly ResourceDef Focus = new("Focus", Max: 10);
        public static readonly AuraDef Mark = AuraDef.Internal("Mark", 5.555);

        public override string MainStat => "Intellect";

        public static Tester Create()
        {
            var hero = new Tester();
            hero.Initialize();
            return hero;
        }

        public override IReadOnlyList<TalentSlot> TalentTreeSlots => [];

        public override void Initialize()
        {
            AddResource(Focus);
            Abilities.Add(new TestAbility(this, 1, "Strike", cooldownSeconds: 4.25, (_, _) => { }));
            // Gather's Focus arrives after a delay.
            Abilities.Add(new TestAbility(this, 2, "Gather", cooldownSeconds: 60,
                (ctx, hero) => ctx.Schedule(SimTime.FromSeconds(2.345), c => hero.GainResource(Focus, 5, c), "Gather")));
            Abilities.Add(new TestAbility(this, 3, "Mark", cooldownSeconds: 60, (ctx, hero) => hero.Auras.Apply(Mark, ctx.CurrentTime)));
            Abilities.Add(new TestAbility(this, 4, "Hold", cooldownSeconds: 60, (_, _) => { }, castSeconds: 10));
        }

        public override void ResetForIteration() => ResetCombatState();
    }

    /// <summary>An ability with a cooldown whose cast does <paramref name="effect"/>.</summary>
    private sealed class TestAbility : Ability
    {
        private readonly Action<SimulationContext, Hero> _effect;

        public TestAbility(Hero owner, int id, string name, double cooldownSeconds, Action<SimulationContext, Hero> effect, double castSeconds = 0)
            : base(owner, id, name)
        {
            _effect = effect;
            SetCooldown(cooldownSeconds);
            CastTime = SimTime.FromSeconds(castSeconds);
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target)
        {
            TriggerCooldown(ctx);
            _effect(ctx, Owner);
        }
    }
}
