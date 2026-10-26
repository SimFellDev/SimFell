using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using TariqTalents = FellowshipSim.Core.Heroes.Tariq.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Each of Tariq's 18 talents on its own: Left Hand Path, Ride the Lightning, Square Hammer, High Road, Blood &amp; Thunder, Bloodline, The Motherload,
/// Mouth for War, Thunderstruck, Pneuma, Sledgehammer, Far Beyond Driven, Kill Em All, Ace of Spades, Schism, Them Bones, Crack the Sky and Killing in
/// the Name.
/// Dev: the talent is taken alone. Chances are tested through <c>ForceProcs</c> (the outcome forced, the chance read from <c>Rolls</c>). Talents no combat log
/// has shown are marked unverified in the engine; the tests follow the tooltips. Hits are compared with a Wild Swing hit (129.14% of Strength) where
/// an absolute number is needed.
/// </summary>
public class TariqTalentsSimple
{
    private const double Ratio = 0.001;

    private static StepDriver With(int talent, double haste = 0, int enemies = 1, double crit = 0, double spirit = 0)
    {
        var f = StepFights.Tariq(haste, enemies, [talent], crit: crit, spirit: spirit);
        f.SetFury(100);
        f.SetHealth(0.2);
        return f;
    }

    private static double ReadyAt(StepDriver f, Ability ability) =>
        f.Now.TotalSeconds + f.Hero.GetCooldownRemains(ability.Name, f.Now);

    /// <summary>Lets time pass until <paramref name="seconds"/> after the fight clock read <paramref name="from"/> (a cast returns when its global cooldown is over, not at its start).</summary>
    private static void WaitUntil(StepDriver f, double from, double seconds) => f.Wait(from + seconds - f.Now.TotalSeconds);

    /// <summary>Lets the cooldown of an ability run out.</summary>
    private static void WaitForCooldown(StepDriver f, string ability) => f.Wait(f.Hero.GetCooldownRemains(ability, f.Now) + 0.001);

    /// <summary>The first hit of an ability in a fight with the given talents (or none): what a talent's multiplier is compared against.</summary>
    private static double Plain(Action<StepDriver> act, string ability)
    {
        var f = StepFights.Tariq();
        f.SetFury(100);
        f.SetHealth(0.2);
        act(f);
        return f.FirstDamageOf(ability);
    }

    // ---- Left Hand Path

    /// <summary>
    /// Player (Left Hand Path): your Face Breaker has +40% increased critical strike chance.
    /// Dev: 10% crit; a Face Breaker has a crit chance of 0.5, a Skull Crusher 0.1.
    /// </summary>
    [Fact]
    public void Left_Hand_Path_Gives_Face_Breaker_40_Percent_Crit_Chance()
    {
        var f = With(TariqTalents.LeftHandPath.TalentId, crit: 0.1);

        f.Cast<FaceBreaker>();
        f.Cast<SkullCrusher>();

        Assert.Equal(0.5, f.Damage("Face Breaker")[0].CritChance, 1e-9);
        Assert.Equal(0.1, f.Damage("Skull Crusher")[0].CritChance, 1e-9);
    }

    // ---- Ride the Lightning

    /// <summary>
    /// Player (Ride the Lightning): your Thunder Call generates 30 Fury over its duration and grants you +5% Haste while it is active.
    /// Dev: Thunder Call at 0% haste; 5% haste at 20.9s and none at 21.1s.
    /// </summary>
    [Fact]
    public void Ride_The_Lightning_Gives_5_Percent_Haste_During_Thunder_Call()
    {
        var f = With(TariqTalents.RideTheLightning.TalentId);
        f.Cast<ThunderCall>();

        f.Wait(20.9);
        Assert.Equal(0.05, f.Hero.GetHaste(f.Now), 1e-9);

        f.Wait(0.2);
        Assert.Equal(0.0, f.Hero.GetHaste(f.Now), 1e-9);
    }

    /// <summary>
    /// Player (Ride the Lightning): Thunder Call generates 30 Fury over its 21 seconds, and no more once it ends.
    /// Dev: Thunder Call at 0% haste with no Fury; at 21.1s Fury is between 30 and 31.7 (30 if its ticks ignore the talent's own 5% haste, 31.5 plus a tick or
    /// two if they follow it); 4 seconds later it has not changed.
    /// TODO: which of the two the game does (the engine's ticks follow haste, as the logs showed, including the talent's 5%?). Check in game.
    /// </summary>
    [Fact]
    public void Ride_The_Lightning_Generates_About_30_Fury_Over_Thunder_Call()
    {
        var f = StepFights.Tariq(talents: [TariqTalents.RideTheLightning.TalentId]);
        f.Cast<ThunderCall>();

        f.Wait(21.1);
        double after = f.Fury();
        f.Wait(4);

        Assert.InRange(after, 30.0, 31.7);
        Assert.Equal(after, f.Fury(), 1e-9);
    }

