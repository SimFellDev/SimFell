using System.Text.Json;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Dungeons.Affixes;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>
/// Dungeon affixes in a dungeon fight: the affixes key of the dungeon's text, Anomalous Orbs (an orb's priority flag,
/// Detonate, the party's stops and the hero's interrupts, the damage it takes, its death and Anomalous Residue, the orbs by
/// rule), Storm Shield (its timer and Storm's Fury) and Ultimatum (its cooldown reduction in sims and replays).
/// </summary>
public class DungeonAffixTests
{
    private static CharacterBuild Build(string hero) => JsonSerializer.Deserialize<CharacterBuild>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{hero.ToLowerInvariant()}_character.json")))!;

    /// <summary>One hit of a fight: its time, target, ability, whether the target was casting, and the target's incoming damage multiplier.</summary>
    private sealed record Hit(double Time, EnemyTarget Target, int AbilityId, bool Casting, double Incoming);

    /// <summary>One fight's hits, each enemy cast change (time, enemy, casting), the hero's interrupts, the hero, and its residue power changes.</summary>
    private sealed record FightLog(List<Hit> Hits, List<(double Time, EnemyTarget Enemy, bool Casting)> Casts, List<double> Interrupts, List<EnemyTarget> Deaths,
        List<(double Time, double CritChance, int Fury, int Shield, int Residue, int ResidueMax)> Auras, Hero Hero);

    private static FightLog Fight(DungeonProfile dungeon, string actions, string hero = "Gunde", int seed = 1)
    {
        var hits = new List<Hit>();
        var casts = new List<(double, EnemyTarget, bool)>();
        var interrupts = new List<double>();
        var deaths = new List<EnemyTarget>();
        var auras = new List<(double, double, int, int, int, int)>();
        Hero? made = null;
        var simulator = new MonteCarloSimulator(() =>
        {
            var h = HeroImporter.Import(Build(hero));
            made = h;
            var watched = new HashSet<EnemyTarget>(ReferenceEqualityComparer.Instance);
            h.OnDamageDealt += (in DamageContext hit) =>
            {
                if (hit.Target is not { } t) return;
                var now = hit.Timestamp;
                hits.Add(new Hit(now.TotalSeconds, t, hit.AbilityId, t.IsCasting, t.Auras.Fold(StatType.IncomingDamageMultiplier, 1.0, now)));
                var residue = AnomalousOrbs.Residue.Values.Select(def => h.Auras.GetStacks(def, now)).ToList();
                auras.Add((now.TotalSeconds, h.CritChance(now), h.Auras.GetStacks(StormShield.StormsFury, now), h.Auras.GetStacks(StormShield.Shield, now), residue.Sum(), residue.Max()));
                if (watched.Add(t))
                {
                    if (t.IsCasting) casts.Add((t.CastStart.TotalSeconds, t, true));
                    t.CastChanged += (c, e) => casts.Add((c.CurrentTime.TotalSeconds, e, e.IsCasting));
                }
            };
            h.OnInterrupt += (ctx, _, _) => interrupts.Add(ctx.CurrentTime.TotalSeconds);
            h.OnEnemyDefeated += (_, e) => deaths.Add(e);
            return h;
        }, AplParser.ParseScript(actions));
        simulator.Run(SimTime.FromSeconds(dungeon.TimeLimitSeconds), iterations: 1, firstSeed: seed, degreeOfParallelism: 1,
            fight: new FightOptions(Dungeon: dungeon));
        return new FightLog(hits, casts, interrupts, deaths, auras, made!);
    }

    private const string Attacks = "actions=switch_target,target_if=target.is_priority\nactions+=/attack";

    private static DungeonProfile Dungeon(string pulls, string affixes = "anomalous_orbs") =>
        DungeonProfile.Parse($"dungeon=Test\ndifficulty=0\ntime_limit=600\naffixes={affixes}\n{pulls}");

    // A trash pull with one orb in it from the start, dying at 'dies'.
    private static string OrbPull(double dies = 30) =>
        $"pull.1=/duration=60\npull.1+=/count=2,name=Trash Mob,hit_points=100000\npull.1+=/count=1,name={AnomalousOrbs.OrbName},dies={dies}";

