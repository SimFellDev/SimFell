using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// A defensive proc (<see cref="Hero.AddDefensiveProc"/>) never fires in a sim, and fires in a replay at each proof of it
/// in the log, of each kind a proc roll reads: a hit (Ruby Storm's), an aura on the player (Hidden Power's), a debuff
/// applied to an enemy (Kindling's), a stack gained on an enemy (Hemotoxin's) and a resource jump (Venomous Delight's).
/// A defensive proc's hit deals the log's amount (<see cref="Hero.DealDefensiveProcHit"/>). The hero is the fixture Rime;
/// a replay with no casts rolls none of its own procs.
/// </summary>
public class DefensiveProcTests
{
    private static readonly UnitRef Dummy = new(100, 1);
    private static readonly UnitRef Other = new(101, 1);
    private static readonly int ProofId = LogIds.Effect(RubyStorm.DamageEffectId);

    private static CharacterBuild Character() => System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;

    [Fact]
    public void A_Sim_Never_Fires_A_Defensive_Proc()
    {
        var hero = HeroImporter.Import(Character());
        int fired = 0;
        hero.AddDefensiveProc(RubyStorm.ProcKey, (_, _) => fired++);
        new MonteCarloSimulator(() => hero, AplParser.ParseScript("actions=frost_bolt"))
            .Run(SimTime.FromSeconds(60), 1, 3, degreeOfParallelism: 1);
        Assert.Equal(0, fired);
    }

    private static ReplayScript Script(ReplayObservations observations, CharacterBuild? character = null) => new()
    {
        Schema = ReplayScript.SchemaVersion,
        ReportCode = "test",
        FightId = 1,
        FightName = "Test",
        DurationSeconds = 10,
        Player = new ReplayPlayer { Id = Player.Id, Name = "Tester", Hero = character?.Hero ?? "Rime" },
        Character = character ?? Character(),
        Commands =
        [
            new ReplayCommand { Time = 0.5, Kind = CommandKind.EnemyArise, Enemy = Dummy, Name = "Dummy", MaxHp = 1e9, Hp = 1e9 },
            new ReplayCommand { Time = 0.5, Kind = CommandKind.EnemyArise, Enemy = Other, Name = "Other", MaxHp = 1e9, Hp = 1e9 },
        ],
        Observations = observations,
    };

    private static readonly UnitRef Player = new(1, 1);

