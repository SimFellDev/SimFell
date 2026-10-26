using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// One Tariq ability on its own: its damage as a share of his Strength, area falloff, what Thunder Call adds to it, Hammer Storm's spins, Chain
/// Lightning's bounces, Culling Strike's Execution and Fury scaling, Leap Smash, Focused Wrath and Raging Tempest.
/// Dev: damage is the expected value, so two hits' ratio is exact. The tooltips give each ability's damage as a percentage of Strength, so a hit is
/// compared with a Wild Swing hit on one enemy (129.14% of Strength) rather than with an absolute number. No haste unless a test is about it.
/// Hits with a crit chance of their own (Leap Smash) are divided by what that chance is worth in expected value before they are compared.
/// </summary>
public class TariqAbilitiesSimple
{
    private const double Ratio = 0.001;

    private static readonly double Strength = TariqFights.StrengthUnit();
    private static readonly double CritMultiplier = TariqFights.CritMultiplier();

    private static Ability Named(StepDriver f, string name) => f.Hero.Abilities.First(a => a.Name == name);

    /// <summary>A fight with what most abilities need: 100 Fury, and an enemy in execute range for Culling Strike.</summary>
    private static StepDriver Fight(double haste = 0, int enemies = 1, int[]? talents = null, double crit = 0)
    {
        var f = StepFights.Tariq(haste, enemies, talents, crit: crit);
        f.SetFury(100);
        f.SetHealth(0.2);
        return f;
    }

    // ---- Damage of each ability

    /// <summary>
    /// Player: each ability deals the share of your Strength its tooltip gives: Wild Swing 129.14%, Face Breaker 618.43%, Heavy Strike 566%, Skull
    /// Crusher 1160%, Hammer Storm 223% a spin, Culling Strike 318.2% with no Fury spent, Chain Lightning 149.98% a hit, Leap Smash 69%.
    /// Dev: one cast of each at no Thunder Call, with no Fury for Culling Strike; the first hit over a Wild Swing hit, against the coefficients' ratio
    /// (Leap Smash's hit divided by what its own 40% crit chance is worth).
    /// </summary>
    [Theory]
    [InlineData("Wild Swing", 1.2914)]
    [InlineData("Face Breaker", 6.18426)]
    [InlineData("Heavy Strike", 5.66)]
    [InlineData("Skull Crusher", 11.6)]
    [InlineData("Hammer Storm", 2.23)]
    [InlineData("Culling Strike", 3.182025)]
    public void Each_Ability_Deals_Its_Share_Of_Strength(string ability, double coefficient)
    {
        var f = Fight();
        if (ability == "Culling Strike") f.SetFury(0);

        f.Cast(Named(f, ability));

        Assert.Equal(coefficient, f.FirstDamageOf(ability) / Strength, coefficient * Ratio);
    }

    /// <summary>
    /// Player: Chain Lightning hits for 149.98% of Strength.
    /// Dev: Thunder Call, one Chain Lightning on one enemy; the first hit over a Wild Swing hit.
    /// </summary>
    [Fact]
    public void Chain_Lightning_Deals_149_98_Percent_Of_Strength_Per_Hit()
    {
        var f = Fight();
        f.Cast<ThunderCall>();

        f.Cast<ChainLightning>();

        Assert.Equal(1.4998, f.FirstDamageOf("Chain Lightning") / Strength, 1.4998 * Ratio);
    }

    /// <summary>
    /// Player: Leap Smash deals 69% of Strength, and has 40% more critical strike chance than your own.
    /// Dev: one Leap Smash with 0% crit; its hit chance to crit reads 0.4, and the hit (without that chance's worth) over a Wild Swing hit is 0.69 / 1.2914.
    /// </summary>
    [Fact]
    public void Leap_Smash_Deals_69_Percent_Of_Strength_With_40_Percent_Crit_Chance_Of_Its_Own()
    {
        var f = Fight();

        f.Cast<LeapSmash>();
        f.Wait(0.5);

        var hit = f.Damage("Leap Smash").Single();
        Assert.Equal(0.4, hit.CritChance, 1e-9);
        Assert.Equal(0.69, hit.WithoutCrit(CritMultiplier) / Strength, 0.69 * Ratio);
    }