    /// <summary>
    /// Player (Ride the Lightning): Fury also comes during Raging Tempest's Enhanced Thunder Call, and stops when it ends.
    /// Dev: Raging Tempest; at 5s there is Fury; at 20.1s and 25s it is the same.
    /// </summary>
    [Fact]
    public void Ride_The_Lightning_Generates_Fury_During_Raging_Tempest()
    {
        var f = StepFights.Tariq(talents: [TariqTalents.RideTheLightning.TalentId]);
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();

        f.Wait(5);
        Assert.True(f.Fury() > 5);

        f.Wait(15.1);
        double end = f.Fury();
        f.Wait(5);
        Assert.Equal(end, f.Fury(), 1e-9);
    }

    // ---- Square Hammer

    /// <summary>
    /// Player (Square Hammer): your Heavy Strike grants you 1 stack of Square Hammer for 20 seconds, stacking up to 5 times.
    /// Dev: a Heavy Strike: 1 stack; with 5 stacks set, another Heavy Strike (after its cooldown) leaves 5; they are there 19.9s after it and gone at 20.1s.
    /// </summary>
    [Fact]
    public void Square_Hammer_Stacks_With_Heavy_Strike_Up_To_5_For_20_Seconds()
    {
        var f = With(TariqTalents.SquareHammer.TalentId);
        f.Cast<HeavyStrike>();
        Assert.Equal(1, f.Stacks(TariqTalents.SquareHammer.Stacks));

        f.Hero.Auras.Apply(TariqTalents.SquareHammer.Stacks, f.Now, setStacks: 5);
        WaitForCooldown(f, "Heavy Strike");
        double cast = f.Now.TotalSeconds;
        f.Cast<HeavyStrike>();
        Assert.Equal(5, f.Stacks(TariqTalents.SquareHammer.Stacks));

        WaitUntil(f, cast, 19.9);
        Assert.Equal(5, f.Stacks(TariqTalents.SquareHammer.Stacks));
        f.Wait(0.2);
        Assert.Equal(0, f.Stacks(TariqTalents.SquareHammer.Stacks));
    }

    /// <summary>
    /// Player (Square Hammer): your next Skull Crusher or Hammer Storm consumes all stacks, reducing the cooldown of your Thunder Call by 1 second per
    /// stack.
    /// Dev: Thunder Call (45s cooldown) at the start; 3 stacks set; a Skull Crusher (1s) or a Hammer Storm (1.5s): no stacks left, and Thunder Call is
    /// ready at 42s.
    /// </summary>
    [Theory]
    [InlineData("Skull Crusher")]
    [InlineData("Hammer Storm")]
    public void Square_Hammer_Is_Consumed_By_The_Next_Spender_And_Shortens_Thunder_Call(string spender)
    {
        var f = With(TariqTalents.SquareHammer.TalentId);
        f.Cast<ThunderCall>();
        f.Hero.Auras.Apply(TariqTalents.SquareHammer.Stacks, f.Now, setStacks: 3);

        f.Cast(f.Hero.Abilities.First(a => a.Name == spender));

        Assert.Equal(0, f.Stacks(TariqTalents.SquareHammer.Stacks));
        Assert.Equal(42.0, ReadyAt(f, f.Ability<ThunderCall>()), 0.002);
    }

    /// <summary>
    /// Player (Square Hammer): each time you consume Square Hammer you are granted +8% Expertise for 4 seconds, which the spender that consumed it
    /// already has.
    /// Dev: 2 stacks set; a Skull Crusher: 8% Expertise at the cast and at 3.9s, none at 4.1s; its hit is 1.08 times one without the talent's stacks.
    /// </summary>
    [Fact]
    public void Square_Hammer_Gives_8_Percent_Expertise_For_4_Seconds_When_Consumed()
    {
        double plain = Plain(f => f.Cast<SkullCrusher>(), "Skull Crusher");
        var f = With(TariqTalents.SquareHammer.TalentId);
        f.Hero.Auras.Apply(TariqTalents.SquareHammer.Stacks, f.Now, setStacks: 2);
        Assert.Equal(0.0, f.Hero.Expertise(f.Now), 1e-9);

        f.Cast<SkullCrusher>();

        Assert.Equal(1.08, f.FirstDamageOf("Skull Crusher") / plain, Ratio);
        Assert.Equal(0.08, f.Hero.Expertise(f.Now), 1e-9);
        f.Wait(2.9);
        Assert.Equal(0.08, f.Hero.Expertise(f.Now), 1e-9);
        f.Wait(0.2);
        Assert.Equal(0.0, f.Hero.Expertise(f.Now), 1e-9);
    }

    /// <summary>
    /// Player (Square Hammer): with no stacks, a Skull Crusher gives no Expertise and does not touch Thunder Call's cooldown.
    /// Dev: Thunder Call, then a Skull Crusher with no stacks; no Expertise, Thunder Call ready at 45s.
    /// </summary>
    [Fact]
    public void Square_Hammer_Does_Nothing_Without_Stacks()
    {
        var f = With(TariqTalents.SquareHammer.TalentId);
        f.Cast<ThunderCall>();

        f.Cast<SkullCrusher>();

        Assert.Equal(0.0, f.Hero.Expertise(f.Now), 1e-9);
        Assert.Equal(45.0, ReadyAt(f, f.Ability<ThunderCall>()), 0.002);
    }

    // ---- High Road