    /// <summary>Replays <paramref name="script"/> with a defensive proc on <paramref name="procKey"/>, and returns its firings.</summary>
    private static List<(double Time, int? Enemy)> Fired(ReplayScript script, string procKey)
    {
        var hero = ReplayDriver.CreateHero(script);
        var fired = new List<(double Time, int? Enemy)>();
        hero.AddDefensiveProc(procKey, (ctx, enemy) => fired.Add((Math.Round(ctx.CurrentTime.TotalSeconds, 3), enemy?.Id)));
        new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false }).Run();
        return fired;
    }

    [Fact]
    public void A_Replay_Fires_A_Defensive_Proc_At_Each_Hit_Proof()
    {
        // Two procs: one hitting two enemies in one server frame, then one on the other enemy.
        var script = Script(new ReplayObservations { Damage = [Hit(5.0, Dummy), Hit(5.01, Other), Hit(8.0, Other)] });
        Assert.Equal(0.0, ProcEvidence.For(RubyStorm.ProcKey)!.MinDelaySeconds);
        Assert.DoesNotContain(script.Character!.Augmentations, a => a.Id == RubyStorm.AspectId);
        Assert.Equal([(5.0, Dummy.Id), (8.0, Other.Id)], Fired(script, RubyStorm.ProcKey));
    }

    [Fact]
    public void A_Replay_Fires_A_Defensive_Proc_At_Each_Aura_Proof()
    {
        // Hidden Power's evidence: its stack aura on the player, from the player.
        var script = Script(new ReplayObservations { Auras = [Aura(5.0, "applybuff", HiddenPower.Stack.LogId, Player), Aura(7.0, "applybuffstack", HiddenPower.Stack.LogId, Player, 2)] });
        Assert.Equal([5.0, 7.0], Fired(script, HiddenPower.ProcKey).Select(f => f.Time));
    }

    [Fact]
    public void A_Replay_Fires_A_Defensive_Proc_At_Each_Enemy_Debuff_Proof()
    {
        // Kindling's evidence: its burn applied or refreshed on an enemy, at most a server frame before the roll.
        int burn = LogIds.Effect(Kindling.DamageEffectId);
        var script = Script(new ReplayObservations { Auras = [Aura(5.0, "applydebuff", burn, Dummy), Aura(8.0, "refreshdebuff", burn, Other)] });
        double frame = Math.Round(PeriodicEffect.ServerFrameSeconds, 3);
        Assert.Equal([(5.0 + frame, Dummy.Id), (8.0 + frame, Other.Id)], Fired(script, Kindling.ProcKey));
    }

    [Fact]
    public void A_Replay_Fires_A_Defensive_Proc_At_Each_Stack_Jump_Proof()
    {
        // Hemotoxin's evidence: its poison on an enemy gaining a stack.
        int poison = LogIds.Effect(Hemotoxin.PoisonEffectId);
        var script = Script(new ReplayObservations
        {
            Auras = [Aura(5.0, "applydebuff", poison, Dummy), Aura(6.0, "applydebuffstack", poison, Dummy, 2), Aura(6.5, "removedebuffstack", poison, Dummy, 1)],
        });
        Assert.Equal([(5.0, Dummy.Id), (6.0, Dummy.Id)], Fired(script, Hemotoxin.ProcKey));
    }

    [Fact]
    public void A_Replay_Fires_A_Defensive_Proc_At_Each_Resource_Proof()
    {
        // Venomous Delight's evidence: 10 Energy at once (a log reading of 1 Energy is 100), beyond 25 per second regenerated.
        LogResourceSnapshot Energy(double time, double energy) => new()
        {
            Time = time, Event = "damage",
            Resources = new() { ["2"] = new LogResourceValue { Amount = energy * 100, Max = Mara.HeroMaxResources * 100 } },
        };
        var script = Script(new ReplayObservations { Resources = [Energy(4.96, 50), Energy(5.0, 61), Energy(7.0, 110)] },
            new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        Assert.Equal([4.96], Fired(script, VenomousDelight.ProcKey).Select(f => f.Time));
    }

    [Fact]
    public void A_Defensive_Proc_Hit_Deals_The_Logs_Amount_In_An_Exact_Replay()
    {
        var script = Script(new ReplayObservations { Damage = [Hit(5.0, Dummy, 1234), Hit(8.0, Other, 5678)] });
        var hero = ReplayDriver.CreateHero(script);
        var dealt = new List<double>();
        hero.OnDamageDealt += (in dmg) => { if (dmg.AbilityId == ProofId) dealt.Add(dmg.Amount); };
        hero.AddDefensiveProc(RubyStorm.ProcKey, (ctx, enemy) => hero.DealDefensiveProcHit(ctx, enemy!, ProofId, RubyStorm.AspectName));
        var result = new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false }).Run();

        Assert.Equal([1234.0, 5678.0], dealt);
        Assert.Equal(0, result.LoggedHitsDealt);
        Assert.Equal([1234.0, 5678.0], result.AllHits.Where(h => h.AbilityId == ProofId).Select(h => h.EngineAmount));
    }

    [Fact]
    public void A_Logged_Pulse_Fires_At_Each_Group_Of_Its_Log_Hits_And_Only_In_A_Replay()
    {
        // Two pulses: one hitting both enemies in one server frame, then one on the first.
        var script = Script(new ReplayObservations { Damage = [Hit(2.0, Dummy), Hit(2.001, Other), Hit(3.5, Dummy)] });
        var hero = ReplayDriver.CreateHero(script);
        var pulses = new List<double>();
        hero.AddLoggedPulse(RubyStorm.ProcKey, (ctx, _) => pulses.Add(Math.Round(ctx.CurrentTime.TotalSeconds, 3)));
        Assert.False(hero.PulsesFollowLog);
        new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false }).Run();
        Assert.True(hero.PulsesFollowLog);
        Assert.Equal([2.0, 3.5], pulses);

        var simmed = HeroImporter.Import(Character());
        int fired = 0;
        simmed.AddLoggedPulse(RubyStorm.ProcKey, (_, _) => fired++);
        new MonteCarloSimulator(() => simmed, AplParser.ParseScript("actions=frost_bolt")).Run(SimTime.FromSeconds(30), 1, 1, degreeOfParallelism: 1);
        Assert.False(simmed.PulsesFollowLog);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void With_Each_Hit_Proves_Every_Hit_In_A_Server_Frame_Is_A_Proof()
    {
        // Hits on two enemies in one server frame: one proof as a group, two with EachHitProves.
        var script = Script(new ReplayObservations { Damage = [Hit(5.0, Dummy), Hit(5.01, Other), Hit(8.0, Other)] });
        var grouped = new ProcEvidence.Evidence("test", ProofId);
        var each = grouped with { EachHitProves = true };
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 });
        Assert.Equal([(5.0, (UnitRef?)Dummy), (8.0, Other)], ProcEvidence.Times(script, grouped, hero));
        Assert.Equal([(5.0, (UnitRef?)Dummy), (5.01, Other), (8.0, Other)], ProcEvidence.Times(script, each, hero));
    }

    private static LogDamage Hit(double time, UnitRef target, double amount = 1000) =>
        new() { Time = time, AbilityId = ProofId, AbilityName = RubyStorm.AspectName, Target = target, Amount = amount };

    private static LogAura Aura(double time, string type, int logId, UnitRef target, int? stack = null) =>
        new() { Time = time, Type = type, AbilityId = logId, AbilityName = logId.ToString(), Source = Player, Target = target, Stack = stack };
}
