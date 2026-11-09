using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Tariq's abilities, on a bare Tariq: each ability's damage as a share of his main stat, area falloff, what Thunder Call adds to it,
/// Hammer Storm's spins, Chain Lightning's bounces, Culling Strike's execute range and Fury scaling, Leap Smash, Focused Wrath and Raging
/// Tempest. Fury gains and costs, cast times and cooldowns have their own files; what every ultimate shares (Raging Tempest's Spirit cost
/// and Spirit of Heroism) is in Generic/Ultimate.cs.
///
/// Damage has no spread and no crit, so two hits' ratio is exact. The tooltips give each ability's damage as a share of the main stat, so a
/// hit is compared with a Wild Swing hit on one enemy (129.14% of it) rather than with an absolute number. No haste unless a test is about
/// it. Hits with a crit chance of their own (Leap Smash) are forced not to crit.
/// </summary>
public class TariqAbilities
{
    /// <summary>The main stat, measured as a Wild Swing hit on a single enemy with nothing else on, which deals 129.14% of it.</summary>
    private static readonly double MainStat = MainStatFromWildSwing();

    private static double MainStatFromWildSwing()
    {
        var f = StepFights.Tariq().WithoutDamageSpread();
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // no Wild Swing is avoided
        f.Cast<WildSwing>();
        return f.Damage<WildSwing>().First().Value / 1.2914;
    }

    // ---- Damage of each ability

    /// <summary>
    /// Each ability deals the share of your main stat its tooltip gives: Wild Swing 129.14%, Face Breaker 618.43%, Heavy Strike 566%,
    /// Skull Crusher 1160%, Hammer Storm 223% a spin, Culling Strike 318.2% with no Fury spent.
    ///
    /// One cast of each with no Thunder Call, with no Fury for Culling Strike; the first hit over the main stat, within 0.1% of the
    /// coefficient.
    /// </summary>
    [Theory]
    [InlineData("Wild Swing", 1.2914)]
    [InlineData("Face Breaker", 6.18426)]
    [InlineData("Heavy Strike", 5.66)]
    [InlineData("Skull Crusher", 11.6)]
    [InlineData("Hammer Storm", 2.23)]
    [InlineData("Culling Strike", 3.182025)]
    public void Each_Ability_Deals_Its_Share_Of_The_Main_Stat(string ability, double coefficient)
    {
        var f = StepFights.Tariq().WithoutDamageSpread();
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // no Wild Swing is avoided
        f.Set(Tariq.Fury, ability == "Culling Strike" ? 0 : 100); // enough for the spenders, none for Culling Strike
        f.SetHealth(0.2); // in execute range, for Culling Strike

        f.Cast(ability);

        AssertRatio(coefficient, f.Damage(ability).First().Value / MainStat, coefficient * RatioTolerance);
    }

    /// <summary>
    /// Chain Lightning hits for 149.98% of your main stat.
    ///
    /// Thunder Call, then one Chain Lightning on one enemy; the first hit over the main stat, within 0.1% of the coefficient.
    /// </summary>
    [Fact]
    public void Chain_Lightning_Deals_149_98_Percent_Of_The_Main_Stat_Per_Hit()
    {
        var f = StepFights.Tariq().WithoutDamageSpread();
        f.Set(Tariq.Fury, 100);
        f.SetHealth(0.2);

        f.Cast<ThunderCall>(); // enables Chain Lightning

        f.Cast<ChainLightning>();

        AssertRatio(1.4998, f.Damage<ChainLightning>().First().Value / MainStat, 1.4998 * RatioTolerance);
    }

    /// <summary>
    /// Leap Smash deals 69% of your main stat, and has 40% more critical strike chance than your own.
    ///
    /// With 0% crit, one Leap Smash forced not to crit: its hit's chance to crit reads 0.4, and the hit over the main stat is 0.69 (within
    /// 0.1% of it).
    /// </summary>
    [Fact]
    public void Leap_Smash_Deals_69_Percent_Of_The_Main_Stat_With_40_Percent_Crit_Chance_Of_Its_Own()
    {
        var f = StepFights.Tariq().WithoutDamageSpread();
        f.Set(Tariq.Fury, 100);
        f.SetHealth(0.2);

        f.Cast<LeapSmash>(crit: false);
        f.Wait(0.5); // the leap lands
        var hit = f.Damage<LeapSmash>().Single();

        using var check = new Checks(f);
        check.Equal(0.4, hit.CritChance);
        check.Equal(0.69, hit.Value / MainStat, 0.69 * RatioTolerance);
    }

    // ---- Wild Swing

    /// <summary>
    /// Wild Swing cannot critically strike, and its damage is increased by your critical strike chance instead: with 30% crit, its hit is
    /// 1.3 times a hit with no crit chance (a hit that could crit would be worth more).
    /// </summary>
    [Fact]
    public void Wild_Swing_Cannot_Crit_And_Is_Increased_By_Crit_Chance()
    {
        var noCritScenario = StepFights.Tariq().WithoutDamageSpread();
        noCritScenario.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // no Wild Swing is avoided

        noCritScenario.Cast<WildSwing>();
        double noCritSwingDamage = noCritScenario.Damage<WildSwing>().First().Value;

        var critScenario = StepFights.Tariq(crit: 0.3).WithoutDamageSpread();
        critScenario.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null);