    /// <summary>
    /// Player (High Road): when your Leap Smash hits at least 1 enemy, you gain 20 Fury.
    /// Dev: one Leap Smash at 3 enemies; the Fury comes with the hit, 0.3s later, once: 20.
    /// </summary>
    [Fact]
    public void High_Road_Gives_20_Fury_When_Leap_Smash_Hits()
    {
        var f = With(TariqTalents.HighRoad.TalentId, enemies: 3);
        f.SetFury(0);

        f.Cast<LeapSmash>();
        Assert.Equal(0, f.Fury());
        f.Wait(0.5);

        Assert.Equal([20.0], f.FuryGained("Fury"));
    }

    /// <summary>
    /// Player (High Road): when your Leap Smash does not hit an enemy, its cooldown is reduced by 10 seconds.
    /// Dev: the only enemy is put out of the fight (it stands for "no enemy near the landing spot"); one Leap Smash; it is ready at 10s (20s - 10s), and no
    /// Fury was gained.
    /// </summary>
    [Fact]
    public void High_Road_Shortens_Leap_Smash_By_10_Seconds_When_It_Hits_Nothing()
    {
        var f = With(TariqTalents.HighRoad.TalentId);
        f.SetFury(0);
        f.Enemy.Demise(f.Context);

        f.Cast<LeapSmash>();
        f.Wait(0.5);

        Assert.Equal(10.0, ReadyAt(f, f.Ability<LeapSmash>()), 0.002);
        Assert.Empty(f.FuryGained("Fury"));
    }

    // ---- Blood & Thunder

    /// <summary>
    /// Player (Blood &amp; Thunder): your Heavy Strike has a 20% chance to cast a free Chain Lightning at the target.
    /// Dev: the proc forced on; Thunder Call; a Heavy Strike; 6 Chain Lightning hits, on the target; Chain Lightning is not on cooldown (a cast works at
    /// once). The chance is 0.2, rolled once per Heavy Strike.
    /// </summary>
    [Fact]
    public void Blood_And_Thunder_Casts_A_Free_Chain_Lightning_With_Heavy_Strike()
    {
        var f = With(TariqTalents.BloodAndThunder.TalentId);
        f.Cast<ThunderCall>();
        var procs = f.ForceProcs(key => key == TariqTalents.BloodAndThunder.ProcKey ? true : null);

        f.Cast<HeavyStrike>();
        f.Wait(1);

        Assert.Equal(6, f.Damage("Chain Lightning").Count);
        Assert.True(f.TryCast<ChainLightning>().Succeeded);
        var rolls = procs.Rolls.Where(r => r.Key == TariqTalents.BloodAndThunder.ProcKey).ToList();
        Assert.Single(rolls);
        Assert.Equal(0.2, rolls[0].Chance, 1e-9);
    }

    /// <summary>
    /// Player (Blood &amp; Thunder): with the proc off, a Heavy Strike casts no Chain Lightning.
    /// Dev: the proc forced off; a Heavy Strike; no Chain Lightning hits.
    /// </summary>
    [Fact]
    public void Blood_And_Thunder_Does_Nothing_When_It_Does_Not_Proc()
    {
        var f = With(TariqTalents.BloodAndThunder.TalentId);
        f.Cast<ThunderCall>();
        f.ForceProcs(key => key == TariqTalents.BloodAndThunder.ProcKey ? false : null);

        f.Cast<HeavyStrike>();
        f.Wait(1);

        Assert.Empty(f.Damage("Chain Lightning"));
    }

    /// <summary>
    /// Player (Blood &amp; Thunder): the free Chain Lightning works without Thunder Call.
    /// Dev: the proc forced on; a Heavy Strike with no Thunder Call; 6 Chain Lightning hits.
    /// TODO: the tooltip does not say; the engine's free Chain Lightning needs no Thunder Call, marked unverified there (as Ace of Spades'). Check in game.
    /// </summary>
    [Fact(Skip = "Whether a free Chain Lightning needs Thunder Call is not known: to check in game")]
    public void Blood_And_Thunder_Chain_Lightning_Works_Without_Thunder_Call()
    {
        var f = With(TariqTalents.BloodAndThunder.TalentId);
        f.ForceProcs(key => key == TariqTalents.BloodAndThunder.ProcKey ? true : null);

        f.Cast<HeavyStrike>();
        f.Wait(1);

        Assert.Equal(6, f.Damage("Chain Lightning").Count);
    }

    // ---- Bloodline

    /// <summary>
    /// Player (Bloodline): you generate 1 Fury every second in combat.
    /// Dev: 10.05 seconds with an enemy: 10 Fury, in gains of 1.
    /// </summary>
    [Fact]
    public void Bloodline_Generates_1_Fury_Every_Second_In_Combat()
    {
        var f = With(TariqTalents.Bloodline.TalentId);
        f.SetFury(0);

        f.Wait(10.05);

        Assert.Equal(10, f.Fury(), 1e-9);
        Assert.All(f.FuryGained("Fury"), g => Assert.Equal(1.0, g, 1e-9));
    }

    /// <summary>
    /// Player (Bloodline): you generate no Fury when there is no enemy to fight.
    /// Dev: a fight with no enemy; 10 seconds; no Fury.
    /// </summary>
    [Fact]
    public void Bloodline_Generates_No_Fury_Out_Of_Combat()
    {
        var f = StepFights.Tariq(enemies: 0, talents: [TariqTalents.Bloodline.TalentId]);

        f.Wait(10);

        Assert.Equal(0, f.Fury());
    }