    [Fact]
    public void The_Affixes_Key_Reads_Back_From_Its_Text()
    {
        Assert.Equal(["ultimatum", "anomalous_orbs", "storm_shield"], DungeonAffix.All.Select(a => a.Key));
        Assert.Contains(DungeonProfile.AffixesKey, DungeonProfile.Keys);
        var dungeon = Dungeon("pull.1=/duration=30,count=1,name=Imp,hit_points=900", "anomalous_orbs,storm_shield");
        Assert.Equal(["anomalous_orbs", "storm_shield"], dungeon.Affixes);
        Assert.Contains("\naffixes=anomalous_orbs,storm_shield\n", dungeon.Format());
        Assert.Equal(dungeon.Format(), DungeonProfile.Parse(dungeon.Format()).Format());
        Assert.Equal(dungeon.Affixes, DungeonProfile.Parse(dungeon.Format()).Affixes);

        var none = DungeonProfile.Parse("dungeon=Test\npull.1=/duration=30,count=1,name=Imp,hit_points=900");
        Assert.Empty(none.Affixes);
        Assert.DoesNotContain("affixes", none.Format());
        var problem = Assert.ThrowsAny<FormatException>(() => Dungeon("pull.1=/duration=30,count=1,name=Imp,hit_points=900", "truesight"));
        Assert.Contains("'truesight' is not a dungeon affix", problem.Message);
        Assert.ThrowsAny<FormatException>(() => Dungeon("pull.1=/duration=30,count=1,name=Imp,hit_points=900", "storm_shield,storm_shield"));
    }

    [Fact]
    public void The_Affix_Values_Are_The_Game_Data()
    {
        // Game data, build 26320967: dungeon affix 24's ability 1949, creature 286 and its abilities 1903 and 1902.
        Assert.Equal(24, AnomalousOrbs.AffixId);
        Assert.Equal((128, 148, 3, 3), (AnomalousOrbs.Constants.SpawnTimeMin, AnomalousOrbs.Constants.SpawnTimeMax,
            AnomalousOrbs.Constants.NumOfOrbs, AnomalousOrbs.Constants.OrbRandomMaxDelayBetweenIndividualOrbSpawn));
        Assert.Equal((15, 0, 12, 9), (AnomalousOrbs.DetonateCastTime, AnomalousOrbs.DetonateInitialCooldown,
            AnomalousOrbs.DetonateInterruptCooldown, AnomalousOrbs.DetonateCcCooldown));
        Assert.Equal(4, AnomalousOrbs.AnomalousInstabilityIncomingDamageMultiplier);
        Assert.Equal((480, 3, 60), (AnomalousOrbs.Constants.AnomalousResidueAdditionalGemPowerPerStack,
            AnomalousOrbs.Constants.AnomalousResidueMaxStacks, AnomalousOrbs.Constants.AnomalousResidueDuration));
        Assert.Equal(3727.757, AnomalousOrbGameCreature.BASE_HEALTH, 3);
        // Dungeon affix 25's ability 1946 and effect 2699.
        Assert.Equal(25, StormShield.AffixId);
        Assert.Equal((59, 72, 8), (StormShield.Constants.SpawnPeriodMin, StormShield.Constants.SpawnPeriodMax, StormShield.Constants.CountdownTimer));
        Assert.Equal((0.2, 20), (StormShield.StormsFuryCritChance, StormShield.StormsFuryDuration));
    }

