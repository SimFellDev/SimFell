using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Tariq's talents, as their tooltips describe them, alone and with each other: Left Hand Path, Ride the Lightning, Square Hammer, High
/// Road, Blood &amp; Thunder, Bloodline, The Motherload, Mouth for War, Thunderstruck, Pneuma, Sledgehammer, Far Beyond Driven, Kill Em
/// All, Ace of Spades, Schism, Them Bones, Crack the Sky and Killing in the Name. How Bloodline's Fury follows haste is in TariqHasteBreakpoints.cs.
///
/// The talent is taken alone, with 100 Fury and the target at 20% health unless the test says otherwise. Damage has no spread where a hit
/// is compared, and random procs are forced, the chance of each read from its rolls. Talents no combat log has shown are marked
/// unverified in the engine; the tests follow the tooltips.
/// </summary>
public class TariqTalents
{
    /// <summary>Tariq with <paramref name="talent"/>, 100 Fury and the target at 20% health, so every spender and Culling Strike can be
    /// cast.</summary>
    private static StepDriver WithTalent(int talent, int enemies = 1, double crit = 0)
    {
        var f = StepFights.Tariq(enemies: enemies, talents: [talent], crit: crit);
        f.Set(Tariq.Fury, 100); // enough for any spender
        f.SetHealth(0.2); // in Culling Strike's range
        return f;
    }

    /// <summary>The first hit of a cast of <typeparamref name="T"/> with no talent, 100 Fury and the target at 20% health: what a talent's
    /// bonus is compared with.</summary>
    private static double PlainHit<T>() where T : Ability
    {
        var f = StepFights.Tariq().WithoutDamageSpread();
        f.Set(Tariq.Fury, 100); // enough for any spender
        f.SetHealth(0.2); // in Culling Strike's range
        f.Cast<T>();
        return f.Damage<T>().First().Value;
    }

    /// <summary>When <typeparamref name="T"/> is ready again, on the fight clock.</summary>
    private static double ReadyAt<T>(StepDriver f) where T : Ability =>
        f.Seconds() + f.Hero.GetCooldownRemains(f.Ability<T>().Name, f.Now);

    // ---- Left Hand Path

    /// <summary>
    /// With Left Hand Path, your Face Breaker has 40% more crit chance: at 10% crit, a Face Breaker has a crit chance of 0.5 and a Skull
    /// Crusher 0.1.
    /// </summary>
    [Fact]
    public void Left_Hand_Path_Gives_Face_Breaker_40_Percent_Crit_Chance()
    {
        var f = WithTalent(LeftHandPath.TalentId, crit: 0.1);

        f.Cast<FaceBreaker>();
        f.Cast<SkullCrusher>();

        using var check = new Checks(f);
        check.Equal(0.5, f.Damage<FaceBreaker>()[0].CritChance);
        check.Equal(0.1, f.Damage<SkullCrusher>()[0].CritChance);
    }

    // ---- Ride the Lightning

    /// <summary>
    /// With Ride the Lightning, Thunder Call gives you 5% haste while it is active: 5% at 20.9 seconds after it, and none at 21.1.
    /// </summary>
    [Fact]
    public void Ride_The_Lightning_Gives_5_Percent_Haste_During_Thunder_Call()
    {
        var f = WithTalent(RideTheLightning.TalentId);

        f.Cast<ThunderCall>(); // at 0s, off the global cooldown

        f.WaitUntil(20.9);
        Assert.Equal(0.05, f.Haste(), 1e-9);
        f.WaitUntil(21.1);
        Assert.Equal(0.0, f.Haste(), 1e-9);
    }

    /// <summary>
    /// With Ride the Lightning, Thunder Call generates 30 Fury over its 21 seconds, and no more once it ends: starting with no Fury, at
    /// 21.1 seconds Fury is between 30 and 31.7, and 4 seconds later it has not changed.
    ///
    /// The range: 30 if the ticks ignore the talent's own 5% haste, 31.5 plus a tick or two if they follow it. TODO: which of the two the
    /// game does (the engine's ticks follow haste, as the logs showed, including the talent's 5%?). Check in game.
    /// </summary>
    [Fact]
    public void Ride_The_Lightning_Generates_About_30_Fury_Over_Thunder_Call()
    {
        var f = StepFights.Tariq(talents: [RideTheLightning.TalentId]);

        f.Cast<ThunderCall>(); // at 0s, off the global cooldown
        f.WaitUntil(21.1); // Thunder Call is over
        double furyAtTheEnd = f.Amount(Tariq.Fury);
        f.WaitUntil(25.1);

        using var check = new Checks(f);
        check.True(furyAtTheEnd >= 30.0 && furyAtTheEnd <= 31.7);
        check.Equal(furyAtTheEnd, f.Amount(Tariq.Fury));
    }

    /// <summary>
    /// With Ride the Lightning, Fury also comes during Raging Tempest's Enhanced Thunder Call, and stops when it ends: more than 5 Fury at
    /// 5 seconds, and the same at 20.1 and 25.1 seconds.
    /// </summary>
    [Fact]
    public void Ride_The_Lightning_Generates_Fury_During_Raging_Tempest()
    {
        var f = StepFights.Tariq(talents: [RideTheLightning.TalentId]);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        f.Cast<RagingTempest>(); // at 0s, off the global cooldown

        f.WaitUntil(5);
        Assert.True(f.Amount(Tariq.Fury) > 5);

        f.WaitUntil(20.1); // Raging Tempest is over
        double furyAtTheEnd = f.Amount(Tariq.Fury);
        f.WaitUntil(25.1);
        Assert.Equal(furyAtTheEnd, f.Amount(Tariq.Fury), 1e-9);
    }

    /// <summary>
    /// With Bloodline and Ride the Lightning, Fury comes from both while Thunder Call lasts: 10.05 seconds after a Thunder Call, you have
    /// more than 10 Fury above Bloodline alone.
    /// </summary>
    [Fact]
    public void Bloodline_And_Ride_The_Lightning_Both_Give_Fury_During_Thunder_Call()
    {
        var bloodlineScenario = StepFights.Tariq(talents: [Bloodline.TalentId]);

        bloodlineScenario.Cast<ThunderCall>();
        bloodlineScenario.Wait(10.05);
        double bloodlineFury = bloodlineScenario.Amount(Tariq.Fury);

        var bothScenario = StepFights.Tariq(talents: [Bloodline.TalentId, RideTheLightning.TalentId]);

        bothScenario.Cast<ThunderCall>();
        bothScenario.Wait(10.05);
        double bothFury = bothScenario.Amount(Tariq.Fury);

        Assert.True(bothFury - bloodlineFury > 10, $"{bothFury} against {bloodlineFury}");
    }