    // ---- The Motherload

    /// <summary>
    /// Player (The Motherload): your Chain Lightning deals +65% increased damage for each unique enemy other than your target that it hits.
    /// Dev: 3 enemies; Thunder Call; one Chain Lightning; the hits over the first (on the target): 1, 1.65, 2.3 and the rest 2.3; with one enemy all hits
    /// are equal.
    /// TODO: whether the hit on a new enemy has the bonus for that enemy itself (the engine's second hit counts it: 1.65). Check in game.
    /// </summary>
    [Fact]
    public void The_Motherload_Adds_65_Percent_Per_Other_Enemy_The_Arc_Hits()
    {
        var f = With(TariqTalents.TheMotherload.TalentId, enemies: 3);
        f.Cast<ThunderCall>();
        f.Cast<ChainLightning>();

        var hits = f.Damage("Chain Lightning");
        Assert.Equal(6, hits.Count);
        Assert.Equal(new[] { 1.0, 1.65, 2.3, 2.3, 2.3, 2.3 }, hits.Select(h => h.Value / hits[0].Value).ToArray(), (a, b) => Math.Abs(a - b) < Ratio);

        var single = With(TariqTalents.TheMotherload.TalentId);
        single.Cast<ThunderCall>();
        single.Cast<ChainLightning>();
        Assert.All(single.Damage("Chain Lightning"), h => Assert.Equal(single.Damage("Chain Lightning")[0].Value, h.Value, 1e-6));
    }

    // ---- Mouth for War

    /// <summary>
    /// Player (Mouth for War): when your Leap Smash deals damage, you are granted 1 stack of Focused Wrath.
    /// Dev: a Leap Smash: no stack at the cast, 1 at 0.5s; a second one (its cooldown reset): 2 (one stack per cast, not per enemy: 3 enemies).
    /// </summary>
    [Fact]
    public void Mouth_For_War_Gives_A_Stack_Of_Focused_Wrath_Per_Leap_Smash()
    {
        var f = With(TariqTalents.MouthForWar.TalentId, enemies: 3);

        f.Cast<LeapSmash>();
        Assert.Equal(0, f.Stacks(FocusedWrath.Buff));
        f.Wait(0.5);
        Assert.Equal(1, f.Stacks(FocusedWrath.Buff));

        f.Hero.CooldownOf(f.Ability<LeapSmash>()).ResetAll();
        f.Cast<LeapSmash>();
        f.Wait(0.5);
        Assert.Equal(2, f.Stacks(FocusedWrath.Buff));
    }

    // ---- Thunderstruck

    /// <summary>
    /// Player (Thunderstruck): your Chain Lightning bounces 3 additional times: 9 hits.
    /// Dev: Thunder Call; one Chain Lightning; 9 hits, the last at 0.30 + 8 x 0.05 = 0.70s from the cast, and 10.8 Fury (1.2 a hit).
    /// </summary>
    [Fact]
    public void Thunderstruck_Adds_3_Bounces_To_Chain_Lightning()
    {
        var f = With(TariqTalents.Thunderstruck.TalentId);
        f.SetFury(0);
        f.Cast<ThunderCall>();
        var start = f.Now.TotalSeconds;

        f.Cast<ChainLightning>();

        var hits = f.Damage("Chain Lightning");
        Assert.Equal(9, hits.Count);
        Assert.Equal(start + 0.70, hits[8].Time.TotalSeconds, 0.02);
        Assert.Equal(10.8, f.Fury(), 1e-6);
    }

    // ---- Pneuma

    /// <summary>
    /// Player (Pneuma): your Culling Strike generates 1 Spirit Point, even when it spends no Fury.
    /// Dev: two Culling Strikes, one with 20 Fury and one with none; each gives 1 Spirit Point (the Spirit Points Tariq regenerates over time, one every 3
    /// seconds, are taken out by reading the points just before and after the cast, which takes no time).
    /// </summary>
    [Fact]
    public void Pneuma_Gives_A_Spirit_Point_With_Each_Culling_Strike()
    {
        var f = With(TariqTalents.Pneuma.TalentId);
        f.SetFury(20);

        double before = f.Hero.ResourceAmount(Hero.SpiritPoints);
        f.Cast<CullingStrike>();
        Assert.Equal(1, f.Hero.ResourceAmount(Hero.SpiritPoints) - before, 1e-9);

        f.Wait(6);
        f.SetFury(0);
        before = f.Hero.ResourceAmount(Hero.SpiritPoints);
        f.Cast<CullingStrike>();
        Assert.Equal(1, f.Hero.ResourceAmount(Hero.SpiritPoints) - before, 1e-9);
    }

    // ---- Sledgehammer