    [Fact]
    public void An_Orb_Is_A_Priority_Target_With_Its_Creatures_Health()
    {
        var log = Fight(Dungeon(OrbPull()), Attacks);
        var orb = log.Hits.First(h => h.Target.Name == AnomalousOrbs.OrbName).Target;
        Assert.True(orb.IsPriority);
        Assert.Equal(0, orb.SpiritPointValue);
        Assert.Equal(3727.757 * DifficultyScaling.Base.Health, orb.MaxHealth, 2);
        // switch_target,target_if=target.is_priority: the hero's hits go to the orb while it is in combat.
        Assert.All(log.Hits.Where(h => h.AbilityId == 1249 && h.Time is > 1 and < 29), h => Assert.Same(orb, h.Target));
        // Without the affix the line is an enemy like any other.
        var plain = Fight(DungeonProfile.Parse($"dungeon=Test\ndifficulty=0\ntime_limit=600\n{OrbPull()}"), "actions=attack");
        Assert.DoesNotContain(plain.Hits, h => h.Target.IsPriority);
        Assert.All(plain.Hits, h => Assert.False(h.Target.IsCasting));
    }

    [Fact]
    public void An_Orb_Casts_Detonate_And_The_Party_Stops_Each_Cast()
    {
        var log = Fight(Dungeon(OrbPull()), Attacks);
        var changes = log.Casts.Where(c => c.Enemy.Name == AnomalousOrbs.OrbName).ToList();
        // Cast at once, stopped 2.15 s later; recast 9 s after the stop, stopped 2.15 s after it begins; and so on to its death.
        double stop = AnomalousOrbs.PartyFirstStopSeconds, recast = AnomalousOrbs.DetonateCcCooldown, again = AnomalousOrbs.PartyRecastStopSeconds;
        var expected = new List<(double, bool)> { (0, true), (stop, false) };
        for (double t = stop + recast; t < 30; t += again + recast) expected.AddRange([(t, true), (t + again, false)]);
        Assert.Equal(expected.Count, changes.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Item1, changes[i].Time, 3);
            Assert.Equal(expected[i].Item2, changes[i].Casting);
        }
    }

    [Fact]
    public void The_Heros_Interrupt_Stops_A_Cast_And_The_Orb_Recasts_Twelve_Seconds_Later()
    {
        var log = Fight(Dungeon(OrbPull()), "actions=switch_target,target_if=target.is_priority\nactions+=/jawbreaker,if=target.casting\nactions+=/attack");
        Assert.NotEmpty(log.Interrupts);
        double interrupt = log.Interrupts[0];
        Assert.True(interrupt < AnomalousOrbs.PartyFirstStopSeconds);
        var begins = log.Casts.Where(c => c.Enemy.Name == AnomalousOrbs.OrbName && c.Casting).Select(c => c.Time).ToList();
        Assert.Contains(begins, t => Math.Abs(t - (interrupt + AnomalousOrbs.DetonateInterruptCooldown)) < 1e-3);
    }

    [Fact]
    public void An_Orb_Dies_At_Its_Time_And_Gives_The_Hero_A_Stack_Of_Residue()
    {
        var log = Fight(Dungeon(OrbPull(dies: 20)), Attacks);
        var orb = log.Hits.First(h => h.Target.Name == AnomalousOrbs.OrbName).Target;
        Assert.True(orb.HasDied);
        Assert.Equal(20, orb.DiesAt.TotalSeconds, 6);
        Assert.All(log.Auras.Where(a => a.Time < 20), a => Assert.Equal(0, a.Residue));
        Assert.All(log.Auras.Where(a => a.Time is > 20 and < 80), a => Assert.Equal(1, a.Residue));
        Assert.All(log.Auras.Where(a => a.Time > 80.5), a => Assert.Equal(0, a.Residue));
    }

    [Fact]
    public void An_Orb_Takes_Four_Times_The_Damage_While_It_Is_Not_Casting()
    {
        var log = Fight(Dungeon(OrbPull()), Attacks);
        var onOrb = log.Hits.Where(h => h.Target.Name == AnomalousOrbs.OrbName).ToList();
        Assert.Contains(onOrb, h => h.Casting);
        Assert.Contains(onOrb, h => !h.Casting);
        Assert.All(onOrb, h => Assert.Equal(h.Casting ? 1.0 : 4.0, h.Incoming, 9));
        Assert.All(log.Hits.Where(h => h.Target.Name != AnomalousOrbs.OrbName), h => Assert.Equal(1.0, h.Incoming, 9));
    }

    [Fact]
    public void A_Hit_Takes_The_Targets_Incoming_Multiplier_Unless_Its_Preset_Has_It_Off()
    {
        var hero = HeroImporter.Import(Build("Rime"));
        var ctx = new SimulationContext(hero);
        hero.BeginFight(ctx);
        var orb = ctx.AddEnemy(1001, 1, AnomalousOrbs.OrbName);
        var now = ctx.CurrentTime;
        double without = hero.DamageMultiplierAgainst(orb, now, incoming: false);
        orb.Auras.Apply(AnomalousOrbs.Instability, now);
        Assert.Equal(4 * without, hero.DamageMultiplierAgainst(orb, now), 9);
        // A hit of an effect whose preset has ApplyTargetIncomingDamageHealingMultipliers off takes none of it.
        Assert.Equal(without, hero.DamageMultiplierAgainst(orb, now, incoming: false), 9);
        orb.Auras.Remove(AnomalousOrbs.Instability, now);
        Assert.Equal(without, hero.DamageMultiplierAgainst(orb, now), 9);
    }

    [Fact]
    public void An_Orbs_Death_Gives_A_Stack_Of_Residue_Of_Its_Colour_Up_To_Three_For_Sixty_Seconds()
    {
        var hero = HeroImporter.Import(Build("Gunde"));
        var ctx = new SimulationContext(hero);
        hero.BeginFight(ctx);
        var ruby = AnomalousOrbs.Residue[GemColor.Ruby];
        Assert.Equal(60, ruby.Duration.TotalSeconds);
        Assert.Equal(3, ruby.MaxStacks);
        double before = hero.Gems.FirstOrDefault(g => g.Color == GemColor.Ruby)?.Power ?? 0;
        AnomalousOrbs.SetResidue(hero, GemColor.Ruby, 2, ctx.CurrentTime);
        var gem = hero.Gems.First(g => g.Color == GemColor.Ruby);
        double scale = hero.RelicPassives.Aggregate(1.0, (m, r) => m * r.GemPowerMultiplier);
        Assert.Equal(gem.BasePower + 960 * scale, gem.Power, 6);
        AnomalousOrbs.SetResidue(hero, GemColor.Ruby, 0, ctx.CurrentTime);
        Assert.Equal(before == 0 ? gem.BasePower : before, gem.Power, 6);

        // Twelve orbs die at 10 s: six colours, so a colour reaches more than three stacks only through its cap.
        var many = Dungeon($"pull.1=/duration=100\npull.1+=/count=2,name=Trash Mob,hit_points=100000\npull.1+=/count=12,name={AnomalousOrbs.OrbName},dies=10");
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var log = Fight(many, "actions=attack", seed: seed);
            var after = log.Auras.Where(a => a.Time is > 10 and < 70).ToList();
            Assert.NotEmpty(after);
            Assert.All(after, a => Assert.InRange(a.ResidueMax, 1, AnomalousOrbs.Constants.AnomalousResidueMaxStacks));
            Assert.All(after, a => Assert.InRange(a.Residue, 3, 12));
            Assert.All(log.Auras.Where(a => a.Time > 70.5), a => Assert.Equal(0, a.Residue));
        }
    }

    [Fact]
    public void Orbs_Spawn_By_Rule_In_Combat_When_The_Text_Has_No_Orb()
    {
        var dungeon = Dungeon("pull.1=/duration=400\npull.1+=/count=3,name=Trash Mob,hit_points=1000000");
        var log = Fight(dungeon, Attacks);
        var orbs = log.Deaths.Where(t => t.Name == AnomalousOrbs.OrbName).ToList();
        // 400 s of combat: two spawns of three (128 to 148 s each).
        Assert.Equal(6, orbs.Count);
        Assert.All(orbs, o => Assert.True(o.IsPriority));
        Assert.All(orbs, o => Assert.Equal(0, o.SpiritPointValue));
        var first = orbs.Min(o => o.DiesAt.TotalSeconds) - AnomalousOrbs.RuleOrbSeconds;
        Assert.InRange(first, AnomalousOrbs.Constants.SpawnTimeMin, AnomalousOrbs.Constants.SpawnTimeMax + AnomalousOrbs.Constants.OrbRandomMaxDelayBetweenIndividualOrbSpawn);
        // The second spawn 128 to 148 s after the first, each orb within 3 s of its spawn.
        var second = orbs.Max(o => o.DiesAt.TotalSeconds) - AnomalousOrbs.RuleOrbSeconds;
        Assert.InRange(second - first, AnomalousOrbs.Constants.SpawnTimeMin - 3, AnomalousOrbs.Constants.SpawnTimeMax + 3);

        // A text with an orb line has those orbs alone.
        var lined = Fight(Dungeon(OrbPull()), Attacks);
        Assert.Single(lined.Deaths, t => t.Name == AnomalousOrbs.OrbName);
    }

    [Fact]
    public void The_Spawn_Timer_Runs_In_Combat_Alone()
    {
        // 100 s of combat, 200 s of downtime, then 100 s more: the first spawn 28 to 48 s into the second pull.
        var dungeon = Dungeon("pull.1=/duration=100\npull.1+=/count=3,name=Trash Mob,hit_points=1000000\n" +
                              "pull.2=/duration=100,downtime=200\npull.2+=/count=3,name=Trash Mob,hit_points=1000000");
        var orbs = Fight(dungeon, Attacks).Deaths.Where(t => t.Name == AnomalousOrbs.OrbName).ToList();
        Assert.Equal(3, orbs.Count);
        Assert.All(orbs, o => Assert.InRange(o.DiesAt.TotalSeconds - AnomalousOrbs.RuleOrbSeconds, 300 + 28, 300 + 48 + 3));
    }

    [Fact]
    public void Storm_Shield_Gives_Storms_Fury_On_Its_Timer()
    {
        var dungeon = Dungeon("pull.1=/duration=300\npull.1+=/count=1,name=Trash Mob,hit_points=1000000", "storm_shield");
        var log = Fight(dungeon, "actions=attack");
        var fury = log.Auras.Where(a => a.Fury > 0).Select(a => a.Time).ToList();
        Assert.NotEmpty(fury);
        // The first charge 59 to 72 s in, the bolt 8 s after it.
        Assert.InRange(fury[0], 59 + 8, 72 + 8 + 2);
        Assert.All(log.Auras, a => Assert.InRange(a.Fury, 0, 1));
        // Storm's Fury adds 20% crit chance.
        var on = log.Auras.First(a => a.Fury > 0);
        var off = log.Auras.Last(a => a.Fury == 0 && a.Time < on.Time);
        Assert.Equal(off.CritChance + StormShield.StormsFuryCritChance, on.CritChance, 6);
        // It lasts 20 s from the bolt, and the next bolt refreshes it: on for at most 20 s past each bolt.
        var dungeonWithout = DungeonProfile.Parse("dungeon=Test\ndifficulty=0\ntime_limit=600\npull.1=/duration=300\npull.1+=/count=1,name=Trash Mob,hit_points=1000000");
        Assert.All(Fight(dungeonWithout, "actions=attack").Auras, a => Assert.Equal(0, a.Fury));
    }

    [Fact]
    public void A_New_Storms_Fury_Refreshes_The_One_Stack()
    {
        var hero = HeroImporter.Import(Build("Gunde"));
        var ctx = new SimulationContext(hero);
        hero.BeginFight(ctx);
        hero.Auras.Apply(StormShield.StormsFury, SimTime.FromSeconds(0));
        Assert.Equal(20, hero.Auras.Get(StormShield.StormsFury, SimTime.FromSeconds(0))!.ExpiresAt.TotalSeconds, 0.05);
        hero.Auras.Apply(StormShield.StormsFury, SimTime.FromSeconds(12));
        var fury = hero.Auras.Get(StormShield.StormsFury, SimTime.FromSeconds(12))!;
        Assert.Equal(1, fury.Stacks);
        Assert.Equal(32, fury.ExpiresAt.TotalSeconds, 0.05);
    }

    [Fact]
    public void Exsanguinate_Takes_None_Of_An_Orbs_Four_Times()
    {
        // Exsanguinate's preset is Empty: on an orb with Anomalous Instability on, its hit is its damage, as on any enemy, not 4 times it.
        var exsanguinated = new List<(double Damage, EnemyTarget Target)>();
        var hits = new List<(double Amount, EnemyTarget Target, double Incoming)>();
        var simulator = new MonteCarloSimulator(() =>
        {
            var h = HeroImporter.Import(Build("Gunde"));
            h.GetAbility<FellowshipSim.Core.Heroes.Gunde.Abilities.HeartSplitter>()!.Exsanguinated += (_, target, damage, _) => exsanguinated.Add((damage, target));
            h.OnDamageDealt += (in DamageContext hit) =>
            {
                if (hit.AbilityId == LogIds.Effect(FellowshipSim.Core.Heroes.Gunde.Abilities.HeartSplitter.ExsanguinateEffectId) && hit.Target is { } t)
                    hits.Add((hit.Amount, t, t.Auras.Fold(StatType.IncomingDamageMultiplier, 1.0, hit.Timestamp)));
            };
            return h;
        }, AplParser.ParseScript("actions=switch_target,target_if=target.is_priority\nactions+=/heart_splitter\nactions+=/grim_carve\nactions+=/blood_arc\nactions+=/attack"));
        simulator.Run(SimTime.FromSeconds(600), iterations: 1, firstSeed: 1, degreeOfParallelism: 1,
            fight: new FightOptions(Dungeon: Dungeon(OrbPull(dies: 50))));
        var onOrb = hits.Where(h => h.Target.Name == AnomalousOrbs.OrbName && h.Incoming == 4.0).ToList();
        Assert.NotEmpty(onOrb);
        // The hit is its damage within the hit's spread, never 4 times it.
        Assert.All(onOrb, h => Assert.Contains(exsanguinated, e => ReferenceEquals(e.Target, h.Target) && h.Amount / e.Damage is > 0.8 and < 1.25));
    }

    [Fact]
    public void A_Charge_Due_In_A_Boss_Fight_Waits_For_The_Bosss_Window_And_A_Bosss_End_Drops_It()
    {
        // Logs: the boss window's wait, 12.4 s (Storm Shield charge gaps in boss fights).
        const double BossWindowWait = 12.4;
        // A boss from the start: the first charge 59 to 72 s in, then the window's wait, then the bolt.
        var boss = Dungeon("pull.1=/duration=300,final=1\npull.1+=/boss.1,name=Boss,hit_points=1000000", "storm_shield");
        double first = Fight(boss, "actions=attack").Auras.First(a => a.Fury > 0).Time;
        Assert.InRange(first, 59 + BossWindowWait + 8, 72 + BossWindowWait + 8 + 2);

        // A boss dying 70 s in, then trash: a charge due in the boss fight waits past the boss's end, which drops it: the next comes 59 to 72 s after the end.
        var ends = Dungeon("pull.1=/duration=70\npull.1+=/boss.1,name=Boss,hit_points=1000000\n" +
                           "pull.2=/duration=300,chain_with=1\npull.2+=/count=1,name=Trash Mob,hit_points=1000000", "storm_shield");
        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            double next = Fight(ends, "actions=attack", seed: seed).Auras.First(a => a.Fury > 0).Time;
            Assert.True(next is >= 70 + 8 and <= 72 + 8 + 1 or >= 70 + 59 + 8 and <= 70 + 72 + 8 + 1, $"Storm's Fury at {next} s");
        }
    }

    /// <summary>
    /// <paramref name="iterations"/> fights of <paramref name="dungeon"/> on one hero: each cast's fight, ability, cooldown
    /// reduction and the cooldown it started, and the hero.
    /// </summary>
    private static (List<(int Fight, string Ability, double Reduction, double Cooldown)> Casts, Hero Hero) Casts(DungeonProfile dungeon, int iterations)
    {
        var casts = new List<(int, string, double, double)>();
        Hero? made = null;
        var simulator = new MonteCarloSimulator(() =>
        {
            var h = HeroImporter.Import(Build("Gunde"));
            made = h;
            int fight = 0;
            double last = 0;
            h.OnCastCompleted += (ability, _, now) =>
            {
                if (now.TotalSeconds < last) fight++;
                last = now.TotalSeconds;
                if (ability.Cooldown <= SimTime.Zero || ability.CooldownScalesWithHaste) return;
                casts.Add((fight, ability.Name, h.Stats.CooldownReductionPct, (h.CooldownOf(ability).NextChargeAt - now).TotalSeconds));
            };
            return h;
        }, AplParser.ParseScript("actions=heart_splitter\nactions+=/grim_carve\nactions+=/blood_arc\nactions+=/attack"));
        simulator.Run(SimTime.FromSeconds(dungeon.TimeLimitSeconds), iterations: iterations, firstSeed: 1, degreeOfParallelism: 1,
            fight: new FightOptions(Dungeon: dungeon));
        return (casts, made!);
    }

    [Fact]
    public void Ultimatum_Adds_Its_Cooldown_Reduction_In_Each_Fight_And_Takes_It_Away_After()
    {
        Assert.Equal(16, DungeonAffix.ByKey("ultimatum")!.Id);
        Assert.Equal(["ultimatum"], Dungeon("pull.1=/duration=30,count=1,name=Imp,hit_points=900", "ultimatum").Affixes);
        const string Pull = "pull.1=/duration=60\npull.1+=/count=1,name=Trash Mob,hit_points=1000000";
        double baseline = HeroImporter.Import(Build("Gunde")).Stats.CooldownReductionPct;

        var (plain, _) = Casts(DungeonProfile.Parse($"dungeon=Test\ndifficulty=0\ntime_limit=600\n{Pull}"), 1);
        var (casts, hero) = Casts(Dungeon(Pull, "ultimatum"), 2);
        Assert.Equal(baseline, hero.Stats.CooldownReductionPct);
        Assert.Equal([0, 1], casts.Select(c => c.Fight).Distinct());
        Assert.All(casts, c => Assert.Equal(baseline + 0.10, c.Reduction));
        Dictionary<string, double> First(IEnumerable<(int Fight, string Ability, double Reduction, double Cooldown)> fight) =>
            fight.GroupBy(c => c.Ability).ToDictionary(g => g.Key, g => g.First().Cooldown);
        var without = First(plain);
        foreach (int fight in new[] { 0, 1 })
        {
            var with = First(casts.Where(c => c.Fight == fight));
            Assert.NotEmpty(with);
            Assert.All(with, c => Assert.True(c.Value < without[c.Key], $"{c.Key}: {c.Value} s with Ultimatum, {without[c.Key]} s without"));
        }
    }

    [Fact]
    public void A_Replay_Of_An_Ultimatum_Run_Has_Its_Cooldown_Reduction_Once()
    {
        var character = Build("Gunde");
        var script = new ReplayScript
        {
            Schema = ReplayScript.SchemaVersion,
            ReportCode = "test",
            FightId = 1,
            FightName = "Test",
            DurationSeconds = 10,
            DungeonModifierIds = [16],
            Player = new ReplayPlayer { Id = 1, Name = "Tester", Hero = character.Hero },
            Character = character,
            Commands = [new ReplayCommand { Time = 0.5, Kind = CommandKind.EnemyArise, Enemy = new UnitRef(100, 1), Name = "Dummy", MaxHp = 1e9, Hp = 1e9 }],
        };
        double baseline = HeroImporter.Import(character).Stats.CooldownReductionPct;
        var hero = ReplayDriver.CreateHero(script);
        Assert.Equal(baseline + 0.10, hero.Stats.CooldownReductionPct);
        new ReplayDriver(hero, script, new ReplayOptions { StopOnFirstFailure = false }).Run();
        Assert.Equal(baseline + 0.10, hero.Stats.CooldownReductionPct);
    }
}