    // ---- Square Hammer

    /// <summary>
    /// With Square Hammer, your Heavy Strike gives you a stack of Square Hammer for 20 seconds, up to 5: a Heavy Strike gives 1 stack; with
    /// 5 stacks, another Heavy Strike leaves 5, there 19.9 seconds after it and gone at 20.1.
    /// </summary>
    [Fact]
    public void Square_Hammer_Stacks_With_Heavy_Strike_Up_To_5_For_20_Seconds()
    {
        var f = WithTalent(SquareHammer.TalentId);

        f.Cast<HeavyStrike>();
        Assert.Equal(1, f.Stacks(SquareHammer.Stacks));

        f.Apply(SquareHammer.Stacks, stacks: 5); // at the cap
        f.CastWhenReady("Heavy Strike");
        double cast = f.CompletedAt("Heavy Strike");
        Assert.Equal(5, f.Stacks(SquareHammer.Stacks));

        f.WaitUntil(cast + 19.9);
        Assert.Equal(5, f.Stacks(SquareHammer.Stacks));
        f.WaitUntil(cast + 20.1);
        Assert.Equal(0, f.Stacks(SquareHammer.Stacks));
    }

    /// <summary>
    /// With Square Hammer, your next Skull Crusher or Hammer Storm uses up all the stacks, and takes 1 second per stack off Thunder Call's
    /// cooldown: Thunder Call (45 second cooldown) at the start, 3 stacks, then the spender: no stack left, and Thunder Call is ready at
    /// 42 seconds.
    /// </summary>
    [Theory]
    [InlineData("Skull Crusher")]
    [InlineData("Hammer Storm")]
    public void Square_Hammer_Is_Consumed_By_The_Next_Spender_And_Shortens_Thunder_Call(string spender)
    {
        var f = WithTalent(SquareHammer.TalentId);

        f.Cast<ThunderCall>();
        f.Apply(SquareHammer.Stacks, stacks: 3); // as 3 Heavy Strikes would

        f.Cast(spender);

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(SquareHammer.Stacks));
        check.Equal(42.0, ReadyAt<ThunderCall>(f), 0.002);
    }

    /// <summary>
    /// With Square Hammer, using up the stacks gives you 8% Expertise for 4 seconds, which the spender that used them already has: with 2
    /// stacks, a Skull Crusher deals 1.08 times a plain one, and you have 8% Expertise at the cast and at 3.9 seconds, none at 4.1.
    /// </summary>
    [Fact]
    public void Square_Hammer_Gives_8_Percent_Expertise_For_4_Seconds_When_Consumed()
    {
        double plainDamage = PlainHit<SkullCrusher>();
        var f = WithTalent(SquareHammer.TalentId).WithoutDamageSpread();
        f.Apply(SquareHammer.Stacks, stacks: 2); // as 2 Heavy Strikes would
        Assert.Equal(0.0, f.Expertise(), 1e-9);

        f.Cast<SkullCrusher>(); // at 0s, over at 1s

        AssertRatio(1.08, f.Damage<SkullCrusher>().First().Value / plainDamage);
        Assert.Equal(0.08, f.Expertise(), 1e-9);
        f.WaitUntil(3.9);
        Assert.Equal(0.08, f.Expertise(), 1e-9);
        f.WaitUntil(4.1);
        Assert.Equal(0.0, f.Expertise(), 1e-9);
    }

    /// <summary>
    /// With Square Hammer and no stacks, a Skull Crusher gives no Expertise and does not touch Thunder Call's cooldown: Thunder Call, then
    /// a Skull Crusher; no Expertise, and Thunder Call is ready at 45 seconds.
    /// </summary>
    [Fact]
    public void Square_Hammer_Does_Nothing_Without_Stacks()
    {
        var f = WithTalent(SquareHammer.TalentId);

        f.Cast<ThunderCall>();

        f.Cast<SkullCrusher>();

        using var check = new Checks(f);
        check.Equal(0.0, f.Expertise());
        check.Equal(45.0, ReadyAt<ThunderCall>(f), 0.002);
    }

    // ---- High Road

    /// <summary>
    /// With High Road, when your Leap Smash hits at least 1 enemy you gain 20 Fury: one Leap Smash at 3 enemies gives no Fury at the cast,
    /// and one gain of 20 with the hit, 0.3 seconds later.
    /// </summary>
    [Fact]
    public void High_Road_Gives_20_Fury_When_Leap_Smash_Hits()
    {
        var f = WithTalent(HighRoad.TalentId, enemies: 3);
        f.Set(Tariq.Fury, 0);

        f.Cast<LeapSmash>();
        Assert.Equal(0, f.Amount(Tariq.Fury));
        f.Wait(0.5); // the leap lands

        Assert.Equal(new[] { 20.0 }, f.Gains("Fury").Select(gain => gain.Value));
    }

    /// <summary>
    /// With High Road, when your Leap Smash hits no enemy its cooldown is reduced by 10 seconds: with the only enemy gone, a Leap Smash is
    /// ready again at 10 seconds (20 - 10), and gives no Fury.
    ///
    /// The enemy is defeated first: it stands for "no enemy near the landing spot".
    /// </summary>
    [Fact]
    public void High_Road_Shortens_Leap_Smash_By_10_Seconds_When_It_Hits_Nothing()
    {
        var f = WithTalent(HighRoad.TalentId);
        f.Set(Tariq.Fury, 0);
        f.Defeat(f.Enemy); // no enemy where the leap lands

        f.Cast<LeapSmash>();
        f.Wait(0.5); // the leap lands

        using var check = new Checks(f);
        check.Equal(10.0, ReadyAt<LeapSmash>(f), 0.002);
        check.Equal(0, f.Gains("Fury").Count);
    }

    // ---- Blood & Thunder

    /// <summary>
    /// With Blood &amp; Thunder, your Heavy Strike has a 20% chance to cast a free Chain Lightning at the target: with Thunder Call, a
    /// Heavy Strike gives 6 Chain Lightning hits, and Chain Lightning is still ready.
    ///
    /// The proc is forced on; its chance, 0.2, is rolled once per Heavy Strike.
    /// </summary>
    [Fact]
    public void Blood_And_Thunder_Casts_A_Free_Chain_Lightning_With_Heavy_Strike()
    {
        var f = WithTalent(BloodAndThunder.TalentId);

        f.Cast<ThunderCall>();
        var procs = f.ForceProcs(key => key == BloodAndThunder.ProcKey ? true : null);

        f.Cast<HeavyStrike>();
        f.Wait(1); // the Chain Lightning hits land

        var chances = procs.ChancesOf(BloodAndThunder.ProcKey);
        using var check = new Checks(f);
        check.Equal(6, f.Damage<ChainLightning>().Count);
        check.True(f.TryCast<ChainLightning>().Succeeded);
        check.Equal(1, chances.Count);
        check.Equal(0.2, chances[0]);
    }

    /// <summary>With Blood &amp; Thunder and the proc off, a Heavy Strike casts no Chain Lightning.</summary>
    [Fact]
    public void Blood_And_Thunder_Does_Nothing_When_It_Does_Not_Proc()
    {
        var f = WithTalent(BloodAndThunder.TalentId);

        f.Cast<ThunderCall>();
        f.ForceProcs(key => key == BloodAndThunder.ProcKey ? false : null);

        f.Cast<HeavyStrike>();
        f.Wait(1); // a Chain Lightning would have landed

        Assert.Empty(f.Damage<ChainLightning>());
    }

    /// <summary>
    /// With Blood &amp; Thunder, the free Chain Lightning works without Thunder Call: with the proc forced on, a Heavy Strike with no
    /// Thunder Call gives 6 Chain Lightning hits.
    ///
    /// TODO: the tooltip does not say; the engine's free Chain Lightning needs no Thunder Call, marked unverified there (as Ace of Spades').
    /// Check in game.
    /// </summary>
    [Fact(Skip = "Whether a free Chain Lightning needs Thunder Call is not known: to check in game")]
    public void Blood_And_Thunder_Chain_Lightning_Works_Without_Thunder_Call()
    {
        var f = WithTalent(BloodAndThunder.TalentId);
        f.ForceProcs(key => key == BloodAndThunder.ProcKey ? true : null);

        f.Cast<HeavyStrike>();
        f.Wait(1); // the Chain Lightning hits land

        Assert.Equal(6, f.Damage<ChainLightning>().Count);
    }

    // ---- Bloodline (how its Fury follows haste is in TariqHasteBreakpoints.cs)

    /// <summary>
    /// With Bloodline, you generate 1 Fury every second in combat: 10.05 seconds with an enemy give 10 Fury, in gains of 1.
    /// </summary>
    [Fact]
    public void Bloodline_Generates_1_Fury_Every_Second_In_Combat()
    {
        var f = WithTalent(Bloodline.TalentId);
        f.Set(Tariq.Fury, 0);

        f.Wait(10.05);

        using var check = new Checks(f);
        check.Equal(10, f.Amount(Tariq.Fury));
        foreach (var gain in f.Gains("Fury")) check.Equal(1.0, gain.Value, what: $"the gain at {gain.Time.TotalSeconds:F3}s");
    }

    /// <summary>With Bloodline, you generate no Fury when there is no enemy to fight: 10 seconds in a fight with no enemy give no Fury.</summary>
    [Fact]
    public void Bloodline_Generates_No_Fury_Out_Of_Combat()
    {
        var f = StepFights.Tariq(enemies: 0, talents: [Bloodline.TalentId]);

        f.Wait(10);

        Assert.Equal(0, f.Amount(Tariq.Fury));
    }

    // ---- The Motherload

    /// <summary>
    /// With The Motherload, your Chain Lightning deals more damage for each unique enemy other than your target that it hits: at 3
    /// enemies, with Thunder Call, the hits of one Chain Lightning are 1, 1.5, 2, 2, 2 and 2 times the first (on the target); at 1 enemy
    /// all its hits are equal.
    ///
    /// The tooltip says +65% for each; the engine follows the combat logs, +50% for each, added per bounce (see
    /// <see cref="TheMotherload.DamagePerEnemy"/>). TODO: check in game whether the bonus is +65% as the tooltip says (the logs show about
    /// +50%), and whether the hit on a new enemy has the bonus for that enemy itself (the engine's second hit counts it: 1.5).
    /// </summary>
    [Fact(Skip = "Follows the combat logs (+50% per enemy), not the tooltip's +65%: to check against the game")]
    public void The_Motherload_Adds_50_Percent_Per_Other_Enemy_The_Arc_Hits()
    {
        var crowdScenario = WithTalent(TheMotherload.TalentId, enemies: 3);

        crowdScenario.Cast<ThunderCall>();
        crowdScenario.Cast<ChainLightning>();
        var crowdHits = crowdScenario.Damage<ChainLightning>();

        var singleScenario = WithTalent(TheMotherload.TalentId);

        singleScenario.Cast<ThunderCall>();
        singleScenario.Cast<ChainLightning>();
        var singleHits = singleScenario.Damage<ChainLightning>();

        Assert.Equal(6, crowdHits.Count);
        Assert.Equal(new[] { 1.0, 1.5, 2.0, 2.0, 2.0, 2.0 }, crowdHits.Select(h => h.Value / crowdHits[0].Value).ToArray(),
            (a, b) => Math.Abs(a - b) < RatioTolerance);
        Assert.All(singleHits, h => Assert.Equal(singleHits[0].Value, h.Value, 1e-6));
    }

    /// <summary>
    /// With Thunderstruck and The Motherload, the bonus keeps growing with the enemies the arc has hit: at 3 enemies, with Thunder Call, one
    /// Chain Lightning hits 9 times, 1, 1.5, 2 and then 2 times the first.
    ///
    /// The engine follows the combat logs, +50% for each enemy, not the tooltip's +65%. TODO: check in game whether The Motherload is +65%
    /// (the tooltip) or +50% (the logs).
    /// </summary>
    [Fact(Skip = "Follows the combat logs (+50% per enemy), not the tooltip's +65%: to check against the game")]
    public void Thunderstruck_And_The_Motherload_Add_Up_On_One_Arc()
    {
        var f = StepFights.Tariq(enemies: 3, talents: [Thunderstruck.TalentId, TheMotherload.TalentId]);
        f.Set(Tariq.Fury, 100); // enough for any spender

        f.Cast<ThunderCall>();

        f.Cast<ChainLightning>();
        var hits = f.Damage<ChainLightning>();

        Assert.Equal(9, hits.Count);
        Assert.Equal(new[] { 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0 }, hits.Select(h => h.Value / hits[0].Value).ToArray(),
            (a, b) => Math.Abs(a - b) < RatioTolerance);
    }

    // ---- Mouth for War

    /// <summary>
    /// With Mouth for War, when your Leap Smash deals damage you gain a stack of Focused Wrath: at 3 enemies, a Leap Smash gives no stack
    /// at the cast and 1 when it lands, and a second one 2 (one stack per cast, not per enemy).
    /// </summary>
    [Fact]
    public void Mouth_For_War_Gives_A_Stack_Of_Focused_Wrath_Per_Leap_Smash()
    {
        var f = WithTalent(MouthForWar.TalentId, enemies: 3);

        f.Cast<LeapSmash>();
        Assert.Equal(0, f.Stacks(FocusedWrath.Buff));
        f.Wait(0.5); // the leap lands
        Assert.Equal(1, f.Stacks(FocusedWrath.Buff));

        f.ResetCooldown<LeapSmash>(); // a second leap at once
        f.Cast<LeapSmash>();
        f.Wait(0.5); // the leap lands
        Assert.Equal(2, f.Stacks(FocusedWrath.Buff));
    }

    /// <summary>
    /// With Mouth for War, its stack of Focused Wrath makes your next spender cheaper as the ability's do: after a Leap Smash, a Skull
    /// Crusher costs 12 Fury.
    /// </summary>
    [Fact]
    public void A_Stack_From_Mouth_For_War_Makes_The_Next_Skull_Crusher_Cheaper()
    {
        var f = WithTalent(MouthForWar.TalentId);

        f.Cast<LeapSmash>();
        f.Wait(0.5); // the leap lands

        f.Cast<SkullCrusher>();

        Assert.Equal([12.0], f.Spends("Fury").Select(spend => spend.Value));
    }

    // ---- Thunderstruck

    /// <summary>
    /// With Thunderstruck, your Chain Lightning bounces 3 more times: with Thunder Call, one Chain Lightning hits 9 times and gives 10.8 Fury
    /// (1.2 a hit).
    /// </summary>
    [Fact]
    public void Thunderstruck_Adds_3_Bounces_To_Chain_Lightning()
    {
        var f = WithTalent(Thunderstruck.TalentId);
        f.Set(Tariq.Fury, 0);

        f.Cast<ThunderCall>();

        f.Cast<ChainLightning>();
        var hits = f.Damage<ChainLightning>();

        using var check = new Checks(f);
        check.Equal(9, hits.Count);
        check.Equal(10.8, f.Amount(Tariq.Fury), 1e-6);
    }

    // ---- Pneuma

    /// <summary>
    /// With Pneuma, your Culling Strike generates 1 Spirit Point, even when it spends no Fury: a Culling Strike with 20 Fury and one with
    /// none each give 1 Spirit Point.
    ///
    /// The Spirit Points Tariq regenerates over time (one every 3 seconds) are left out by reading the points just before and after the
    /// cast, which takes no time.
    /// </summary>
    [Fact]
    public void Pneuma_Gives_A_Spirit_Point_With_Each_Culling_Strike()
    {
        var f = WithTalent(Pneuma.TalentId);
        f.Set(Tariq.Fury, 20);

        double before = f.Amount(Hero.SpiritPoints);
        f.Cast<CullingStrike>();
        Assert.Equal(1, f.Amount(Hero.SpiritPoints) - before, 1e-9);

        f.Wait(6); // Culling Strike's cooldown
        f.Set(Tariq.Fury, 0);
        before = f.Amount(Hero.SpiritPoints);
        f.Cast<CullingStrike>();
        Assert.Equal(1, f.Amount(Hero.SpiritPoints) - before, 1e-9);
    }

    // ---- Sledgehammer

    /// <summary>
    /// With Sledgehammer, your Skull Crusher's direct damage deals 10% of its damage to enemies near your target, and that splash cannot
    /// crit: at 3 enemies and 50% crit, a Skull Crusher splashes the two other enemies for 10% of the target's hit (crit included), with no
    /// crit chance.
    /// </summary>
    [Fact]
    public void Sledgehammer_Deals_10_Percent_Of_Skull_Crusher_To_Enemies_Near()
    {
        var f = WithTalent(Sledgehammer.TalentId, enemies: 3, crit: 0.5).WithoutDamageSpread();

        f.Cast<SkullCrusher>();
        var hit = f.Damage<SkullCrusher>().Single();
        var splashes = f.Damage("Sledgehammer");

        Assert.Equal(2, splashes.Count);
        Assert.DoesNotContain(splashes, s => s.Target == f.Enemy.Name);
        Assert.All(splashes, s =>
        {
            AssertRatio(0.1, s.Value / hit.Value);
            Assert.Equal(0.0, s.CritChance, 1e-9);
        });
    }

    /// <summary>
    /// With Sledgehammer, only the direct damage splashes, not the thunder bolt Thunder Call adds: at 3 enemies with Thunder Call, a Skull
    /// Crusher still gives 2 Sledgehammer hits, each 10% of the physical hit.
    /// </summary>
    [Fact]
    public void Sledgehammer_Does_Not_Splash_The_Thunder_Bolt()
    {
        var f = WithTalent(Sledgehammer.TalentId, enemies: 3).WithoutDamageSpread();

        f.Cast<ThunderCall>();

        f.Cast<SkullCrusher>();
        double physicalHit = f.Damage<SkullCrusher>().First().Value;
        var splashes = f.Damage("Sledgehammer");

        Assert.Equal(2, splashes.Count);
        Assert.All(splashes, s => AssertRatio(0.1, s.Value / physicalHit));
    }

    /// <summary>
    /// Sledgehammer's splash deals less beyond 8 targets: sqrt(8 / enemies) of its damage. At 13 enemies (12 near the target) a splash is
    /// sqrt(8 / 12) times one at 9 enemies (8 near the target).
    ///
    /// TODO: whether the game counts the target in that number (the engine counts the enemies the splash hits). Check in game.
    /// </summary>
    [Fact]
    public void Sledgehammer_Falls_Off_Beyond_8_Enemies()
    {
        double Splash(int enemies)
        {
            var f = WithTalent(Sledgehammer.TalentId, enemies: enemies).WithoutDamageSpread();
            f.Cast<SkullCrusher>();
            return f.Damage("Sledgehammer")[0].Value;
        }

        AssertRatio(Math.Sqrt(8.0 / 12), Splash(13) / Splash(9));
    }

    /// <summary>
    /// With Sledgehammer, the splash is 10% of the Skull Crusher hit as it was dealt, bonuses included: at 3 enemies, with Focused Wrath and
    /// Them Bones's crit chance, each of the 2 splashes is still a tenth of the hit.
    ///
    /// Them Bones is forced on.
    /// </summary>
    [Fact]
    public void Sledgehammer_Takes_A_Tenth_Of_The_Boosted_Hit()
    {
        var f = StepFights.Tariq(enemies: 3, talents: [Sledgehammer.TalentId, ThemBones.TalentId]).WithoutDamageSpread();
        f.Set(Tariq.Fury, 100); // enough for any spender
        f.ForceProcs(key => key == ThemBones.ProcKey ? true : null);

        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();
        var hit = f.Damage<SkullCrusher>().Single();
        var splashes = f.Damage("Sledgehammer");

        Assert.Equal(2, splashes.Count);
        Assert.All(splashes, s => AssertRatio(0.1, s.Value / hit.Value));
    }

    // ---- Far Beyond Driven

    /// <summary>
    /// With Far Beyond Driven, each Heavy Strike gives you a stack for 20 seconds, each 1.5% Spirit, up to 5: a Heavy Strike gives 1.5%
    /// Spirit; with 5 stacks, another Heavy Strike leaves 5 (7.5%), 7.5% 19.9 seconds after it and none at 20.1.
    /// </summary>
    [Fact]
    public void Far_Beyond_Driven_Gives_1_5_Percent_Spirit_Per_Heavy_Strike_Up_To_5_For_20_Seconds()
    {
        var f = WithTalent(FarBeyondDriven.TalentId);

        f.Cast<HeavyStrike>();
        Assert.Equal(0.015, f.Spirit(), 1e-9);

        f.Apply(FarBeyondDriven.Buff, stacks: 5); // at the cap
        f.CastWhenReady("Heavy Strike");
        double cast = f.CompletedAt("Heavy Strike");
        Assert.Equal(5, f.Stacks(FarBeyondDriven.Buff));
        Assert.Equal(0.075, f.Spirit(), 1e-9);

        f.WaitUntil(cast + 19.9);
        Assert.Equal(0.075, f.Spirit(), 1e-9);
        f.WaitUntil(cast + 20.1);
        Assert.Equal(0.0, f.Spirit(), 1e-9);
    }

    // ---- Kill Em All

    /// <summary>
    /// With Kill Em All, your offensive abilities have a 10% chance to reset Heavy Strike's cooldown and give you 2 stacks of Kill Em All
    /// for 8 seconds: with Heavy Strike on cooldown, a Wild Swing that procs makes it ready at once and gives 2 stacks, there 7.9 seconds
    /// after the cast and gone 0.2 later.
    ///
    /// The proc is forced; its chance, 0.1, is rolled for each offensive cast, but not for Thunder Call or Focused Wrath.
    /// </summary>
    [Fact]
    public void Kill_Em_All_Resets_Heavy_Strike_And_Gives_2_Stacks_For_8_Seconds()
    {
        var f = WithTalent(KillEmAll.TalentId);
        bool proc = false;
        var procs = f.ForceProcs(key => key == KillEmAll.ProcKey ? proc : null);

        f.Cast<HeavyStrike>();
        Assert.False(f.TryCast<HeavyStrike>().Succeeded);
        f.Cast<ThunderCall>();
        f.Cast<FocusedWrath>();
        Assert.DoesNotContain(procs.Rolls.Skip(1), r => r.Key == KillEmAll.ProcKey); // the first roll is Heavy Strike's

        proc = true;
        double procAt = f.Seconds();
        f.Cast<WildSwing>();

        Assert.Equal(2, f.Stacks(KillEmAll.Buff));
        Assert.Equal(0.1, procs.ChancesOf(KillEmAll.ProcKey).Last(), 1e-9);
        Assert.Equal(0.0, f.Hero.GetCooldownRemains("Heavy Strike", f.Now), 1e-9);
        f.WaitUntil(procAt + 7.9);
        Assert.Equal(2, f.Stacks(KillEmAll.Buff));
        f.WaitUntil(procAt + 8.1);
        Assert.Equal(0, f.Stacks(KillEmAll.Buff));
    }

    /// <summary>
    /// With Kill Em All, your next Heavy Strike uses up a stack and deals 150% more damage, 2.5 times as much: with 2 stacks, two Heavy
    /// Strikes each deal 2.5 times a plain one, and a third 1 time.
    ///
    /// The talent's proc is forced off.
    /// </summary>
    [Fact]
    public void Kill_Em_All_Makes_The_Next_Two_Heavy_Strikes_Deal_150_Percent_More()
    {
        double plainDamage = PlainHit<HeavyStrike>();
        var f = WithTalent(KillEmAll.TalentId).WithoutDamageSpread();
        f.ForceProcs(key => key == KillEmAll.ProcKey ? false : null);
        f.Apply(KillEmAll.Buff, stacks: 2); // as a proc would

        f.Cast<HeavyStrike>();
        Assert.Equal(1, f.Stacks(KillEmAll.Buff));
        f.CastWhenReady("Heavy Strike");
        Assert.Equal(0, f.Stacks(KillEmAll.Buff));
        f.CastWhenReady("Heavy Strike");

        var hits = f.Ticks(HeavyStrike.TargetEffectId); // the hits on the target
        using var check = new Checks(f);
        check.Ratio(2.5, hits[0].Value / plainDamage);
        check.Ratio(2.5, hits[1].Value / plainDamage);
        check.Ratio(1.0, hits[2].Value / plainDamage);
    }

    /// <summary>
    /// While Kill Em All is active, your Heavy Strike has Cooldown Acceleration 1.6: its cooldown recovers 2.6 times as fast, so its 8
    /// seconds take 8 / 2.6. With 2 stacks, a Heavy Strike uses one, the other is still up, and the cooldown is over 8 / 2.6 seconds after
    /// the cast.
    ///
    /// The 1.6 is game data (effect 3160, cooldownAcceleration Additive on Heavy Strike); the tooltip's percentage is wrong (KillEmAll.cs).
    /// </summary>
    [Fact]
    public void Kill_Em_All_Makes_The_Heavy_Strike_Cooldown_Recover_2_6_Times_As_Fast()
    {
        var f = WithTalent(KillEmAll.TalentId);
        f.ForceProcs(key => key == KillEmAll.ProcKey ? false : null);
        f.Apply(KillEmAll.Buff, stacks: 2); // as a proc would

        f.Cast<HeavyStrike>();
        Assert.Equal(1, f.Stacks(KillEmAll.Buff));
        double end = f.CompletedAt("Heavy Strike") + 8.0 / 2.6;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<HeavyStrike>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<HeavyStrike>());
    }

    /// <summary>
    /// One Heavy Strike gives everything its talents give together: a stack of Square Hammer, a stack of Far Beyond Driven and a roll for
    /// Blood &amp; Thunder; it does not roll for Kill Em All, which only the other offensive abilities roll for.
    ///
    /// The procs are forced off. Kill Em All not rolling for Heavy Strike was confirmed in game on 2026-11-01 (see KillEmAll.cs).
    /// </summary>
    [Fact]
    public void A_Heavy_Strike_Triggers_All_Its_Talents()
    {
        var f = StepFights.Tariq(talents: [SquareHammer.TalentId, FarBeyondDriven.TalentId, KillEmAll.TalentId, BloodAndThunder.TalentId]);
        f.Set(Tariq.Fury, 100); // enough for any spender
        var procs = f.ForceProcs(key => key is KillEmAll.ProcKey or BloodAndThunder.ProcKey ? false : null);

        f.Cast<HeavyStrike>();

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(SquareHammer.Stacks));
        check.Equal(1, f.Stacks(FarBeyondDriven.Buff));
        check.Equal(0, procs.ChancesOf(KillEmAll.ProcKey).Count);
        check.Equal(1, procs.ChancesOf(BloodAndThunder.ProcKey).Count);
    }

    // ---- Ace of Spades

    /// <summary>
    /// With Ace of Spades, your Chain Lightning has 2 charges: with Thunder Call, two Chain Lightnings in a row work and a third is refused,
    /// and the first charge is back 8 seconds after the first cast.
    /// </summary>
    [Fact]
    public void Ace_Of_Spades_Gives_Chain_Lightning_2_Charges()
    {
        var f = WithTalent(AceOfSpades.TalentId);

        f.Cast<ThunderCall>();
        f.ForceProcs(key => key == AceOfSpades.ProcKey ? false : null); // no free Chain Lightning

        f.Cast<ChainLightning>();
        f.Cast<ChainLightning>();
        Assert.False(f.TryCast<ChainLightning>().Succeeded);

        f.WaitUntil(7.9);
        Assert.False(f.TryCast<ChainLightning>().Succeeded);
        f.WaitUntil(8.1);
        Assert.True(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// With Ace of Spades, each time your Skull Crusher or Hammer Storm deals damage to an enemy you have a chance (3 procs per minute) to
    /// cast a free Chain Lightning at it, which costs no charge: after 10 seconds of nothing, a Skull Crusher that procs gives 6 Chain
    /// Lightning hits, and both charges are still there.
    ///
    /// The chance is 3 x seconds since the last roll / 60: 0.5 after 10 seconds.
    /// </summary>
    [Fact]
    public void Ace_Of_Spades_Casts_A_Free_Chain_Lightning_At_3_Procs_Per_Minute()
    {
        var f = WithTalent(AceOfSpades.TalentId);
        var procs = f.ForceProcs(key => key == AceOfSpades.ProcKey ? true : null);
        f.Wait(10); // 10 seconds of nothing

        f.Cast<SkullCrusher>();
        f.Wait(1); // the Chain Lightning hits land

        var chances = procs.ChancesOf(AceOfSpades.ProcKey);
        Assert.Single(chances);
        Assert.Equal(3.0 * 10 / 60, chances[0], 1e-9);
        Assert.Equal(6, f.Damage<ChainLightning>().Count);

        f.Cast<ThunderCall>();
        f.Cast<ChainLightning>();
        Assert.True(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// With Ace of Spades, "each time your Skull Crusher and Hammer Storm deals damage to an enemy" rolls once per damage dealt: with
    /// Thunder Call a Skull Crusher deals 2 (the hit and its thunder bolt), so it rolls twice; a Hammer Storm on one enemy rolls 6 times
    /// more (3 spins and their 3 thunder bolts).
    ///
    /// The proc is forced off. TODO: the engine rolls for every hit, thunder bolts included; whether the game does is not known. Check in
    /// game.
    /// </summary>
    [Fact(Skip = "Whether Ace of Spades rolls for thunder bolts and each Hammer Storm spin separately is not known: to check in game")]
    public void Ace_Of_Spades_Rolls_For_Every_Damage_Dealt_By_Skull_Crusher_And_Hammer_Storm()
    {
        var f = WithTalent(AceOfSpades.TalentId);
        var procs = f.ForceProcs(key => key == AceOfSpades.ProcKey ? false : null);

        f.Cast<ThunderCall>();
        f.Cast<SkullCrusher>();
        Assert.Equal(2, procs.ChancesOf(AceOfSpades.ProcKey).Count);

        f.Cast<HammerStorm>();
        f.Wait(1.5); // the spins are over

        Assert.Equal(2 + 6, procs.ChancesOf(AceOfSpades.ProcKey).Count);
    }

    /// <summary>
    /// With Ace of Spades, the free Chain Lightning is a full one, with Thunderstruck's bounces too: after 10 seconds, a Skull Crusher
    /// that procs gives 9 Chain Lightning hits.
    ///
    /// The proc is forced on. With no Thunder Call, the Skull Crusher deals one hit and rolls once. TODO: this relies on the free Chain
    /// Lightning working without Thunder Call (see <see cref="Blood_And_Thunder_Chain_Lightning_Works_Without_Thunder_Call"/>). Check in
    /// game.
    /// </summary>
    [Fact]
    public void Ace_Of_Spades_Free_Chain_Lightning_Has_Thunderstruck_Bounces()
    {
        var f = StepFights.Tariq(talents: [AceOfSpades.TalentId, Thunderstruck.TalentId]);
        f.Set(Tariq.Fury, 100); // enough for any spender
        f.ForceProcs(key => key == AceOfSpades.ProcKey ? true : null);
        f.Wait(10); // 10 seconds of nothing

        f.Cast<SkullCrusher>();
        f.Wait(1); // the Chain Lightning hits land

        Assert.Equal(9, f.Damage<ChainLightning>().Count);
    }

    // ---- Schism

    /// <summary>
    /// With Schism, your Skull Crusher has a 35% chance to make your next Hammer Storm deal 150% more damage, and your Hammer Storm the same
    /// for your next Skull Crusher: with each proc forced on, a Skull Crusher then a Hammer Storm whose first spin is 2.5 times a plain
    /// one, then a Skull Crusher 2.5 times a plain one.
    ///
    /// The proc is rolled once per cast; its chance is in <see cref="Schism_Has_Bad_Luck_Protection"/>.
    /// </summary>
    [Fact]
    public void Schism_Makes_The_Next_Other_Spender_Deal_150_Percent_More()
    {
        double plainStormDamage = PlainHit<HammerStorm>();
        double plainCrusherDamage = PlainHit<SkullCrusher>();
        var f = WithTalent(Schism.TalentId).WithoutDamageSpread();
        f.Set(Tariq.Fury, 200); // enough for the three spenders
        var procs = f.ForceProcs(key => key is Schism.SkullCrusherProcKey or Schism.HammerStormProcKey ? true : null);

        f.Cast<SkullCrusher>();
        Assert.Equal(1, f.Stacks(Schism.HammerStormBuff));
        f.Cast<HammerStorm>();
        Assert.Equal(1, f.Stacks(Schism.SkullCrusherBuff));
        f.Cast<SkullCrusher>();

        using var check = new Checks(f);
        check.Ratio(2.5, f.Damage<HammerStorm>()[0].Value / plainStormDamage);
        check.Ratio(2.5, f.Damage<SkullCrusher>()[1].Value / plainCrusherDamage);
        check.Equal(3, procs.ChancesOf(Schism.SkullCrusherProcKey).Count + procs.ChancesOf(Schism.HammerStormProcKey).Count);
    }

    /// <summary>
    /// Schism's 35% chance has bad luck protection: it starts lower, grows with every roll that fails and starts again after a proc, for an
    /// average of 35%. Four Skull Crushers whose roll fails, fails, procs and fails roll at 1, 2, 3 and 1 times the step.
    ///
    /// The step is the one whose average chance is 35% (BadLuckProtection.StepFor(0.35), about 0.158). It is UNVERIFIED in the engine (the
    /// game data has none).
    /// </summary>
    [Fact]
    public void Schism_Has_Bad_Luck_Protection()
    {
        double step = BadLuckProtection.StepFor(0.35);
        var f = WithTalent(Schism.TalentId);
        int roll = 0;
        var procs = f.ForceProcs(key => key == Schism.HammerStormProcKey ? ++roll == 3 : null); // the third roll procs

        for (int i = 0; i < 4; i++)
        {
            f.Set(Tariq.Fury, 100); // enough for the next one
            f.CastWhenReady("Skull Crusher");
        }

        var chances = procs.ChancesOf(Schism.HammerStormProcKey);
        using var check = new Checks(f);
        check.Equal(4, chances.Count);
        check.Equal(step, chances[0]);
        check.Equal(2 * step, chances[1]);
        check.Equal(3 * step, chances[2]);
        check.Equal(step, chances[3]);
    }

    /// <summary>With Schism, you can have up to 2 stacks of each Schism: with the proc forced on, 3 Skull Crushers give Hammer Storm's
    /// Schism 2 stacks.</summary>
    [Fact]
    public void Schism_Stacks_Up_To_2()
    {
        var f = WithTalent(Schism.TalentId);
        f.ForceProcs(key => key == Schism.HammerStormProcKey ? true : null);

        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();

        Assert.Equal(2, f.Stacks(Schism.HammerStormBuff));
    }

    /// <summary>
    /// With Schism, a Skull Crusher never makes the next Skull Crusher stronger, nor a Hammer Storm the next Hammer Storm: with both procs
    /// forced on, a Skull Crusher gives Schism for Hammer Storm only.
    /// </summary>
    [Fact]
    public void Schism_Does_Not_Boost_The_Same_Spender()
    {
        var f = WithTalent(Schism.TalentId);
        f.ForceProcs(key => key is Schism.SkullCrusherProcKey or Schism.HammerStormProcKey ? true : null);

        f.Cast<SkullCrusher>();

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(Schism.SkullCrusherBuff));
        check.Equal(1, f.Stacks(Schism.HammerStormBuff));
    }

    /// <summary>
    /// With Schism, the damage bonuses on one Skull Crusher multiply: with Schism (+150%) and Focused Wrath (+10%), a Skull Crusher deals
    /// 1.1 x 2.5 = 2.75 times a plain one, uses up the Schism stack and one of Focused Wrath's two.
    ///
    /// Schism's procs are forced off.
    /// </summary>
    [Fact]
    public void Focused_Wrath_And_Schism_Multiply_On_One_Skull_Crusher()
    {
        double plainDamage = PlainHit<SkullCrusher>();
        var f = WithTalent(Schism.TalentId).WithoutDamageSpread();
        f.ForceProcs(key => key is Schism.SkullCrusherProcKey or Schism.HammerStormProcKey ? false : null);
        f.Apply(Schism.SkullCrusherBuff); // as a Hammer Storm's proc would

        f.Cast<FocusedWrath>();

        f.Cast<SkullCrusher>();

        using var check = new Checks(f);
        check.Ratio(1.1 * 2.5, f.Damage<SkullCrusher>().First().Value / plainDamage);
        check.Equal(0, f.Stacks(Schism.SkullCrusherBuff));
        check.Equal(1, f.Stacks(FocusedWrath.Buff));
    }

    /// <summary>
    /// With Schism, the bonus lasts a whole Hammer Storm: all 3 spins deal 2.5 times those of a plain one, and the stack is used up.
    ///
    /// Schism's procs are forced off.
    /// </summary>
    [Fact]
    public void Schism_Boosts_All_Three_Spins_Of_Hammer_Storm()
    {
        var plainScenario = StepFights.Tariq().WithoutDamageSpread();
        plainScenario.Set(Tariq.Fury, 100); // enough to cast it

        plainScenario.Cast<HammerStorm>();
        plainScenario.Wait(1.5); // the spins are over
        var plainSpins = plainScenario.Damage<HammerStorm>();

        var schismScenario = StepFights.Tariq(talents: [Schism.TalentId]).WithoutDamageSpread();
        schismScenario.Set(Tariq.Fury, 100); // enough to cast it
        schismScenario.ForceProcs(key => key is Schism.SkullCrusherProcKey or Schism.HammerStormProcKey ? false : null);
        schismScenario.Apply(Schism.HammerStormBuff); // as a Skull Crusher's proc would

        schismScenario.Cast<HammerStorm>();
        schismScenario.Wait(1.5); // the spins are over
        var schismSpins = schismScenario.Damage<HammerStorm>();

        using var check = new Checks(schismScenario);
        for (int i = 0; i < 3; i++) check.Ratio(2.5, schismSpins[i].Value / plainSpins[i].Value, what: $"spin {i + 1} over the plain one");
        check.Equal(0, schismScenario.Stacks(Schism.HammerStormBuff));
    }

    // ---- Them Bones

    /// <summary>
    /// With Them Bones, your Skull Crusher has a 30% chance to have 100% more crit chance, for the physical hit and the thunder bolt of
    /// Thunder Call alike: at 0% crit, with Thunder Call, a Skull Crusher that procs has a crit chance of 1 on both hits.
    ///
    /// The proc is forced on; its chance, 0.3, is rolled once per cast.
    /// </summary>
    [Fact]
    public void Them_Bones_Adds_100_Percent_Crit_Chance_To_Skull_Crusher_And_Its_Thunder_Bolt()
    {
        var f = WithTalent(ThemBones.TalentId);

        f.Cast<ThunderCall>();
        var procs = f.ForceProcs(key => key == ThemBones.ProcKey ? true : null);

        f.Cast<SkullCrusher>();

        var chances = procs.ChancesOf(ThemBones.ProcKey);
        using var check = new Checks(f);
        check.Equal(1.0, f.Damage<SkullCrusher>().Single().CritChance);
        check.Equal(1.0, f.Damage("Thunder Call: Skull Crusher").Single().CritChance);
        check.Equal(1, chances.Count);
        check.Equal(0.3, chances[0]);
    }

    /// <summary>
    /// With Them Bones, without the proc Skull Crusher has your own crit chance, and the other abilities never have the bonus: at 10% crit,
    /// a Skull Crusher with the proc off has a crit chance of 0.1, and a Face Breaker with the proc on 0.1.
    /// </summary>
    [Fact]
    public void Them_Bones_Only_Affects_Skull_Crusher_When_It_Procs()
    {
        var f = WithTalent(ThemBones.TalentId, crit: 0.1);
        bool proc = false;
        f.ForceProcs(key => key == ThemBones.ProcKey ? proc : null);

        f.Cast<SkullCrusher>();
        proc = true;
        f.Cast<FaceBreaker>();

        using var check = new Checks(f);
        check.Equal(0.1, f.Damage<SkullCrusher>().Single().CritChance);
        check.Equal(0.1, f.Damage<FaceBreaker>().First().CritChance);
    }

    // ---- Crack the Sky

    /// <summary>
    /// With Crack the Sky, your lightning damage has 20% more crit chance: the thunder bolts and lightning of Thunder Call, Chain
    /// Lightning, Raging Currents and Raging Tempest's leap, and not your physical hits. At 10% crit, each lightning hit has a crit chance
    /// of 0.3, and the physical hits of the same casts 0.1.
    ///
    /// Thunder Call: Heavy Strike is the exception, in <see cref="Thunder_Call_Heavy_Strike_Does_Not_Gain_Crack_The_Sky"/>.
    /// </summary>
    [Theory]
    [InlineData("Thunder Call: Skull Crusher")]
    [InlineData("Thunder Call: Hammer Storm")]
    [InlineData("Chain Lightning")]
    [InlineData("Raging Currents")]
    [InlineData("Raging Tempest")]
    public void Crack_The_Sky_Adds_20_Percent_Crit_Chance_To_Lightning_Damage(string lightning)
    {
        var f = WithTalent(CrackTheSky.TalentId, crit: 0.1);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Raging Tempest

        if (lightning is "Raging Currents" or "Raging Tempest") f.Cast<RagingTempest>();
        else f.Cast<ThunderCall>();
        f.Cast<SkullCrusher>();
        f.Cast<HeavyStrike>();
        f.Cast<HammerStorm>();
        f.Cast<ChainLightning>();
        f.Wait(2); // every hit lands
        var hits = f.Damage(lightning);

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal(0.3, h.CritChance, 1e-9));
        Assert.All(f.Damage<SkullCrusher>(), h => Assert.Equal(0.1, h.CritChance, 1e-9));
        Assert.All(f.Damage<HammerStorm>(), h => Assert.Equal(0.1, h.CritChance, 1e-9));
    }

    /// <summary>
    /// With Crack the Sky, Thunder Call's Heavy Strike is lightning, so it should gain the 20% crit chance too.
    ///
    /// The engine follows the combat logs, where Heavy Strike's lightning crits as often as the same casts' physical hits (a developer bug
    /// the engine models until the game fixes it): at 10% crit the hit has a crit chance of 0.1, as without the talent. TODO: check in
    /// game whether Heavy Strike gains Crack the Sky's crit chance.
    /// </summary>
    [Fact(Skip = "Follows the combat logs (Heavy Strike's lightning gets no Crack the Sky), against the tooltip: to check against the game")]
    public void Thunder_Call_Heavy_Strike_Does_Not_Gain_Crack_The_Sky()
    {
        var f = WithTalent(CrackTheSky.TalentId, crit: 0.1);
        f.Set(Hero.SpiritPoints, 100); // as in the theory above

        f.Cast<ThunderCall>();
        f.Cast<HeavyStrike>();
        f.Wait(2); // every hit lands
        var hits = f.Damage("Thunder Call: Heavy Strike");

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal(0.1, h.CritChance, 1e-9));
    }

    // ---- Killing in the Name

    /// <summary>
    /// With Killing in the Name, your Culling Strike has 50% more crit chance: at 10% crit, a Culling Strike has a crit chance of 0.6 and a
    /// Skull Crusher 0.1.
    /// </summary>
    [Fact]
    public void Killing_In_The_Name_Gives_Culling_Strike_50_Percent_Crit_Chance()
    {
        var f = WithTalent(KillingInTheName.TalentId, crit: 0.1);

        f.Cast<CullingStrike>();
        f.Cast<SkullCrusher>();

        using var check = new Checks(f);
        check.Equal(0.6, f.Damage<CullingStrike>()[0].CritChance);
        check.Equal(0.1, f.Damage<SkullCrusher>()[0].CritChance);
    }
}