        critScenario.Cast<WildSwing>();
        double critSwingDamage = critScenario.Damage<WildSwing>().First().Value;

        AssertRatio(1.3, critSwingDamage / noCritSwingDamage);
    }

    /// <summary>Wild Swing hits the target and every enemy near it, each for the same damage: with 3 enemies, 3 equal hits, one on each.</summary>
    [Fact]
    public void Wild_Swing_Hits_Every_Enemy_Near()
    {
        var f = StepFights.Tariq(enemies: 3).WithoutDamageSpread();
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // no Wild Swing is avoided

        f.Cast<WildSwing>();
        var hits = f.Damage<WildSwing>();

        using var check = new Checks(f);
        check.Equal(3, hits.Count);
        check.Equal(3, hits.Select(h => h.Target).Distinct().Count());
        foreach (var hit in hits) check.Equal(hits[0].Value, hit.Value, 1e-6, what: "a hit's damage against the first's");
    }

    /// <summary>
    /// Beyond 5 enemies each Wild Swing hit is weaker: multiplied by sqrt(5 / enemies). The first hit at 10 enemies over the first hit at 5
    /// is sqrt(5 / 10).
    ///
    /// TODO: check in game whether the game counts the target in that number (the engine counts every enemy hit).
    /// </summary>
    [Fact]
    public void Wild_Swing_Falls_Off_Beyond_5_Enemies()
    {
        double FirstHit(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies).WithoutDamageSpread();
            f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // no Wild Swing is avoided
            f.Cast<WildSwing>();
            return f.Damage<WildSwing>()[0].Value;
        }

        AssertRatio(Math.Sqrt(5.0 / 10), FirstHit(10) / FirstHit(5));
    }

    /// <summary>
    /// Enemies have an increased chance to avoid Wild Swing (a miss or a dodge): an avoided hit deals no damage, and you still gain the
    /// Fury. With 2 enemies avoiding it, its hits deal 0 and you gain 3 Fury.
    /// </summary>
    [Fact]
    public void An_Avoided_Wild_Swing_Deals_No_Damage_But_Still_Gives_Fury()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? true : null); // every enemy avoids it

        f.Cast<WildSwing>();

        using var check = new Checks(f);
        foreach (var hit in f.Damage<WildSwing>()) check.Equal(0.0, hit.Value, what: "an avoided hit's damage");
        check.Equal(3, f.Amount(Tariq.Fury));
    }

    /// <summary>
    /// Wild Swing is avoided about 7% of the time (3-10% a fight in the logs), each enemy rolling on its own: with 2 enemies, two avoid
    /// rolls at 0.07.
    ///
    /// TODO: the engine's 7% is an estimate from three logs (misses and dodges together); the game's number is not known. Check in game.
    /// </summary>
    [Fact(Skip = "Wild Swing's avoid chance is an estimate from combat logs: to check in game")]
    public void Wild_Swing_Is_Avoided_7_Percent_Of_The_Time_Per_Enemy()
    {
        var f = StepFights.Tariq(enemies: 2);
        var procs = f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // only to read the rolls

        f.Cast<WildSwing>();
        var rolls = procs.ChancesOf(WildSwing.AvoidedProcKey);

        using var check = new Checks(f);
        check.Equal(2, rolls.Count);
        foreach (double chance in rolls) check.Equal(0.07, chance, what: "an avoid roll's chance");
    }

    // ---- Face Breaker

    /// <summary>
    /// Face Breaker deals 50% of its damage to the enemies near your target, and that splash cannot crit; a crit on the target carries
    /// over to it.
    ///
    /// 3 enemies and 50% crit; one Face Breaker: the target's hit has a crit chance of 0.5, each other enemy takes exactly half of it,
    /// with no crit chance.
    /// </summary>
    [Fact]
    public void Face_Breaker_Deals_Half_Its_Damage_To_Enemies_Near_The_Target_Without_Crit_Of_Its_Own()
    {
        var f = StepFights.Tariq(enemies: 3, crit: 0.5).WithoutDamageSpread();

        f.Cast<FaceBreaker>();
        var hits = f.Damage<FaceBreaker>();
        var target = hits.Single(h => h.Target == f.Enemy.Name);
        var splashes = hits.Where(h => h.Target != f.Enemy.Name).ToList();

        using var check = new Checks(f);
        check.Equal(3, hits.Count);
        check.Equal(2, splashes.Count);
        foreach (var splash in splashes)
        {
            check.Ratio(0.5, splash.Value / target.Value, what: "a splash over the target's hit");
            check.Equal(0.0, splash.CritChance, what: "a splash's crit chance");
        }
    }

    /// <summary>
    /// Beyond 12 enemies near the target, each Face Breaker splash is weaker: multiplied by sqrt(12 / enemies). A splash at 17 enemies
    /// (16 near the target) over a splash at 13 (12 near it) is sqrt(12 / 16).
    ///
    /// TODO: check in game whether the game counts the target in that number (the engine counts the enemies the splash hits).
    /// </summary>
    [Fact]
    public void Face_Breaker_Splash_Falls_Off_Beyond_12_Enemies()
    {
        double Splash(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies).WithoutDamageSpread();
            f.Cast<FaceBreaker>();
            return f.Damage<FaceBreaker>().First(h => h.Target != f.Enemy.Name).Value;
        }

        AssertRatio(Math.Sqrt(12.0 / 16), Splash(17) / Splash(13));
    }

    // ---- Heavy Strike

    /// <summary>
    /// Heavy Strike also deals 169.8% of your main stat to the enemies near your target.
    ///
    /// 3 enemies; one Heavy Strike: the target is hit once, and the other two enemies are hit (logged as another effect) for 1.698 / 5.66
    /// of the target's hit.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Cleaves_For_169_8_Percent_Of_The_Main_Stat()
    {
        var f = StepFights.Tariq(enemies: 3).WithoutDamageSpread();

        f.Cast<HeavyStrike>();
        var target = f.Ticks(HeavyStrike.TargetEffectId);
        var cleave = f.Ticks(HeavyStrike.CleaveEffectId);

        using var check = new Checks(f);
        check.Equal(1, target.Count);
        check.True(target[0].Target == f.Enemy.Name, "the hit is on the target");
        check.Equal(2, cleave.Count);
        check.False(cleave.Any(h => h.Target == f.Enemy.Name), "no cleave hit on the target");
        foreach (var hit in cleave) check.Ratio(1.698 / 5.66, hit.Value / target[0].Value, what: "a cleave hit over the target's hit");
    }

    /// <summary>
    /// Beyond 8 enemies near the target, each Heavy Strike cleave hit is weaker: multiplied by sqrt(8 / enemies). A cleave hit at 13
    /// enemies (12 near the target) over one at 9 (8 near it) is sqrt(8 / 12).
    ///
    /// TODO: check in game whether the game counts the target in that number (the engine counts the enemies the cleave hits).
    /// </summary>
    [Fact]
    public void Heavy_Strike_Cleave_Falls_Off_Beyond_8_Enemies()
    {
        double Cleave(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies).WithoutDamageSpread();
            f.Cast<HeavyStrike>();
            return f.Ticks(HeavyStrike.CleaveEffectId)[0].Value;
        }

        AssertRatio(Math.Sqrt(8.0 / 12), Cleave(13) / Cleave(9));
    }

    // ---- Skull Crusher

    /// <summary>Skull Crusher hits its target alone: with 3 enemies, one hit, on the target.</summary>
    [Fact]
    public void Skull_Crusher_Hits_Only_Its_Target()
    {
        var f = StepFights.Tariq(enemies: 3);
        f.Set(Tariq.Fury, 100); // enough to cast it
        f.SetHealth(0.2);

        f.Cast<SkullCrusher>();
        var hits = f.Damage<SkullCrusher>();

        using var check = new Checks(f);
        check.Equal(1, hits.Count);
        check.True(hits[0].Target == f.Enemy.Name, "the hit is on the target");
    }

    // ---- Hammer Storm

    /// <summary>
    /// Hammer Storm spins 3 times, dealing 35% more damage with each spin: 1, 1.35 and 1.8225 times the first (each spin multiplies the
    /// one before).
    /// </summary>
    [Fact]
    public void Hammer_Storm_Spins_3_Times_Each_35_Percent_Stronger_Than_The_One_Before()
    {
        var f = StepFights.Tariq().WithoutDamageSpread();
        f.Set(Tariq.Fury, 100); // enough to cast it
        f.SetHealth(0.2);

        f.Cast<HammerStorm>();
        f.Wait(1.5); // the spins are over
        var spins = f.Damage<HammerStorm>();

        using var check = new Checks(f);
        check.Equal(3, spins.Count);
        check.Ratio(1.35, spins[1].Value / spins[0].Value);
        check.Ratio(1.35 * 1.35, spins[2].Value / spins[0].Value);
    }

    /// <summary>Every spin of Hammer Storm hits every enemy near you: with 4 enemies, 12 hits, each spin on each enemy.</summary>
    [Fact]
    public void Hammer_Storm_Hits_Every_Enemy_On_Each_Spin()
    {
        var f = StepFights.Tariq(enemies: 4);
        f.Set(Tariq.Fury, 100); // enough to cast it
        f.SetHealth(0.2);

        f.Cast<HammerStorm>();
        f.Wait(1.5); // the spins are over
        var hits = f.Damage<HammerStorm>();

        using var check = new Checks(f);
        check.Equal(12, hits.Count);
        foreach (var spin in hits.Chunk(4)) check.Equal(4, spin.Select(h => h.Target).Distinct().Count(), what: "enemies hit by a spin");
    }

    /// <summary>
    /// Beyond 12 enemies each Hammer Storm hit is weaker: multiplied by sqrt(12 / enemies). The first spin's first hit at 16 enemies over
    /// the same at 12 is sqrt(12 / 16).
    /// </summary>
    [Fact]
    public void Hammer_Storm_Falls_Off_Beyond_12_Enemies()
    {
        double FirstSpinHit(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies).WithoutDamageSpread();
            f.Set(Tariq.Fury, 100); // enough to cast it
            f.SetHealth(0.2);
            f.Cast<HammerStorm>();
            return f.Damage<HammerStorm>()[0].Value;
        }

        AssertRatio(Math.Sqrt(12.0 / 16), FirstSpinHit(16) / FirstSpinHit(12));
    }

    // ---- Culling Strike (the Fury it spends is in TariqResourceEconomy.cs)

    /// <summary>Culling Strike can only be used on an enemy at 30% health or less: refused on an enemy at 31%, allowed at 29%.</summary>
    [Fact]
    public void Culling_Strike_Needs_An_Enemy_At_30_Percent_Health_Or_Less()
    {
        var f = StepFights.Tariq();

        using var check = new Checks(f);
        f.SetHealth(0.31);
        check.False(f.TryCast<CullingStrike>().Succeeded);

        f.SetHealth(0.29);
        check.True(f.TryCast<CullingStrike>().Succeeded);
    }

    /// <summary>
    /// Culling Strike deals 40% more damage for each Fury it spends, up to 10 Fury (+400%), and no more beyond: on an enemy at 20% health,
    /// a cast at 0, 5, 10 and 50 Fury deals 1, 3, 5 and 5 times the 0-Fury hit.
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
            var f = StepFights.Tariq().WithoutDamageSpread();
            f.SetHealth(0.2); // in execute range
            f.Set(Tariq.Fury, furyGiven);
            f.Cast<CullingStrike>();
            return f.Damage<CullingStrike>().First().Value;
        }

        AssertRatio(times, Hit(fury) / Hit(0));
    }

    // ---- Chain Lightning

    /// <summary>Chain Lightning can only be cast while Thunder Call is active: refused at the start of a fight, allowed after a Thunder
    /// Call.</summary>
    [Fact]
    public void Chain_Lightning_Needs_Thunder_Call()
    {
        var f = StepFights.Tariq();

        using var check = new Checks(f);
        check.False(f.TryCast<ChainLightning>().Succeeded);

        f.Cast<ThunderCall>();
        check.True(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// Chain Lightning's arc hits your target first, then jumps to enemies it has not hit yet; with a single enemy it hits it every time.
    ///
    /// On 3 enemies, the first hit is on the target and the first three hits are on three different enemies; on 1 enemy, every hit is on
    /// it.
    /// </summary>
    [Fact]
    public void Chain_Lightning_Hits_The_Target_First_Then_Enemies_It_Has_Not_Hit()
    {
        var threeEnemyScenario = StepFights.Tariq(enemies: 3);

        threeEnemyScenario.Cast<ThunderCall>(); // enables Chain Lightning
        threeEnemyScenario.Cast<ChainLightning>();
        var threeEnemyHits = threeEnemyScenario.Damage<ChainLightning>();

        var oneEnemyScenario = StepFights.Tariq();

        oneEnemyScenario.Cast<ThunderCall>();
        oneEnemyScenario.Cast<ChainLightning>();
        var oneEnemyHits = oneEnemyScenario.Damage<ChainLightning>();

        using var check = new Checks();
        check.True(threeEnemyHits[0].Target == threeEnemyScenario.Enemy.Name, "the first hit is on the target");
        check.Equal(3, threeEnemyHits.Take(3).Select(h => h.Target).Distinct().Count());
        foreach (var hit in oneEnemyHits) check.True(hit.Target == oneEnemyScenario.Enemy.Name, "a hit on the single enemy");
    }

    // ---- Leap Smash

    /// <summary>Leap Smash hits every enemy near the target: with 3 enemies, one hit on each.</summary>
    [Fact]
    public void Leap_Smash_Hits_Every_Enemy_Near_The_Target()
    {
        var f = StepFights.Tariq(enemies: 3);

        f.Cast<LeapSmash>();
        f.Wait(0.5); // the leap lands

        Assert.Equal(3, f.Damage<LeapSmash>().Select(h => h.Target).Distinct().Count());
    }

    /// <summary>
    /// Beyond 8 enemies each Leap Smash hit is weaker: multiplied by sqrt(8 / enemies). A hit at 12 enemies over a hit at 8 is
    /// sqrt(8 / 12).
    /// </summary>
    [Fact]
    public void Leap_Smash_Falls_Off_Beyond_8_Enemies()
    {
        double Hit(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies).WithoutDamageSpread();
            f.Cast<LeapSmash>(crit: false);
            f.Wait(0.5); // the leap lands
            return f.Damage<LeapSmash>()[0].Value;
        }

        AssertRatio(Math.Sqrt(8.0 / 12), Hit(12) / Hit(8));
    }

    // ---- Thunder Call

    /// <summary>Thunder Call enhances your attacks with lightning for 21 seconds.</summary>
    [Fact]
    public void Thunder_Call_Lasts_21_Seconds()
    {
        var f = StepFights.Tariq();

        f.Cast<ThunderCall>();
        double end = f.CompletedAt("Thunder Call") + 21;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(ThunderCall.Buff));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(ThunderCall.Buff));
    }

    /// <summary>
    /// With Thunder Call, Skull Crusher also beckons a thunder bolt that deals 406% of your main stat to the target: a hit named after
    /// Thunder Call, on the target, 4.06 / 11.6 of the physical hit. Without Thunder Call there is no such hit.
    /// </summary>
    [Fact]
    public void Skull_Crusher_Adds_A_Thunder_Bolt_With_Thunder_Call()
    {
        var plainScenario = StepFights.Tariq().WithoutDamageSpread();
        plainScenario.Set(Tariq.Fury, 100); // enough to cast it
        plainScenario.SetHealth(0.2);

        plainScenario.Cast<SkullCrusher>();

        var thunderScenario = StepFights.Tariq().WithoutDamageSpread();
        thunderScenario.Set(Tariq.Fury, 100);
        thunderScenario.SetHealth(0.2);

        thunderScenario.Cast<ThunderCall>();
        thunderScenario.Cast<SkullCrusher>();
        var bolts = thunderScenario.Damage("Thunder Call: Skull Crusher");

        using var check = new Checks();
        check.Equal(0, plainScenario.Damage("Thunder Call: Skull Crusher").Count);
        check.Equal(1, bolts.Count);
        check.True(bolts[0].Target == thunderScenario.Enemy.Name, "the bolt is on the target");
        check.Ratio(4.06 / 11.6, bolts[0].Value / thunderScenario.Damage<SkullCrusher>().First().Value);
    }

    /// <summary>
    /// With Thunder Call, Heavy Strike also charges the ground with lightning, dealing 198% of your main stat to the target and to every
    /// enemy near it: with 3 enemies, lightning on each, 1.98 / 5.66 of the target's physical hit. Without Thunder Call there is none.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Adds_Lightning_On_Every_Enemy_With_Thunder_Call()
    {
        var plainScenario = StepFights.Tariq(enemies: 3).WithoutDamageSpread();

        plainScenario.Cast<HeavyStrike>();

        var thunderScenario = StepFights.Tariq(enemies: 3).WithoutDamageSpread();

        thunderScenario.Cast<ThunderCall>();
        thunderScenario.Cast<HeavyStrike>();
        var bolts = thunderScenario.Damage("Thunder Call: Heavy Strike");
        double targetHit = thunderScenario.Ticks(HeavyStrike.TargetEffectId)[0].Value;

        using var check = new Checks();
        check.Equal(0, plainScenario.Damage("Thunder Call: Heavy Strike").Count);
        check.Equal(3, bolts.Select(h => h.Target).Distinct().Count());
        foreach (var bolt in bolts) check.Ratio(1.98 / 5.66, bolt.Value / targetHit, what: "lightning over the target's physical hit");
    }

    /// <summary>
    /// With Thunder Call, each spin of Hammer Storm shocks every enemy hit for 78% of your main stat (against 223% for the spin), growing
    /// with the spin as the physical hit does: on one enemy, 3 lightning hits, each 0.78 / 2.23 of the physical spin of the same moment.
    /// Without Thunder Call there are none.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Adds_Lightning_On_Each_Spin_With_Thunder_Call()
    {
        var plainScenario = StepFights.Tariq().WithoutDamageSpread();
        plainScenario.Set(Tariq.Fury, 100); // enough to cast it
        plainScenario.SetHealth(0.2);

        plainScenario.Cast<HammerStorm>();
        plainScenario.Wait(1.5); // the spins are over

        var thunderScenario = StepFights.Tariq().WithoutDamageSpread();
        thunderScenario.Set(Tariq.Fury, 100);
        thunderScenario.SetHealth(0.2);

        thunderScenario.Cast<ThunderCall>();
        thunderScenario.Cast<HammerStorm>();
        thunderScenario.Wait(1.5);
        var spins = thunderScenario.Damage<HammerStorm>();
        var bolts = thunderScenario.Damage("Thunder Call: Hammer Storm");

        using var check = new Checks();
        check.Equal(0, plainScenario.Damage("Thunder Call: Hammer Storm").Count);
        check.Equal(3, bolts.Count);
        for (int i = 0; i < 3; i++) check.Ratio(0.78 / 2.23, bolts[i].Value / spins[i].Value, what: $"spin {i + 1}'s lightning over its physical hit");
    }

    /// <summary>
    /// With Thunder Call, a Heavy Strike hits in this order: the target, then the other enemies with the cleave, then the lightning on all
    /// of them. With 3 enemies: the target's hit, 2 cleave hits, 3 lightning hits.
    /// </summary>
    [Fact]
    public void Heavy_Strike_Hits_The_Target_Then_The_Others_Then_The_Lightning()
    {
        var f = StepFights.Tariq(enemies: 3);

        f.Cast<ThunderCall>();

        f.Cast<HeavyStrike>();

        Assert.Equal(
            [
                LogIds.Effect(HeavyStrike.TargetEffectId),
                LogIds.Effect(HeavyStrike.CleaveEffectId), LogIds.Effect(HeavyStrike.CleaveEffectId),
                LogIds.Effect(HeavyStrike.LightningEffectId), LogIds.Effect(HeavyStrike.LightningEffectId), LogIds.Effect(HeavyStrike.LightningEffectId),
            ],
            f.AllDamage().Select(hit => hit.AbilityId));
    }

    /// <summary>
    /// With Thunder Call, every spin of Hammer Storm hits every enemy with the physical damage first, then every enemy with the lightning:
    /// with 2 enemies, the first four hits are physical, physical, lightning, lightning.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Spin_Hits_Every_Enemy_With_Physical_Then_Every_Enemy_With_Lightning()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.Set(Tariq.Fury, 100); // enough to cast it

        f.Cast<ThunderCall>();

        f.Cast<HammerStorm>();

        Assert.Equal(["Hammer Storm", "Hammer Storm", "Thunder Call: Hammer Storm", "Thunder Call: Hammer Storm"],
            f.AllDamage().Take(4).Select(hit => hit.Name));
    }

    /// <summary>
    /// Beyond 15 enemies the lightning of Hammer Storm falls off, multiplied by sqrt(15 / enemies), while its physical hit already did
    /// beyond 12: at 16 enemies, the first spin's physical hit is sqrt(12 / 16) of a 12-enemy one's, and its lightning sqrt(15 / 16) of a
    /// 15-enemy one's.
    /// </summary>
    [Fact]
    public void Hammer_Storm_Lightning_Falls_Off_Beyond_15_Enemies()
    {
        (double Physical, double Lightning) FirstSpin(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies).WithoutDamageSpread();
            f.Set(Tariq.Fury, 100); // enough to cast it
            f.Cast<ThunderCall>();
            f.Cast<HammerStorm>();
            return (f.Damage<HammerStorm>()[0].Value, f.Damage("Thunder Call: Hammer Storm")[0].Value);
        }

        var sixteenEnemySpin = FirstSpin(16);

        using var check = new Checks();
        check.Ratio(Math.Sqrt(12.0 / 16), sixteenEnemySpin.Physical / FirstSpin(12).Physical);
        check.Ratio(Math.Sqrt(15.0 / 16), sixteenEnemySpin.Lightning / FirstSpin(15).Lightning);
    }

    /// <summary>
    /// The thunder bolt of a Thunder Call Skull Crusher does not need the physical hit to land first: it follows the cast's target. With 2
    /// enemies, a Skull Crusher on the second one: the bolt and the physical hit are both on it.
    /// </summary>
    [Fact]
    public void Skull_Crusher_Thunder_Bolt_Strikes_The_Cast_Target()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.Set(Tariq.Fury, 100); // enough to cast it
        var second = f.Enemies()[1];

        f.Cast<ThunderCall>();

        f.Cast<SkullCrusher>(target: second);

        using var check = new Checks(f);
        check.True(f.Damage("Thunder Call: Skull Crusher").Single().Target == second.Name, "the bolt is on the second enemy");
        check.True(f.Damage<SkullCrusher>().Single().Target == second.Name, "the physical hit is on the second enemy");
    }

    // ---- Focused Wrath (its cost reduction is in TariqResourceEconomy.cs)

    /// <summary>Focused Wrath makes your next two Skull Crusher or Hammer Storm casts deal 10% more damage, and the third does not: three
    /// Skull Crushers deal 1.1, 1.1 and 1 times a Skull Crusher with no Focused Wrath.</summary>
    [Fact]
    public void Focused_Wrath_Adds_10_Percent_Damage_To_The_Next_Two_Casts()
    {
        var plainScenario = StepFights.Tariq().WithoutDamageSpread();
        plainScenario.Set(Tariq.Fury, 100); // enough to cast it

        plainScenario.Cast<SkullCrusher>();
        double plainCrusherDamage = plainScenario.Damage<SkullCrusher>().First().Value;

        var wrathScenario = StepFights.Tariq().WithoutDamageSpread();
        wrathScenario.Set(Tariq.Fury, 100); // enough for three

        wrathScenario.Cast<FocusedWrath>();

        wrathScenario.Cast<SkullCrusher>();
        wrathScenario.Cast<SkullCrusher>();
        wrathScenario.Cast<SkullCrusher>();
        var hits = wrathScenario.Damage<SkullCrusher>();

        using var check = new Checks();
        check.Ratio(1.1, hits[0].Value / plainCrusherDamage);
        check.Ratio(1.1, hits[1].Value / plainCrusherDamage);
        check.Ratio(1.0, hits[2].Value / plainCrusherDamage);
    }

    /// <summary>
    /// Focused Wrath on a Hammer Storm uses one stack for the whole cast: with Thunder Call, all 3 spins and their lightning deal 10% more,
    /// and 1 of the 2 stacks is left.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Boosts_Every_Spin_Of_One_Hammer_Storm_And_Uses_One_Stack()
    {
        var plainScenario = StepFights.Tariq().WithoutDamageSpread();
        plainScenario.Set(Tariq.Fury, 100); // enough to cast it

        plainScenario.Cast<ThunderCall>();
        plainScenario.Cast<HammerStorm>();
        plainScenario.Wait(1.5); // the spins are over
        var plainSpins = plainScenario.Damage<HammerStorm>();
        var plainLightning = plainScenario.Damage("Thunder Call: Hammer Storm");

        var wrathScenario = StepFights.Tariq().WithoutDamageSpread();
        wrathScenario.Set(Tariq.Fury, 100);

        wrathScenario.Cast<ThunderCall>();
        wrathScenario.Cast<FocusedWrath>();
        wrathScenario.Cast<HammerStorm>();
        wrathScenario.Wait(1.5);
        var wrathSpins = wrathScenario.Damage<HammerStorm>();
        var wrathLightning = wrathScenario.Damage("Thunder Call: Hammer Storm");

        using var check = new Checks(wrathScenario);
        check.Equal(1, wrathScenario.Stacks(FocusedWrath.Buff));
        for (int i = 0; i < 3; i++)
        {
            check.Ratio(1.1, wrathSpins[i].Value / plainSpins[i].Value, what: $"spin {i + 1} over the plain one");
            check.Ratio(1.1, wrathLightning[i].Value / plainLightning[i].Value, what: $"spin {i + 1}'s lightning over the plain one");
        }
    }

    /// <summary>Focused Wrath boosts the thunder bolt of a Thunder Call Skull Crusher as well as the physical hit: the bolt is 1.1 times a
    /// bolt without Focused Wrath.</summary>
    [Fact]
    public void Focused_Wrath_Boosts_The_Skull_Crusher_Thunder_Bolt_Too()
    {
        double Bolt(bool wrath)
        {
            var f = StepFights.Tariq().WithoutDamageSpread();
            f.Set(Tariq.Fury, 100); // enough to cast it
            f.Cast<ThunderCall>();
            if (wrath) f.Cast<FocusedWrath>();
            f.Cast<SkullCrusher>();
            return f.Damage("Thunder Call: Skull Crusher").Single().Value;
        }

        AssertRatio(1.1, Bolt(wrath: true) / Bolt(wrath: false));
    }

    /// <summary>
    /// Focused Wrath holds 3 stacks at most: with Mouth for War, the ability's 2 stacks and the 1 of a Leap Smash fill it, and another
    /// Leap Smash adds none.
    /// </summary>
    [Fact]
    public void Focused_Wrath_Holds_3_Stacks_At_Most()
    {
        var f = StepFights.Tariq(talents: [MouthForWar.TalentId]);

        f.Cast<FocusedWrath>();

        f.Cast<LeapSmash>();
        f.Wait(0.5); // the leap lands
        Assert.Equal(3, f.Stacks(FocusedWrath.Buff));

        f.ResetCooldown<LeapSmash>(); // a second leap at once
        f.Cast<LeapSmash>();
        f.Wait(0.5); // the leap lands
        Assert.Equal(3, f.Stacks(FocusedWrath.Buff));
    }

    // ---- Rising Spirit (its own rules are in Generic/RisingSpirit.cs, the Spirit Refund's in Generic/SpiritRefund.cs)

    /// <summary>
    /// Each Spirit Refund gives a stack of Rising Spirit, +5% of your main stat each, so your hits deal 5% more per stack: two Skull
    /// Crushers with their Spirit Refunds give 2 stacks, and a Wild Swing after them is 1.10 times 129.14% of the main stat.
    ///
    /// Within 0.1% of 1.2914 x 1.10.
    /// </summary>
    [Fact]
    public void Rising_Spirit_Adds_5_Percent_Damage_Per_Stack()
    {
        var f = StepFights.Tariq(spirit: 0.2).WithoutDamageSpread();
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // no Wild Swing is avoided
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Set(Tariq.Fury, 100); // enough for both

        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();
        Assert.Equal(2, f.Stacks(Hero.RisingSpirit));

        f.Cast<WildSwing>();

        AssertRatio(1.2914 * 1.10, f.Damage<WildSwing>().First().Value / MainStat, 1.2914 * 1.10 * RatioTolerance);
    }

    // ---- Raging Tempest (its Spirit cost and Spirit of Heroism are in Generic/Ultimate.cs)

    /// <summary>
    /// Raging Tempest leaps to the target area and deals 1006% of your main stat of lightning damage to the enemies near it; beyond 5
    /// enemies each hit is weaker: multiplied by sqrt(5 / enemies).
    ///
    /// On 1 enemy, the hit is 10.06 times the main stat (the 1% Expertise of the stack the cast gives taken out), within 0.1%; on 5 and 10
    /// enemies, the first hits are in the ratio of sqrt(5 / 10).
    /// </summary>
    [Fact]
    public void Raging_Tempest_Leaps_For_1006_Percent_Of_The_Main_Stat_And_Falls_Off_Beyond_5_Enemies()
    {
        StepEvent Leap(int enemies)
        {
            var f = StepFights.Tariq(enemies: enemies).WithoutDamageSpread();
            f.Set(Hero.SpiritPoints, 100); // enough to cast it
            f.Cast<RagingTempest>(); // at 0s
            f.Wait(0.5); // the leap lands
            return f.Damage<RagingTempest>().First();
        }

        var oneEnemyLeap = Leap(1);

        using var check = new Checks();
        check.Equal(10.06, oneEnemyLeap.Value / 1.01 / MainStat, 10.06 * RatioTolerance);
        check.Ratio(Math.Sqrt(5.0 / 10), Leap(10).Value / Leap(5).Value);
    }

    /// <summary>
    /// Raging Tempest gives an Enhanced Thunder Call for 20 seconds: your attacks have lightning and you can cast Chain Lightning, as with
    /// Thunder Call. Right after it Chain Lightning is allowed and a Skull Crusher has a thunder bolt; 20.1 seconds after that Skull
    /// Crusher, Chain Lightning is refused.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Charges_Your_Attacks_Like_Thunder_Call()
    {
        var f = StepFights.Tariq();
        f.Set(Tariq.Fury, 100); // enough for Skull Crusher
        f.SetHealth(0.2);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast<RagingTempest>();

        using var check = new Checks(f);
        check.True(f.TryCast<ChainLightning>().Succeeded);
        f.Cast<SkullCrusher>();
        check.Equal(1, f.Damage("Thunder Call: Skull Crusher").Count);

        f.Wait(20.1);
        check.False(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// Raging Tempest removes Thunder Call, and Thunder Call cannot be cast while Raging Tempest lasts: after Thunder Call then Raging
    /// Tempest, Thunder Call's aura is gone; a new Thunder Call is refused even off cooldown; 20.1 seconds after the Tempest it is allowed
    /// again.
    ///
    /// TODO: the engine's rule comes from an icy-veins guide and 45 logs where none was cast during a Tempest. Check in game.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Removes_Thunder_Call_And_Blocks_It()
    {
        var f = StepFights.Tariq();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast<ThunderCall>();
        f.Cast<RagingTempest>();
        double start = f.CompletedAt("Raging Tempest");

        using var check = new Checks(f);
        check.False(f.Has(ThunderCall.Buff));
        f.ResetCooldown<ThunderCall>(); // off cooldown, so only the Tempest can refuse it
        check.False(f.TryCast<ThunderCall>().Succeeded);

        f.WaitUntil(start + 20.1);
        check.True(f.TryCast<ThunderCall>().Succeeded);
    }

    /// <summary>
    /// Raging Tempest's buff comes with the cast at 1 stack and lasts 20 seconds; each Raging Currents hit (a lightning strike at a random
    /// enemy every second, shortened by haste) adds a stack and does not extend it, up to 20 stacks; each stack is +1% Expertise.
    ///
    /// At 0% haste (so 30% from Spirit of Heroism: a strike every 1 / 1.3 seconds): 1 stack and 1% Expertise at the cast; at 5 seconds, 6
    /// strikes, 7 stacks and 7% Expertise; the buff is still up at 19.9 seconds and gone at 20.1.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Stacks_With_Each_Raging_Currents_Hit_Without_Extending()
    {
        var f = StepFights.Tariq();
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<RagingTempest>();
        double start = f.CompletedAt("Raging Tempest");

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(RagingTempest.Buff));
        check.Equal(0.01, f.Expertise());

        f.WaitUntil(start + 5.0);
        check.Equal(6, f.Damage("Raging Currents").Count);
        check.Equal(7, f.Stacks(RagingTempest.Buff));
        check.Equal(0.07, f.Expertise());

        f.WaitUntil(start + 19.9);
        check.True(f.Stacks(RagingTempest.Buff) > 0);
        f.WaitUntil(start + 20.1);
        check.Equal(0, f.Stacks(RagingTempest.Buff));
    }

    /// <summary>
    /// Raging Currents strikes for 154% of your main stat of lightning damage: the first strike, with the 1% Expertise of the one stack
    /// there is taken out, over the main stat is 1.54, within 0.1%.
    /// </summary>
    [Fact]
    public void Raging_Currents_Strikes_For_154_Percent_Of_The_Main_Stat()
    {
        var f = StepFights.Tariq().WithoutDamageSpread();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast<RagingTempest>();
        f.Wait(1.0); // the first strike

        AssertRatio(1.54, f.Damage("Raging Currents").First().Value / 1.01 / MainStat, 1.54 * RatioTolerance);
    }

    /// <summary>During Raging Tempest your abilities have lightning as with Thunder Call, Heavy Strike's included: with 2 enemies, a Heavy
    /// Strike has 2 lightning hits.</summary>
    [Fact]
    public void Heavy_Strike_Has_Lightning_During_Raging_Tempest()
    {
        var f = StepFights.Tariq(enemies: 2);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast<RagingTempest>();

        f.Cast<HeavyStrike>();

        Assert.Equal(2, f.Damage("Thunder Call: Heavy Strike").Count);
    }

    /// <summary>
    /// Raging Currents strikes a random nearby enemy: with several enemies the strikes land on them, not on one only. With 4 enemies at 2%
    /// haste, over 21 seconds, 26 strikes (with the 30% of Spirit of Heroism) on at least 2 different enemies.
    /// </summary>
    [Fact]
    public void Raging_Currents_Strikes_Random_Enemies()
    {
        var f = StepFights.Tariq(haste: 0.02, enemies: 4);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast<RagingTempest>();
        f.Wait(21); // Raging Tempest is over
        var targets = f.Damage("Raging Currents").Select(strike => strike.Target).ToList();

        using var check = new Checks(f);
        check.Equal(26, targets.Count);
        check.True(targets.Distinct().Count() >= 2, "at least 2 different enemies struck");
    }
}