    // ---- Wild Swing

    /// <summary>
    /// Player: Wild Swing cannot critically strike, and its damage is increased by your critical strike chance instead.
    /// Dev: 30% crit; one Wild Swing; the hit is 1.3 times a hit with no crit chance (a hit that could crit would be worth more).
    /// </summary>
    [Fact]
    public void Wild_Swing_Cannot_Crit_And_Is_Increased_By_Crit_Chance()
    {
        double noCrit = StepFights.Tariq().Do(f => f.Cast<WildSwing>()).FirstDamageOf("Wild Swing");
        var f = StepFights.Tariq(crit: 0.3);

        f.Cast<WildSwing>();

        Assert.Equal(1.3, f.FirstDamageOf("Wild Swing") / noCrit, Ratio);
    }

    /// <summary>
    /// Player: Wild Swing hits the target and every enemy near it, each for the same damage.
    /// Dev: 3 enemies; one Wild Swing; 3 hits, one on each, equal.
    /// </summary>
    [Fact]
    public void Wild_Swing_Hits_Every_Enemy_Near()
    {
        var f = StepFights.Tariq(enemies: 3);

        f.Cast<WildSwing>();

        var hits = f.Damage("Wild Swing");
        Assert.Equal(3, hits.Count);
        Assert.Equal(3, hits.Select(h => h.Target).Distinct().Count());
        Assert.All(hits, h => Assert.Equal(hits[0].Value, h.Value, 1e-6));
    }

    /// <summary>
    /// Player: beyond 5 enemies each Wild Swing hit is weaker: multiplied by sqrt(5 / enemies).
    /// Dev: 5 enemies and 10 enemies; one cast; the first hit of each, in the ratio of the square roots.
    /// TODO: whether the game counts the target in that number (the engine counts every enemy hit). Check in game.
    /// </summary>
    [Fact]
    public void Wild_Swing_Falls_Off_Beyond_5_Enemies()
    {
        double Hit(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies);
            f.Cast<WildSwing>();
            return f.Damage("Wild Swing")[0].Value;
        }