    /// <summary>
    /// Player (Sledgehammer): your Skull Crusher direct damage deals 10% of the damage dealt to enemies near your target. That splash cannot crit.
    /// Dev: 3 enemies, 50% crit; a Skull Crusher; the two other enemies take 10% of the target's hit (crit included), with no crit chance.
    /// </summary>
    [Fact]
    public void Sledgehammer_Deals_10_Percent_Of_Skull_Crusher_To_Enemies_Near()
    {
        var f = With(TariqTalents.Sledgehammer.TalentId, enemies: 3, crit: 0.5);

        f.Cast<SkullCrusher>();

        var hit = f.Damage("Skull Crusher").Single();
        var splashes = f.Damage("Sledgehammer");
        Assert.Equal(2, splashes.Count);
        Assert.DoesNotContain(splashes, s => s.Target == f.Enemy.Name);
        Assert.All(splashes, s =>
        {
            Assert.Equal(0.1, s.Value / hit.Value, Ratio);
            Assert.Equal(0.0, s.CritChance, 1e-9);
        });
    }

    /// <summary>
    /// Player (Sledgehammer): only the direct damage splashes: not the thunder bolt Thunder Call adds.
    /// Dev: 3 enemies; Thunder Call; a Skull Crusher; still 2 Sledgehammer hits, each 10% of the physical hit.
    /// </summary>
    [Fact]
    public void Sledgehammer_Does_Not_Splash_The_Thunder_Bolt()
    {
        var f = With(TariqTalents.Sledgehammer.TalentId, enemies: 3);
        f.Cast<ThunderCall>();

        f.Cast<SkullCrusher>();

        var splashes = f.Damage("Sledgehammer");
        Assert.Equal(2, splashes.Count);
        Assert.All(splashes, s => Assert.Equal(0.1, s.Value / f.FirstDamageOf("Skull Crusher"), Ratio));
    }

    /// <summary>
    /// Player (Sledgehammer): damage is reduced beyond 8 targets: each splash is multiplied by sqrt(8 / enemies).
    /// Dev: 9 and 13 enemies (8 and 12 near the target); one cast; a splash of each, in the ratio of the square roots.
    /// TODO: whether the game counts the target in that number (the engine counts the enemies the splash hits). Check in game.
    /// </summary>
    [Fact]
    public void Sledgehammer_Falls_Off_Beyond_8_Enemies()
    {
        double Splash(int enemies)
        {
            var f = With(TariqTalents.Sledgehammer.TalentId, enemies: enemies);
            f.Cast<SkullCrusher>();
            return f.Damage("Sledgehammer")[0].Value;
        }

        Assert.Equal(Math.Sqrt(8.0 / 12), Splash(13) / Splash(9), Ratio);
    }

    // ---- Far Beyond Driven

    /// <summary>
    /// Player (Far Beyond Driven): each time you use Heavy Strike you gain 1 stack of Far Beyond Driven for 20 seconds, increasing your Spirit by +1.5%,
    /// stacking up to 5 times.
    /// Dev: a Heavy Strike: Spirit 1.5%; with 5 stacks set, another one (after its cooldown) leaves 5 (7.5%); 7.5% at 19.9s after it and none at 20.1s.
    /// </summary>
    [Fact]
    public void Far_Beyond_Driven_Gives_1_5_Percent_Spirit_Per_Heavy_Strike_Up_To_5_For_20_Seconds()
    {
        var f = With(TariqTalents.FarBeyondDriven.TalentId);
        f.Cast<HeavyStrike>();
        Assert.Equal(0.015, f.Hero.SpiritPercent(f.Now), 1e-9);

        f.Hero.Auras.Apply(TariqTalents.FarBeyondDriven.Buff, f.Now, setStacks: 5);
        WaitForCooldown(f, "Heavy Strike");
        double cast = f.Now.TotalSeconds;
        f.Cast<HeavyStrike>();
        Assert.Equal(5, f.Stacks(TariqTalents.FarBeyondDriven.Buff));
        Assert.Equal(0.075, f.Hero.SpiritPercent(f.Now), 1e-9);

        WaitUntil(f, cast, 19.9);
        Assert.Equal(0.075, f.Hero.SpiritPercent(f.Now), 1e-9);
        f.Wait(0.2);
        Assert.Equal(0.0, f.Hero.SpiritPercent(f.Now), 1e-9);
    }

    // ---- Kill Em All

    /// <summary>
    /// Player (Kill Em All): your offensive abilities have a 10% chance to reset the cooldown of your Heavy Strike and grant you 2 stacks of Kill Em All
    /// for 8 seconds.
    /// Dev: Heavy Strike cast (on cooldown); the proc forced on; a Wild Swing: Heavy Strike is castable at once, 2 stacks; the stacks are there 7.9 seconds after the cast and gone 0.2 later. The
    /// chance is 0.1, rolled for each offensive cast but not for Thunder Call or Focused Wrath.
    /// </summary>
    [Fact]
    public void Kill_Em_All_Resets_Heavy_Strike_And_Gives_2_Stacks_For_8_Seconds()
    {
        var f = With(TariqTalents.KillEmAll.TalentId);
        bool on = false;
        var procs = f.ForceProcs(key => key == TariqTalents.KillEmAll.ProcKey ? on : null);
        f.Cast<HeavyStrike>();
        Assert.False(f.TryCast<HeavyStrike>().Succeeded);
        f.Cast<ThunderCall>();
        f.Cast<FocusedWrath>();
        Assert.DoesNotContain(procs.Rolls.Skip(1), r => r.Key == TariqTalents.KillEmAll.ProcKey);

        on = true;
        double procAt = f.Now.TotalSeconds;
        f.Cast<WildSwing>();

        Assert.Equal(2, f.Stacks(TariqTalents.KillEmAll.Buff));
        Assert.Equal(0.1, procs.Rolls.Last(r => r.Key == TariqTalents.KillEmAll.ProcKey).Chance, 1e-9);
        Assert.Equal(0.0, f.Hero.GetCooldownRemains("Heavy Strike", f.Now), 1e-9);
        WaitUntil(f, procAt, 7.9);
        Assert.Equal(2, f.Stacks(TariqTalents.KillEmAll.Buff));
        f.Wait(0.2);
        Assert.Equal(0, f.Stacks(TariqTalents.KillEmAll.Buff));
    }

