using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// A defensive proc (<see cref="Hero.AddDefensiveProc"/>) never fires in a sim, and fires in a replay at each proof of it
/// in the log, with the logged hit's enemy. The proof here is Ruby Storm's evidence (its damage effect's hit), on a Rime
/// whose build has no Ruby Storm.
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

    [Fact]
    public void A_Replay_Fires_A_Defensive_Proc_At_Each_Proof_In_The_Log()
    {
        var script = new ReplayScript
        {
            Schema = ReplayScript.SchemaVersion,
            ReportCode = "test",
            FightId = 1,
            FightName = "Test",
            DurationSeconds = 10,
            Player = new ReplayPlayer { Id = 1, Name = "Tester", Hero = "Rime" },
            Character = Character(),
            Commands =
            [
                new ReplayCommand { Time = 0.5, Kind = CommandKind.EnemyArise, Enemy = Dummy, Name = "Dummy", MaxHp = 1e9, Hp = 1e9 },
                new ReplayCommand { Time = 0.5, Kind = CommandKind.EnemyArise, Enemy = Other, Name = "Other", MaxHp = 1e9, Hp = 1e9 },
            ],
            Observations = new ReplayObservations
            {
                // Two procs: one hitting two enemies in one server frame, then one on the other enemy.
                Damage = [Hit(5.0, Dummy), Hit(5.01, Other), Hit(8.0, Other)],
            },
        };
        Assert.Equal(0.0, ProcEvidence.For(RubyStorm.ProcKey)!.MinDelaySeconds);
        Assert.DoesNotContain(script.Character!.Augmentations, a => a.Id == RubyStorm.AspectId);

        var hero = ReplayDriver.CreateHero(script);
        var fired = new List<(double Time, int Enemy)>();
        hero.AddDefensiveProc(RubyStorm.ProcKey, (ctx, enemy) => fired.Add((ctx.CurrentTime.TotalSeconds, enemy!.Id)));
        new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false }).Run();

        Assert.Equal([(5.0, Dummy.Id), (8.0, Other.Id)], fired.Select(f => (Math.Round(f.Time, 3), f.Enemy)));
    }

    private static LogDamage Hit(double time, UnitRef target) =>
        new() { Time = time, AbilityId = ProofId, AbilityName = RubyStorm.AspectName, Target = target, Amount = 1000 };
}