        Assert.Equal(Math.Sqrt(5.0 / 10), Hit(10) / Hit(5), Ratio);
    }

    /// <summary>
    /// Player: enemies have an increased chance to avoid Wild Swing (a miss or a dodge): an avoided hit deals no damage, and you still gain the Fury.
    /// Dev: the avoid roll forced on; 2 enemies; one Wild Swing; 2 hits of 0 damage, and 3 Fury.
    /// </summary>
    [Fact]
    public void An_Avoided_Wild_Swing_Deals_No_Damage_But_Still_Gives_Fury()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? true : null);

        f.Cast<WildSwing>();

        Assert.All(f.Damage("Wild Swing"), h => Assert.Equal(0.0, h.Value));
        Assert.Equal(3, f.Fury());
    }

    /// <summary>
    /// Player: Wild Swing is avoided about 7% of the time (3-10% a fight in the logs), each enemy rolling on its own.
    /// Dev: the chance the engine gives the avoid roll, for 2 enemies; two rolls of 0.07.
    /// TODO: the engine's 7% is an estimate from three logs (misses and dodges together); the game's number is not known. Check in game.
    /// </summary>
    [Fact(Skip = "Wild Swing's avoid chance is an estimate from combat logs: to check in game")]
    public void Wild_Swing_Is_Avoided_7_Percent_Of_The_Time_Per_Enemy()
    {
        var f = StepFights.Tariq(enemies: 2);
        var procs = f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null);

        f.Cast<WildSwing>();

        var rolls = procs.Rolls.Where(r => r.Key == WildSwing.AvoidedProcKey).ToList();
        Assert.Equal(2, rolls.Count);
        Assert.All(rolls, r => Assert.Equal(0.07, r.Chance, 1e-9));
    }

    // ---- Face Breaker

    /// <summary>
    /// Player: Face Breaker deals 50% of its damage to the enemies near your target, and that splash cannot crit; a crit on the target carries over
    /// to it.
    /// Dev: 3 enemies and 50% crit; one Face Breaker; the target's hit has a crit chance of 0.5, each other enemy takes exactly half of it, with no
    /// crit chance.
    /// </summary>
    [Fact]
    public void Face_Breaker_Deals_Half_Its_Damage_To_Enemies_Near_The_Target_Without_Crit_Of_Its_Own()
    {
        var f = StepFights.Tariq(enemies: 3, crit: 0.5);

        f.Cast<FaceBreaker>();

        var hits = f.Damage("Face Breaker");
        Assert.Equal(3, hits.Count);
        var target = hits.Single(h => h.Target == f.Enemy.Name);
        var splashes = hits.Where(h => h.Target != f.Enemy.Name).ToList();
        Assert.Equal(2, splashes.Count);
        Assert.All(splashes, s =>
        {
            Assert.Equal(0.5, s.Value / target.Value, Ratio);
            Assert.Equal(0.0, s.CritChance, 1e-9);
        });
    }

    /// <summary>
    /// Player: beyond 12 enemies near the target, each Face Breaker splash is weaker: multiplied by sqrt(12 / enemies).
    /// Dev: 13 and 17 enemies (12 and 16 near the target); one cast; a splash of each, in the ratio of the square roots.
    /// TODO: whether the game counts the target in that number (the engine counts the enemies the splash hits). Check in game.
    /// </summary>
    [Fact]
    public void Face_Breaker_Splash_Falls_Off_Beyond_12_Enemies()
    {
        double Splash(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies);
            f.Cast<FaceBreaker>();
            return f.Damage("Face Breaker").First(h => h.Target != f.Enemy.Name).Value;
        }

        Assert.Equal(Math.Sqrt(12.0 / 16), Splash(17) / Splash(13), Ratio);
    }

    // ---- Heavy Strike

    /// <summary>
    /// Player: Heavy Strike also deals 169.8% of Strength to the enemies near your target.
    /// Dev: 3 enemies; one Heavy Strike; the other two enemies are hit (logged as another effect) for 1.698 / 5.66 of the target's hit; the target is
    /// hit once.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Cleaves_For_169_8_Percent_Of_Strength()
    {
        var f = StepFights.Tariq(enemies: 3);

        f.Cast<HeavyStrike>();

        var target = f.DamageOfEffect(1776);
        var cleave = f.DamageOfEffect(2755);
        Assert.Single(target);
        Assert.Equal(f.Enemy.Name, target[0].Target);
        Assert.Equal(2, cleave.Count);
        Assert.DoesNotContain(cleave, h => h.Target == f.Enemy.Name);
        Assert.All(cleave, h => Assert.Equal(1.698 / 5.66, h.Value / target[0].Value, Ratio));
    }

    /// <summary>
    /// Player: beyond 8 enemies near the target, each Heavy Strike cleave hit is weaker: multiplied by sqrt(8 / enemies).
    /// Dev: 9 and 13 enemies (8 and 12 near the target); one cast; a cleave hit of each, in the ratio of the square roots.
    /// TODO: whether the game counts the target in that number (the engine counts the enemies the cleave hits). Check in game.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Cleave_Falls_Off_Beyond_8_Enemies()
    {
        double Cleave(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies);
            f.Cast<HeavyStrike>();
            return f.DamageOfEffect(2755)[0].Value;
        }

        Assert.Equal(Math.Sqrt(8.0 / 12), Cleave(13) / Cleave(9), Ratio);
    }

    // ---- Skull Crusher

    /// <summary>
    /// Player: Skull Crusher hits its target alone.
    /// Dev: 3 enemies; one Skull Crusher; one hit, on the target.
    /// </summary>
    [Fact]
    public void Skull_Crusher_Hits_Only_Its_Target()
    {
        var f = Fight(enemies: 3);

        f.Cast<SkullCrusher>();

        var hits = f.Damage("Skull Crusher");
        Assert.Single(hits);
        Assert.Equal(f.Enemy.Name, hits[0].Target);
    }

    // ---- Hammer Storm

    /// <summary>
    /// Player: Hammer Storm spins 3 times, dealing 35% more damage with each spin: 1, 1.35 and 1.8225 times the first.
    /// Dev: 100 Fury; one cast on one enemy; 3 hits, in that ratio (each spin multiplies the one before).
    /// </summary>
    [Fact]
    public void Hammer_Storm_Spins_3_Times_Each_35_Percent_Stronger_Than_The_One_Before()
    {
        var f = Fight();

        f.Cast<HammerStorm>();
        f.Wait(1.5);

        var spins = f.Damage("Hammer Storm");
        Assert.Equal(3, spins.Count);
        Assert.Equal(1.35, spins[1].Value / spins[0].Value, Ratio);
        Assert.Equal(1.35 * 1.35, spins[2].Value / spins[0].Value, Ratio);
    }

    /// <summary>
    /// Player: Hammer Storm spins every 0.5 seconds, shortened by haste, the first one with the cast.
    /// Dev: at 0% and 50% haste; the spin times read from the damage events, from the cast: 0, 0.5 / (1 + haste), 1 / (1 + haste), each within one
    /// server frame (50ms) after (1ms before, for rounding).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Hammer_Storm_Spins_Every_Half_Second_Over_1_Plus_Haste(double haste)
    {
        var f = Fight(haste);

        f.Cast<HammerStorm>();
        f.Wait(1.5);

        var spins = f.Damage("Hammer Storm");
        for (int i = 0; i < 3; i++)
        {
            double expected = i * 0.5 / (1 + haste);
            Assert.InRange(spins[i].Time.TotalSeconds, expected - 0.001, expected + 0.05);
        }
    }

    /// <summary>
    /// Player: every spin of Hammer Storm hits every enemy near you.
    /// Dev: 4 enemies; one cast; 12 hits: each spin, each enemy.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Hits_Every_Enemy_On_Each_Spin()
    {
        var f = Fight(enemies: 4);

        f.Cast<HammerStorm>();
        f.Wait(1.5);

        var hits = f.Damage("Hammer Storm");
        Assert.Equal(12, hits.Count);
        Assert.All(hits.Chunk(4), spin => Assert.Equal(4, spin.Select(h => h.Target).Distinct().Count()));
    }

    /// <summary>
    /// Player: beyond 12 enemies each Hammer Storm hit is weaker: multiplied by sqrt(12 / enemies).
    /// Dev: 12 and 16 enemies; one cast; the first spin's first hit of each, in the ratio of the square roots.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Falls_Off_Beyond_12_Enemies()
    {
        double Spin(int enemies)
        {
            var f = Fight(enemies: enemies);
            f.Cast<HammerStorm>();
            return f.Damage("Hammer Storm")[0].Value;
        }

        Assert.Equal(Math.Sqrt(12.0 / 16), Spin(16) / Spin(12), Ratio);
    }

    // ---- Culling Strike

    /// <summary>
    /// Player: Culling Strike can only be used on an enemy at 30% health or less.
    /// Dev: an enemy at 31% health: refused; at 29%: allowed.
    /// </summary>
    [Fact]
    public void Culling_Strike_Needs_An_Enemy_At_30_Percent_Health_Or_Less()
    {
        var f = StepFights.Tariq();

        f.SetHealth(0.31);
        Assert.False(f.TryCast<CullingStrike>().Succeeded);

        f.SetHealth(0.29);
        Assert.True(f.TryCast<CullingStrike>().Succeeded);
    }

    /// <summary>
    /// Player: Culling Strike deals 40% more damage for each Fury it spends, up to 10 Fury (+400%), and no more beyond.
    /// Dev: an enemy at 20% health; one cast at 0, 5, 10 and 50 Fury; the hits over the 0-Fury hit are 1, 3, 5 and 5.
    /// </summary>
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(5, 3.0)]
    [InlineData(10, 5.0)]
    [InlineData(50, 5.0)]
    public void Culling_Strike_Deals_40_Percent_More_Per_Fury_Spent_Up_To_10(double fury, double times)
    {
        double Hit(double furyGiven)
        {
            var f = StepFights.Tariq();
            f.SetHealth(0.2);
            f.SetFury(furyGiven);
            f.Cast<CullingStrike>();
            return f.FirstDamageOf("Culling Strike");
        }

        Assert.Equal(times, Hit(fury) / Hit(0), Ratio);
    }

    // ---- Chain Lightning

    /// <summary>
    /// Player: Chain Lightning can only be cast while Thunder Call is active.
    /// Dev: refused at the start of a fight, allowed after a Thunder Call.
    /// </summary>
    [Fact]
    public void Chain_Lightning_Needs_Thunder_Call()
    {
        var f = StepFights.Tariq();

        Assert.False(f.TryCast<ChainLightning>().Succeeded);

        f.Cast<ThunderCall>();
        Assert.True(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// Player: Chain Lightning hits 6 times, the first 0.3 seconds after the cast and one every 0.05 seconds after that.
    /// Dev: Thunder Call; one Chain Lightning; 6 hits, at 0.30, 0.35, ... 0.55 from the cast, each within 20ms (the logs round them to server frames).
    /// </summary>
    [Fact]
    public void Chain_Lightning_Hits_6_Times_Every_0_05_Seconds_From_0_3()
    {
        var f = StepFights.Tariq();
        f.Cast<ThunderCall>();
        var start = f.Now.TotalSeconds;

        f.Cast<ChainLightning>();

        var hits = f.Damage("Chain Lightning");
        Assert.Equal(6, hits.Count);
        for (int i = 0; i < 6; i++) Assert.Equal(start + 0.30 + 0.05 * i, hits[i].Time.TotalSeconds, 0.02);
    }

    /// <summary>
    /// Player: Chain Lightning's arc hits your target first, then jumps to enemies it has not hit yet; with a single enemy it hits it every time.
    /// Dev: Thunder Call; one cast on 3 enemies: the first hit is on the target and the first three hits are on three different enemies; one cast on
    /// 1 enemy: 6 hits on it.
    /// </summary>
    [Fact]
    public void Chain_Lightning_Hits_The_Target_First_Then_Enemies_It_Has_Not_Hit()
    {
        var f = StepFights.Tariq(enemies: 3);
        f.Cast<ThunderCall>();
        f.Cast<ChainLightning>();

        var hits = f.Damage("Chain Lightning");
        Assert.Equal(f.Enemy.Name, hits[0].Target);
        Assert.Equal(3, hits.Take(3).Select(h => h.Target).Distinct().Count());

        var single = StepFights.Tariq();
        single.Cast<ThunderCall>();
        single.Cast<ChainLightning>();
        Assert.All(single.Damage("Chain Lightning"), h => Assert.Equal(single.Enemy.Name, h.Target));
    }

    // ---- Leap Smash

    /// <summary>
    /// Player: Leap Smash hits every enemy near the target, a moment after you leap: about 0.3 seconds.
    /// Dev: 3 enemies; one Leap Smash; 3 hits, one on each, 0.27 to 0.30s after the cast; no time passes for the cast.
    /// </summary>
    [Fact]
    public void Leap_Smash_Hits_Every_Enemy_0_3_Seconds_After_The_Cast()
    {
        var f = StepFights.Tariq(enemies: 3);

        f.Cast<LeapSmash>();
        Assert.Empty(f.Damage("Leap Smash"));
        f.Wait(0.5);

        var hits = f.Damage("Leap Smash");
        Assert.Equal(3, hits.Select(h => h.Target).Distinct().Count());
        Assert.All(hits, h => Assert.InRange(h.Time.TotalSeconds, 0.27, 0.30 + 0.001));
    }

    /// <summary>
    /// Player: beyond 8 enemies each Leap Smash hit is weaker: multiplied by sqrt(8 / enemies).
    /// Dev: 8 and 12 enemies; one cast; a hit of each, in the ratio of the square roots.
    /// </summary>
    [Fact]
    public void Leap_Smash_Falls_Off_Beyond_8_Enemies()
    {
        double Hit(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies);
            f.Cast<LeapSmash>();
            f.Wait(0.5);
            return f.Damage("Leap Smash")[0].Value;
        }

        Assert.Equal(Math.Sqrt(8.0 / 12), Hit(12) / Hit(8), Ratio);
    }

    // ---- Thunder Call

    /// <summary>
    /// Player: Thunder Call enhances your attacks with lightning for 21 seconds.
    /// Dev: Thunder Call at the start; the aura is up at 20.9s and gone at 21.1s.
    /// </summary>
    [Fact]
    public void Thunder_Call_Lasts_21_Seconds()
    {
        var f = StepFights.Tariq();
        f.Cast<ThunderCall>();

        f.Wait(20.9);
        Assert.True(f.TariqOf().IsCharged(f.Now));

        f.Wait(0.2);
        Assert.False(f.TariqOf().IsCharged(f.Now));
    }

    /// <summary>
    /// Player: with Thunder Call, Skull Crusher also beckons a thunder bolt that deals 406% of Strength to the target.
    /// Dev: Thunder Call; one Skull Crusher; a hit named after Thunder Call, on the target, 4.06 / 11.6 of the physical hit. Without Thunder Call
    /// there is no such hit.
    /// </summary>
    [Fact]
    public void Skull_Crusher_Adds_A_Thunder_Bolt_With_Thunder_Call()
    {
        var without = Fight();
        without.Cast<SkullCrusher>();
        Assert.Empty(without.Damage("Thunder Call: Skull Crusher"));

        var f = Fight();
        f.Cast<ThunderCall>();
        f.Cast<SkullCrusher>();

        var bolt = f.Damage("Thunder Call: Skull Crusher").Single();
        Assert.Equal(f.Enemy.Name, bolt.Target);
        Assert.Equal(4.06 / 11.6, bolt.Value / f.FirstDamageOf("Skull Crusher"), Ratio);
    }

    /// <summary>
    /// Player: with Thunder Call, Heavy Strike also charges the ground with lightning, dealing 198% of Strength to the target and to every enemy near it.
    /// Dev: 3 enemies; Thunder Call; one Heavy Strike; 3 lightning hits, one on each enemy, 1.98 / 5.66 of the target's physical hit. Without Thunder Call
    /// there are none.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Adds_Lightning_On_Every_Enemy_With_Thunder_Call()
    {
        var without = StepFights.Tariq(enemies: 3);
        without.Cast<HeavyStrike>();
        Assert.Empty(without.Damage("Thunder Call: Heavy Strike"));

        var f = StepFights.Tariq(enemies: 3);
        f.Cast<ThunderCall>();
        f.Cast<HeavyStrike>();

        var bolts = f.Damage("Thunder Call: Heavy Strike");
        Assert.Equal(3, bolts.Select(h => h.Target).Distinct().Count());
        Assert.All(bolts, b => Assert.Equal(1.98 / 5.66, b.Value / f.DamageOfEffect(1776)[0].Value, Ratio));
    }

    /// <summary>
    /// Player: with Thunder Call, each spin of Hammer Storm shocks every enemy hit for 78% of Strength (against 223% for the spin), growing with the
    /// spin as the physical hit does.
    /// Dev: Thunder Call; one Hammer Storm on one enemy; 3 lightning hits, each 0.78 / 2.23 of the physical spin of the same moment. Without
    /// Thunder Call there are none.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Adds_Lightning_On_Each_Spin_With_Thunder_Call()
    {
        var without = Fight();
        without.Cast<HammerStorm>();
        without.Wait(1.5);
        Assert.Empty(without.Damage("Thunder Call: Hammer Storm"));

        var f = Fight();
        f.Cast<ThunderCall>();
        f.Cast<HammerStorm>();
        f.Wait(1.5);

        var spins = f.Damage("Hammer Storm");
        var bolts = f.Damage("Thunder Call: Hammer Storm");
        Assert.Equal(3, bolts.Count);
        for (int i = 0; i < 3; i++) Assert.Equal(0.78 / 2.23, bolts[i].Value / spins[i].Value, Ratio);
    }

    // ---- Focused Wrath

    /// <summary>
    /// Player: Focused Wrath makes your next two Skull Crusher or Hammer Storm casts deal 10% more damage, and the third does not.
    /// Dev: Focused Wrath; 3 Skull Crushers; the first two are 1.1 times a Skull Crusher with no Focused Wrath, the third is not.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Adds_10_Percent_Damage_To_The_Next_Two_Casts()
    {
        double plain = StepFights.Tariq().Do(p => { p.SetFury(100); p.Cast<SkullCrusher>(); }).FirstDamageOf("Skull Crusher");
        var f = StepFights.Tariq();
        f.SetFury(100);
        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();

        var hits = f.Damage("Skull Crusher");
        Assert.Equal(1.1, hits[0].Value / plain, Ratio);
        Assert.Equal(1.1, hits[1].Value / plain, Ratio);
        Assert.Equal(1.0, hits[2].Value / plain, Ratio);
    }

    // ---- Raging Tempest

    /// <summary>
    /// Player: Raging Tempest costs 100 Spirit Points; you cannot cast it with fewer.
    /// Dev: 99 Spirit Points: refused; 100: allowed, and they are spent.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Needs_And_Spends_100_Spirit_Points()
    {
        var f = StepFights.Tariq();
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 99;
        Assert.False(f.TryCast<RagingTempest>().Succeeded);

        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();

        Assert.Equal(0, f.Hero.ResourceAmount(Hero.SpiritPoints));
    }

    /// <summary>
    /// Player: Raging Tempest leaps to the target area and deals 1006% of Strength of lightning damage to the enemies near it, a moment later (0.3
    /// to 0.46 seconds); beyond 5 enemies each hit is weaker: multiplied by sqrt(5 / enemies).
    /// Dev: one cast on 1 enemy: the hit comes after 0.3s and is 10.06 times a Wild Swing's 1.2914 (the 1% Expertise of the stack the cast gives taken
    /// out); one cast on 5 and 10 enemies: the first hit of each in the ratio of sqrt(5 / 10).
    /// </summary>
    [Fact]
    public void Raging_Tempest_Leaps_For_1006_Percent_Of_Strength_And_Falls_Off_Beyond_5_Enemies()
    {
        StepEvent Leap(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies);
            f.GiveTariqSpirit();
            f.Cast<RagingTempest>();
            f.Wait(0.5);
            return f.Damage("Raging Tempest").First();
        }

        var one = Leap(1);
        Assert.InRange(one.Time.TotalSeconds, 0.3, 0.46);
        Assert.Equal(10.06, one.Value / 1.01 / Strength, 10.06 * Ratio);
        Assert.Equal(Math.Sqrt(5.0 / 10), Leap(10).Value / Leap(5).Value, Ratio);
    }

    /// <summary>
    /// Player: Raging Tempest gives Spirit of Heroism: +30% haste for 20 seconds.
    /// Dev: haste before, at the cast and at 19.9s and 20.1s.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Gives_30_Percent_Haste_For_20_Seconds()
    {
        var f = StepFights.Tariq(0.1);
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();
        Assert.Equal(0.4, f.Hero.GetHaste(f.Now), 1e-9);

        f.Wait(19.9);
        Assert.Equal(0.4, f.Hero.GetHaste(f.Now), 1e-9);

        f.Wait(0.2);
        Assert.Equal(0.1, f.Hero.GetHaste(f.Now), 1e-9);
    }

    /// <summary>
    /// Player: Raging Tempest gives an Enhanced Thunder Call for 20 seconds: your attacks have lightning and you can cast Chain Lightning, as with
    /// Thunder Call.
    /// Dev: Raging Tempest; Chain Lightning allowed; a Skull Crusher has a thunder bolt; 20.1 seconds later Chain Lightning is refused.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Charges_Your_Attacks_Like_Thunder_Call()
    {
        var f = Fight();
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();

        Assert.True(f.TryCast<ChainLightning>().Succeeded);
        f.Cast<SkullCrusher>();
        Assert.Single(f.Damage("Thunder Call: Skull Crusher"));

        f.Wait(20.1);
        Assert.False(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// Player: Raging Tempest removes Thunder Call, and Thunder Call cannot be cast while Raging Tempest lasts.
    /// Dev: Thunder Call, then Raging Tempest: Thunder Call's aura is gone (the hero is charged by the Tempest alone); a new Thunder Call is refused even
    /// though it is off cooldown; after 20.1 seconds it is allowed again.
    /// TODO: the engine's rule comes from an icy-veins guide and 45 logs where none was cast during a Tempest. Check in game.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Removes_Thunder_Call_And_Blocks_It()
    {
        var f = StepFights.Tariq();
        f.GiveTariqSpirit();
        f.Cast<ThunderCall>();
        f.Cast<RagingTempest>();

        Assert.False(f.Hero.Auras.HasAura(ThunderCall.Buff, f.Now));
        f.Hero.CooldownOf(f.Ability<ThunderCall>()).ResetAll();
        Assert.False(f.TryCast<ThunderCall>().Succeeded);

        f.Wait(20.1);
        Assert.True(f.TryCast<ThunderCall>().Succeeded);
    }

    /// <summary>
    /// Player: Raging Tempest's buff comes with the cast at 1 stack and lasts 20 seconds; each Raging Currents hit (a lightning strike at a random enemy
    /// every second, shortened by haste) adds a stack and does not extend it, up to 20 stacks; each stack is +1% Expertise.
    /// Dev: Raging Tempest at 0% haste (so 30% from Spirit of Heroism: a strike every 1 / 1.3s); 1 stack and 1% Expertise at the cast; at 5s there
    /// are 6 strikes: 7 stacks, 7% Expertise; at 19.9s the aura is still there and at 20.1s it is gone.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Stacks_With_Each_Raging_Currents_Hit_Without_Extending()
    {
        var f = StepFights.Tariq();
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();
        Assert.Equal(1, f.Stacks(RagingTempest.Buff));
        Assert.Equal(0.01, f.Hero.Expertise(f.Now), 1e-9);

        f.Wait(5.0);
        Assert.Equal(6, f.Damage("Raging Currents").Count);
        Assert.Equal(7, f.Stacks(RagingTempest.Buff));
        Assert.Equal(0.07, f.Hero.Expertise(f.Now), 1e-9);

        f.Wait(14.9);
        Assert.True(f.Stacks(RagingTempest.Buff) > 0);
        f.Wait(0.2);
        Assert.Equal(0, f.Stacks(RagingTempest.Buff));
    }

    /// <summary>
    /// Player: Raging Currents strikes for 154% of Strength of lightning damage.
    /// Dev: Raging Tempest; the first strike (with the 1% Expertise of the one stack there is, taken out) over a Wild Swing hit is 1.54 / 1.2914.
    /// </summary>
    [Fact]
    public void Raging_Currents_Strikes_For_154_Percent_Of_Strength()
    {
        var f = StepFights.Tariq();
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();

        f.Wait(1.0);

        Assert.Equal(1.54, f.FirstDamageOf("Raging Currents") / 1.01 / Strength, 1.54 * Ratio);
    }
}