    /// <summary>
    /// Player (Kill Em All): your next Heavy Strike consumes 1 stack of Kill Em All, causing it to deal 150% more damage: 2.5 times as much.
    /// Dev: 2 stacks set; two Heavy Strikes (each after its cooldown); both 2.5 times a Heavy Strike with no stacks; a third
    /// is back to 1 (the talent's proc is forced off).
    /// </summary>
    [Fact]
    public void Kill_Em_All_Makes_The_Next_Two_Heavy_Strikes_Deal_150_Percent_More()
    {
        double plain = Plain(f => f.Cast<HeavyStrike>(), "Heavy Strike");
        var f = With(TariqTalents.KillEmAll.TalentId);
        f.ForceProcs(key => key == TariqTalents.KillEmAll.ProcKey ? false : null);
        f.Hero.Auras.Apply(TariqTalents.KillEmAll.Buff, f.Now, setStacks: 2);

        f.Cast<HeavyStrike>();
        Assert.Equal(1, f.Stacks(TariqTalents.KillEmAll.Buff));
        WaitForCooldown(f, "Heavy Strike");
        f.Cast<HeavyStrike>();
        Assert.Equal(0, f.Stacks(TariqTalents.KillEmAll.Buff));
        WaitForCooldown(f, "Heavy Strike");
        f.Cast<HeavyStrike>();

        var hits = f.DamageOfEffect(1776);
        Assert.Equal(2.5, hits[0].Value / plain, Ratio);
        Assert.Equal(2.5, hits[1].Value / plain, Ratio);
        Assert.Equal(1.0, hits[2].Value / plain, Ratio);
    }

    /// <summary>
    /// Player (Kill Em All): while Kill Em All is active, your Heavy Strike has +60% Cooldown Acceleration: its 8 second cooldown takes 5.
    /// Dev: 2 stacks set; a Heavy Strike uses one, the other is still up; the cooldown is over 5 seconds after the cast.
    /// TODO: fails: the engine has it over after 5.56 seconds: the acceleration only works until the cast ends, then the cooldown runs at its normal speed
    /// although a stack is still up. Check in game whether a remaining stack keeps the acceleration.
    /// </summary>
    [Fact]
    public void Kill_Em_All_Speeds_Up_The_Heavy_Strike_Cooldown_By_60_Percent()
    {
        var f = With(TariqTalents.KillEmAll.TalentId);
        f.ForceProcs(key => key == TariqTalents.KillEmAll.ProcKey ? false : null);
        f.Hero.Auras.Apply(TariqTalents.KillEmAll.Buff, f.Now, setStacks: 2);

        f.Cast<HeavyStrike>();

        Assert.Equal(5.0, ReadyAt(f, f.Ability<HeavyStrike>()), 0.05);
    }

    // ---- Ace of Spades

    /// <summary>
    /// Player (Ace of Spades): your Chain Lightning now has 2 charges.
    /// Dev: Thunder Call; two Chain Lightnings in a row work, a third is refused; the first charge is back 8 seconds after the first cast.
    /// </summary>
    [Fact]
    public void Ace_Of_Spades_Gives_Chain_Lightning_2_Charges()
    {
        var f = With(TariqTalents.AceOfSpades.TalentId);
        f.Cast<ThunderCall>();
        f.ForceProcs(key => key == TariqTalents.AceOfSpades.ProcKey ? false : null);

        f.Cast<ChainLightning>();
        f.Cast<ChainLightning>();
        Assert.False(f.TryCast<ChainLightning>().Succeeded);

        f.Wait(8.0 - f.Now.TotalSeconds - 0.1);
        Assert.False(f.TryCast<ChainLightning>().Succeeded);
        f.Wait(0.2);
        Assert.True(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// Player (Ace of Spades): each time your Skull Crusher and Hammer Storm deal damage to an enemy you have a chance (3.0 PPM) to cast a free Chain
    /// Lightning at them: it costs no charge.
    /// Dev: the proc forced on after 10 seconds of nothing; Thunder Call; a Skull Crusher; 6 Chain Lightning hits (the free one), and both charges are
    /// still there. The chance is 3 x seconds since the last roll / 60: 0.5 after 10 seconds.
    /// </summary>
    [Fact]
    public void Ace_Of_Spades_Casts_A_Free_Chain_Lightning_At_3_Procs_Per_Minute()
    {
        var f = With(TariqTalents.AceOfSpades.TalentId);
        var procs = f.ForceProcs(key => key == TariqTalents.AceOfSpades.ProcKey ? true : null);
        f.Wait(10);

        f.Cast<SkullCrusher>();
        f.Wait(1);

        var rolls = procs.Rolls.Where(r => r.Key == TariqTalents.AceOfSpades.ProcKey).ToList();
        Assert.Single(rolls);
        Assert.Equal(3.0 * 10 / 60, rolls[0].Chance, 1e-9);
        Assert.Equal(6, f.Damage("Chain Lightning").Count);

        f.Cast<ThunderCall>();
        f.Cast<ChainLightning>();
        Assert.True(f.TryCast<ChainLightning>().Succeeded);
    }

    /// <summary>
    /// Player (Ace of Spades): "each time your Skull Crusher and Hammer Storm deals damage to an enemy" rolls once per damage dealt: with Thunder Call a
    /// Skull Crusher deals 2 (the hit and its thunder bolt), so it rolls twice; a Hammer Storm rolls once per spin and enemy.
    /// Dev: the proc forced off; Thunder Call; a Skull Crusher: 2 rolls; a Hammer Storm on one enemy: 3 rolls more (and a Hammer Storm's thunder bolts 3
    /// more).
    /// TODO: the engine rolls for every hit, thunder bolts included; whether the game does is not known. Check in game.
    /// </summary>
    [Fact(Skip = "Whether Ace of Spades rolls for thunder bolts and each Hammer Storm spin separately is not known: to check in game")]
    public void Ace_Of_Spades_Rolls_For_Every_Damage_Dealt_By_Skull_Crusher_And_Hammer_Storm()
    {
        var f = With(TariqTalents.AceOfSpades.TalentId);
        var procs = f.ForceProcs(key => key == TariqTalents.AceOfSpades.ProcKey ? false : null);
        f.Cast<ThunderCall>();
        f.Cast<SkullCrusher>();
        Assert.Equal(2, procs.Rolls.Count(r => r.Key == TariqTalents.AceOfSpades.ProcKey));

        f.Cast<HammerStorm>();
        f.Wait(1.5);

        Assert.Equal(2 + 6, procs.Rolls.Count(r => r.Key == TariqTalents.AceOfSpades.ProcKey));
    }

    // ---- Schism

    /// <summary>
    /// Player (Schism): your Skull Crusher has a 35% chance to make your next Hammer Storm deal +150% increased damage, and your Hammer Storm has a 35%
    /// chance to make your next Skull Crusher deal +150% increased damage.
    /// Dev: each proc forced on; a Skull Crusher then a Hammer Storm: the Hammer Storm's first spin is 2.5 times a plain one; then the Skull Crusher
    /// that follows is 2.5 times a plain one. The chance is 0.35, rolled once per cast.
    /// </summary>
    [Fact]
    public void Schism_Makes_The_Next_Other_Spender_Deal_150_Percent_More()
    {
        double plainStorm = Plain(f => f.Cast<HammerStorm>(), "Hammer Storm");
        double plainCrusher = Plain(f => f.Cast<SkullCrusher>(), "Skull Crusher");
        var f = With(TariqTalents.Schism.TalentId);
        f.SetFury(200);
        var procs = f.ForceProcs(key => key is TariqTalents.Schism.SkullCrusherProcKey or TariqTalents.Schism.HammerStormProcKey ? true : null);

        f.Cast<SkullCrusher>();
        Assert.Equal(1, f.Stacks(TariqTalents.Schism.HammerStormBuff));
        f.Cast<HammerStorm>();
        Assert.Equal(1, f.Stacks(TariqTalents.Schism.SkullCrusherBuff));
        f.Cast<SkullCrusher>();

        Assert.Equal(2.5, f.Damage("Hammer Storm")[0].Value / plainStorm, Ratio);
        Assert.Equal(2.5, f.Damage("Skull Crusher")[1].Value / plainCrusher, Ratio);
        var rolls = procs.Rolls.Where(r => r.Key is TariqTalents.Schism.SkullCrusherProcKey or TariqTalents.Schism.HammerStormProcKey).ToList();
        Assert.Equal(3, rolls.Count);
        Assert.All(rolls, r => Assert.Equal(0.35, r.Chance, 1e-9));
    }

    /// <summary>
    /// Player (Schism): you can have up to 2 stacks of each Schism at any one time.
    /// Dev: the proc forced on; 3 Skull Crushers: Hammer Storm's Schism has 2 stacks.
    /// </summary>
    [Fact]
    public void Schism_Stacks_Up_To_2()
    {
        var f = With(TariqTalents.Schism.TalentId);
        f.ForceProcs(key => key == TariqTalents.Schism.HammerStormProcKey ? true : null);

        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();
        f.Cast<SkullCrusher>();

        Assert.Equal(2, f.Stacks(TariqTalents.Schism.HammerStormBuff));
    }

    /// <summary>
    /// Player (Schism): a Skull Crusher never makes the next Skull Crusher stronger, nor a Hammer Storm the next Hammer Storm.
    /// Dev: both procs forced on; a Skull Crusher: Schism for Hammer Storm only.
    /// </summary>
    [Fact]
    public void Schism_Does_Not_Boost_The_Same_Spender()
    {
        var f = With(TariqTalents.Schism.TalentId);
        f.ForceProcs(key => key is TariqTalents.Schism.SkullCrusherProcKey or TariqTalents.Schism.HammerStormProcKey ? true : null);

        f.Cast<SkullCrusher>();

        Assert.Equal(0, f.Stacks(TariqTalents.Schism.SkullCrusherBuff));
        Assert.Equal(1, f.Stacks(TariqTalents.Schism.HammerStormBuff));
    }

    // ---- Them Bones

    /// <summary>
    /// Player (Them Bones): your Skull Crusher has a 30% chance to have +100% increased critical strike chance, for the physical hit and for the thunder
    /// bolt of Thunder Call alike.
    /// Dev: the proc forced on; 0% crit; Thunder Call; a Skull Crusher; both hits have a crit chance of 1 (100%); the chance is 0.3, rolled once per cast.
    /// </summary>
    [Fact]
    public void Them_Bones_Adds_100_Percent_Crit_Chance_To_Skull_Crusher_And_Its_Thunder_Bolt()
    {
        var f = With(TariqTalents.ThemBones.TalentId);
        f.Cast<ThunderCall>();
        var procs = f.ForceProcs(key => key == TariqTalents.ThemBones.ProcKey ? true : null);

        f.Cast<SkullCrusher>();

        Assert.Equal(1.0, f.Damage("Skull Crusher").Single().CritChance, 1e-9);
        Assert.Equal(1.0, f.Damage("Thunder Call: Skull Crusher").Single().CritChance, 1e-9);
        var rolls = procs.Rolls.Where(r => r.Key == TariqTalents.ThemBones.ProcKey).ToList();
        Assert.Single(rolls);
        Assert.Equal(0.3, rolls[0].Chance, 1e-9);
    }

    /// <summary>
    /// Player (Them Bones): without the proc, Skull Crusher has your own crit chance; the other abilities never have the bonus.
    /// Dev: 10% crit; the proc forced off then on; a Skull Crusher at 0.1; a Face Breaker with the proc on at 0.1.
    /// </summary>
    [Fact]
    public void Them_Bones_Only_Affects_Skull_Crusher_When_It_Procs()
    {
        var f = With(TariqTalents.ThemBones.TalentId, crit: 0.1);
        bool on = false;
        f.ForceProcs(key => key == TariqTalents.ThemBones.ProcKey ? on : null);

        f.Cast<SkullCrusher>();
        on = true;
        f.Cast<FaceBreaker>();

        Assert.Equal(0.1, f.Damage("Skull Crusher").Single().CritChance, 1e-9);
        Assert.Equal(0.1, f.Damage("Face Breaker").First().CritChance, 1e-9);
    }

    // ---- Crack the Sky

    /// <summary>
    /// Player (Crack the Sky): your lightning damage has +20% increased critical strike chance: the thunder bolts and lightning of Thunder Call, Chain
    /// Lightning, Raging Currents and Raging Tempest's leap, and not your physical hits.
    /// Dev: 10% crit; each lightning hit has a crit chance of 0.3; the physical hits of the same casts have 0.1.
    /// </summary>
    [Theory]
    [InlineData("Thunder Call: Skull Crusher")]
    [InlineData("Thunder Call: Heavy Strike")]
    [InlineData("Thunder Call: Hammer Storm")]
    [InlineData("Chain Lightning")]
    [InlineData("Raging Currents")]
    [InlineData("Raging Tempest")]
    public void Crack_The_Sky_Adds_20_Percent_Crit_Chance_To_Lightning_Damage(string lightning)
    {
        var f = With(TariqTalents.CrackTheSky.TalentId, crit: 0.1);
        f.GiveTariqSpirit();
        if (lightning is "Raging Currents" or "Raging Tempest") f.Cast<RagingTempest>();
        else f.Cast<ThunderCall>();
        f.Cast<SkullCrusher>();
        f.Cast<HeavyStrike>();
        f.Cast<HammerStorm>();
        f.Cast<ChainLightning>();
        f.Wait(2);

        var hits = f.Damage(lightning);
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal(0.3, h.CritChance, 1e-9));
        Assert.All(f.Damage("Skull Crusher"), h => Assert.Equal(0.1, h.CritChance, 1e-9));
        Assert.All(f.Damage("Hammer Storm"), h => Assert.Equal(0.1, h.CritChance, 1e-9));
    }

    // ---- Killing in the Name

    /// <summary>
    /// Player (Killing in the Name): your Culling Strike has +50% increased critical strike chance.
    /// Dev: 10% crit; a Culling Strike has a crit chance of 0.6, a Skull Crusher 0.1.
    /// </summary>
    [Fact]
    public void Killing_In_The_Name_Gives_Culling_Strike_50_Percent_Crit_Chance()
    {
        var f = With(TariqTalents.KillingInTheName.TalentId, crit: 0.1);

        f.Cast<CullingStrike>();
        f.Cast<SkullCrusher>();

        Assert.Equal(0.6, f.Damage("Culling Strike")[0].CritChance, 1e-9);
        Assert.Equal(0.1, f.Damage("Skull Crusher")[0].CritChance, 1e-9);
    }
}
