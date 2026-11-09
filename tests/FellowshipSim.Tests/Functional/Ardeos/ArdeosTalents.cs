using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's talents, on a bare Ardeos: each talent's own rule first, then how it works with other talents and abilities. How long an
/// Engulfing Flames lasts with Undying Flame and Slow Burn is in Specific/ArdeosEngulfingFlamesDuration.cs, and the highest a Searing Blaze
/// can be extended to after a recast in Specific/ArdeosSearingBlazeDuration.cs.
/// </summary>
public class ArdeosTalents
{
    // ---- Agonizing Blaze

    /// <summary>
    /// Casting Searing Blaze again and again still deals its ticks, with or without Agonizing Blaze: one every 2 seconds, shortened by
    /// haste, so floor(time / 2 x (1 + haste)) over the casts.
    ///
    /// A recast once moved the next tick back, so chain-casting dealt nothing.
    /// 59 casts in a row, 44.25 ticks' worth at any haste: with a multiple of 4 casts the last tick would land on the end of the last cast, an edge.
    /// With Agonizing Blaze each recast also adds a stack (up to 10), which this test does not read.
    /// </summary>
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(false, 0.1)]
    [InlineData(false, 0.2)]
    [InlineData(true, 0.0)]
    [InlineData(true, 0.1)]
    [InlineData(true, 0.2)]
    public void Spamming_Searing_Blaze_Ticks_Every_2_Hasted_Seconds_With_Or_Without_Agonizing_Blaze(bool agonizingBlaze, double haste)
    {
        var f = StepFights.Ardeos(haste: haste, talents: agonizingBlaze ? [AgonizingBlaze.TalentId] : null);

        for (int i = 0; i < 59; i++) f.Cast<SearingBlaze>();

        Assert.Equal((int)Math.Floor(f.Seconds() / 2 * (1 + haste)), f.Ticks(SearingBlazeDot.EffectId).Count);
    }

    // ---- Apocalyptic Surge

    /// <summary>
    /// Apocalyptic Surge takes 1.5 seconds off Apocalypse's 3 second cast: it takes 1.5 seconds, shortened by haste, instead of 3.
    /// </summary>
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(false, 0.2)]
    [InlineData(true, 0.0)]
    [InlineData(true, 0.2)]
    public void Apocalyptic_Surge_Takes_1_5_Seconds_Off_Apocalypse_Cast(bool apocalypticSurge, double haste)
    {
        var f = StepFights.Ardeos(haste: haste, talents: apocalypticSurge ? [ApocalypticSurge.TalentId] : null);

        f.Cast<Apocalypse>();

        AssertSeconds((apocalypticSurge ? 1.5 : 3.0) / (1 + haste), f.LastCastDuration("Apocalypse"));
    }

    /// <summary>
    /// With Apocalyptic Surge, Apocalypse gives 2 stacks of Apocalyptic Surge for 18 seconds from the end of its cast.
    /// </summary>
    [Fact]
    public void Apocalyptic_Surge_Gives_2_Stacks_For_18_Seconds()
    {
        var f = StepFights.Ardeos(talents: [ApocalypticSurge.TalentId]);

        f.Cast<Apocalypse>();
        double end = f.CompletedAt("Apocalypse") + 18.0;

        Assert.Equal(2, f.Stacks(ApocalypticSurge.Buff));
        f.WaitUntil(end - 0.1);
        Assert.Equal(2, f.Stacks(ApocalypticSurge.Buff));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(ApocalypticSurge.Buff));
    }

    /// <summary>With Apocalyptic Surge, the stacks stop at 4: two Apocalypses give 4, and a third leaves 4.</summary>
    [Fact]
    public void Apocalyptic_Surge_Stacks_Up_To_4()
    {
        var f = StepFights.Ardeos(talents: [ApocalypticSurge.TalentId]);

        f.Cast<Apocalypse>();
        f.ResetCooldown<Apocalypse>(); // to cast it again at once
        f.Cast<Apocalypse>();

        using var check = new Checks(f);
        check.Equal(4, f.Stacks(ApocalypticSurge.Buff));
        f.ResetCooldown<Apocalypse>(); // to cast it again at once
        f.Cast<Apocalypse>();
        check.Equal(4, f.Stacks(ApocalypticSurge.Buff));
    }

    /// <summary>
    /// With Apocalyptic Surge, your next Detonate takes a stack and is free: cast with no Cinders, it spends none and leaves 1 of the 2
    /// stacks. Having spent nothing, it does not try for a Spirit Refund either, where a Detonate that spends a Burning Ember does.
    /// </summary>
    [Fact]
    public void Apocalyptic_Surge_Makes_The_Next_Detonate_Free_For_A_Stack()
    {
        var f = StepFights.Ardeos(talents: [ApocalypticSurge.TalentId]);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // only to read the rolls

        f.Cast<Apocalypse>(); // 2 stacks, and Searing Blaze for Detonate to detonate
        f.Set(Ardeos.Cinders, 0); // no Burning Ember
        int refundRollsBefore = procs.ChancesOf(Hero.SpiritRefundProc).Count; // Apocalypse's own

        f.Cast<Detonate>();

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(ApocalypticSurge.Buff));
        check.Equal(0, f.Spends("Cinders").Count);
        check.Equal(refundRollsBefore, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls of the free Detonate");

        f.Remove(ApocalypticSurge.Buff); // no free cast left
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
        f.Cast<Detonate>();
        check.Equal(refundRollsBefore + 1, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls once a Detonate spends");
    }

    // ---- Backdraft (its extensions after a Searing Blaze recast are in Specific/ArdeosSearingBlazeDuration.cs)

    /// <summary>
    /// With Backdraft, each Detonate extends your Searing Blaze by 1.5 seconds, but never past its full 30 seconds: three Detonates right
    /// after casting it leave it 30 seconds after the last one.
    /// </summary>
    [Fact]
    public void Backdraft_Never_Extends_Searing_Blaze_Past_30_Seconds()
    {
        var f = StepFights.Ardeos(talents: [Backdraft.TalentId]);

        f.Cast<SearingBlaze>();

        for (int i = 0; i < 3; i++)
        {
            f.Set(Ardeos.Cinders, 100); // a Burning Ember for each
            f.Cast<Detonate>(); // 1.5 seconds added, about 1 second apart
        }
        double end = f.CompletedAt("Detonate") + 30.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Searing Blaze"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Searing Blaze"));
    }

    // ---- Burning Initiative

    /// <summary>With Burning Initiative, you start the fight with 2 Burning Embers, 200 Cinders; without it, with none.</summary>
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(true, 200.0)]
    public void Burning_Initiative_Starts_The_Fight_With_2_Burning_Embers(bool burningInitiative, double cinders)
    {
        var f = StepFights.Ardeos(talents: burningInitiative ? [BurningInitiative.TalentId] : null);

        Assert.Equal(cinders, f.Amount(Ardeos.Cinders));
    }

    /// <summary>
    /// Burning Initiative gives no Spirit Points: you start the fight with as many Spirit Points with the talent as without it.
    ///
    /// Per the user, the 50 Spirit Points in the game data (AdditionalSpiritPoints 50) were cut from the game.
    /// </summary>
    [Fact]
    public void Burning_Initiative_Gives_No_Spirit_Points()
    {
        var plainScenario = StepFights.Ardeos();
        var burningInitiativeScenario = StepFights.Ardeos(talents: [BurningInitiative.TalentId]);

        Assert.Equal(plainScenario.Amount(Hero.SpiritPoints), burningInitiativeScenario.Amount(Hero.SpiritPoints));
    }

    // ---- Cascading Inferno

    /// <summary>With Cascading Inferno, each Detonate you cast gives a stack of Cascading Inferno, up to 12: 13 Detonates leave 12.</summary>
    [Fact]
    public void Cascading_Inferno_Gives_A_Stack_For_Each_Detonate_Up_To_12()
    {
        var f = StepFights.Ardeos(talents: [CascadingInferno.TalentId]);

        f.Cast<SearingBlaze>(); // an effect for Detonate to detonate

        using var check = new Checks(f);
        for (int cast = 1; cast <= 13; cast++)
        {
            f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
            f.Cast<Detonate>();
            check.Equal(Math.Min(cast, 12), f.Stacks(CascadingInferno.Buff), what: $"stacks after Detonate {cast}");
        }
    }

    /// <summary>With Cascading Inferno, Infernal Wave is instant once you have at least 4 stacks; with 3, it takes its 1.5 second cast.</summary>
    [Theory]
    [InlineData(3, 1.5)]
    [InlineData(4, 0.0)]
    [InlineData(7, 0.0)]
    public void Cascading_Inferno_Makes_Infernal_Wave_Instant_At_4_Stacks(int stacks, double castTime)
    {
        var f = StepFights.Ardeos(talents: [CascadingInferno.TalentId]);
        f.Apply(CascadingInferno.Buff, stacks: stacks); // as many Detonates

        f.Cast<InfernalWave>();

        AssertSeconds(castTime, f.LastCastDuration("Infernal Wave"));
    }

    /// <summary>
    /// With Cascading Inferno, an instant Infernal Wave takes 4 stacks and leaves the rest: 7 stacks leave 3, 4 leave none, and a wave cast
    /// with 3 takes none.
    /// </summary>
    [Theory]
    [InlineData(3, 3)]
    [InlineData(4, 0)]
    [InlineData(7, 3)]
    public void Cascading_Inferno_Instant_Infernal_Wave_Takes_4_Stacks(int stacks, int stacksLeft)
    {
        var f = StepFights.Ardeos(talents: [CascadingInferno.TalentId]);
        f.Apply(CascadingInferno.Buff, stacks: stacks); // as many Detonates

        f.Cast<InfernalWave>();

        Assert.Equal(stacksLeft, f.Stacks(CascadingInferno.Buff));
    }

    /// <summary>
    /// With Cascading Inferno, an Infernal Wave cast with at least 4 stacks has +100% critical strike chance: on a hero with no crit, its hit
    /// has a crit chance of 1 with 4 stacks and 0 with 3.
    /// </summary>
    [Theory]
    [InlineData(3, 0.0)]
    [InlineData(4, 1.0)]
    public void Cascading_Inferno_Gives_Infernal_Wave_100_Percent_Crit_Chance_At_4_Stacks(int stacks, double critChance)
    {
        var f = StepFights.Ardeos(talents: [CascadingInferno.TalentId]);
        f.Apply(CascadingInferno.Buff, stacks: stacks); // as many Detonates

        f.Cast<InfernalWave>();
        f.Wait(1.0); // the wave lands

        Assert.Equal(critChance, f.Damage<InfernalWave>().Single().CritChance, 6);
    }

    /// <summary>
    /// With Cascading Inferno, an Infernal Wave cast with at least 4 stacks generates twice its Cinders: 80 instead of 40.
    /// </summary>
    [Theory]
    [InlineData(3, 40.0)]
    [InlineData(4, 80.0)]
    public void Cascading_Inferno_Doubles_Infernal_Wave_Cinders_At_4_Stacks(int stacks, double cinders)
    {
        var f = StepFights.Ardeos(talents: [CascadingInferno.TalentId]);
        f.Apply(CascadingInferno.Buff, stacks: stacks); // as many Detonates

        f.Cast<InfernalWave>();

        Assert.Equal(cinders, f.Gains("Cinders").Single().Value, 6);
    }

    // ---- Crash and Burn

    /// <summary>
    /// With Crash and Burn, each time Searing Blaze deals damage, Fire Ball's recharge loses 0.05 seconds: a charge spent at the start comes
    /// back 30 - 0.05 x (Searing Blaze ticks before then) seconds later.
    /// </summary>
    [Fact]
    public void Crash_And_Burn_Takes_0_05_Seconds_Off_Fire_Ball_For_Each_Searing_Blaze_Tick()
    {
        var f = StepFights.Ardeos(talents: [CrashAndBurn.TalentId]);

        f.Cast<FireBall>(); // a charge recharging
        f.Cast<SearingBlaze>();
        f.WaitUntil(29.0);
        int ticks = f.Ticks(SearingBlazeDot.EffectId).Count;
        double end = f.CompletedAt("Fire Ball") + 30.0 - 0.05 * ticks;

        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<FireBall>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<FireBall>());
    }

    /// <summary>
    /// With Crash and Burn, the recharge of Fire Ball drops in a step each time Searing Blaze ticks, 0.05 seconds for each enemy it ticks
    /// on: with Searing Blaze on 4 enemies in sync, 0.2 seconds off at every tick, and only the time passing in between.
    ///
    /// The recharge is read directly, just before and just after each of the next ticks, which are 2 seconds apart.
    /// </summary>
    [Fact]
    public void Crash_And_Burn_Takes_Fire_Ball_Recharge_Down_In_A_Step_At_Each_Searing_Blaze_Tick()
    {
        var f = StepFights.Ardeos(enemies: 4, talents: [CrashAndBurn.TalentId]);

        f.Cast<Apocalypse>(); // Searing Blaze on all 4 enemies, in sync
        double applied = f.CompletedAt("Apocalypse");
        f.Cast<FireBall>(); // a charge recharging

        using var check = new Checks(f);
        double previousAfter = f.RechargeRemaining<FireBall>();
        double previousTime = f.Seconds();
        for (int tick = 1; tick <= 4; tick++)
        {
            double tickTime = applied + 2.0 * tick;
            f.WaitUntil(tickTime - 0.05);
            double before = f.RechargeRemaining<FireBall>();
            f.WaitUntil(tickTime + 0.05);
            double after = f.RechargeRemaining<FireBall>();

            check.Seconds(previousAfter - (f.Seconds() - 0.1 - previousTime), before, what: $"nothing but time passing before tick {tick}");
            check.Seconds(before - (4 * 0.05 + 0.1), after, what: $"step at tick {tick}: 4 x 0.05s, and the 0.1s that passed");
            previousAfter = after;
            previousTime = f.Seconds();
        }
    }

    // ---- Slow Burn (how long it makes an Engulfing Flames last is in Specific/ArdeosEngulfingFlamesDuration.cs)

    /// <summary>
    /// With Slow Burn, each Fire Ball tick extends the target's Searing Blaze or Engulfing Flames by 0.5 seconds, but never past its full
    /// duration: an effect applied a quarter of a second before a tick is back at its full duration after that tick, then gains 0.5 seconds
    /// from each of the burn's 4 later ticks.
    /// </summary>
    [Theory]
    [InlineData("Searing Blaze", 30.0)]
    [InlineData("Engulfing Flames", 9.0)]
    public void Slow_Burn_Never_Extends_An_Effect_Past_Its_Full_Duration(string effect, double duration)
    {
        var f = StepFights.Ardeos(talents: [SlowBurn.TalentId]);

        f.Cast<FireBall>();
        f.Wait(1.0); // the ball lands
        double ballLanded = f.DirectHits<FireBall>().Single().Time.TotalSeconds;
        double cappingTick = ballLanded + 4.0; // the burn's second tick
        double castTime = effect == "Engulfing Flames" ? 1.5 : 0.0; // Searing Blaze is instant
        f.WaitUntil(cappingTick - 0.25 - castTime);

        f.Cast(effect); // lands 0.25s before that tick
        double end = cappingTick + duration + 0.5 * 4; // back to its full duration, then the 4 later ticks

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff(effect));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff(effect));
    }

    /// <summary>
    /// With Slow Burn, even a partial Fire Ball tick extends Searing Blaze by the full 0.5 seconds: a second Fire Ball landing before the
    /// burn's first tick gives the burn 6 full ticks and a partial one, and the Searing Blaze lasts 30 + 0.5 x 7 seconds.
    ///
    /// Slow Burn's tooltip is misleading.
    /// Instead, it should read as:
    /// "Each tick of Fireball extends the duration of Searing Blaze and Engulfing Flames by 0.5s"
    /// </summary>
    [Fact]
    public void Slow_Burn_Extends_By_The_Full_Half_Second_On_A_Partial_Fire_Ball_Tick()
    {
        var f = StepFights.Ardeos(talents: [SlowBurn.TalentId]).WithoutDamageSpread();

        f.Cast<SearingBlaze>();

        f.Cast<FireBall>();
        f.Cast<FireBall>(); // lands before the burn's first tick
        f.Wait(16); // the burn ticks out

        var burnTicks = f.Ticks(FireBallDot.EffectId);
        double end = f.CompletedAt("Searing Blaze") + 30.0 + 0.5 * burnTicks.Count;

        Assert.Equal(7, burnTicks.Count);
        Assert.True(burnTicks[^1].Value < burnTicks[0].Value); // the last tick is partial
        f.WaitUntil(end - 0.05);
        Assert.True(f.HasDebuff("Searing Blaze"));
        f.WaitUntil(end + 0.05);
        Assert.False(f.HasDebuff("Searing Blaze"));
    }

    // ---- Firestarter and Spontaneous Combustion

    /// <summary>
    /// Firestarter and Spontaneous Combustion make your damage over time effects crit more.
    /// However, Detonate isn't affected: Detonate only looks at base damage, not expected damage.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Firestarter_And_Spontaneous_Combustion_Do_Not_Change_Detonate_Damage(bool firestarter, bool spontaneousCombustion)
    {
        List<StepEvent> DetonateHits(params int[] talents)
        {
            var f = StepFights.Ardeos(talents: talents).WithoutDamageSpread();
            f.ForceProcs(key => key is SpontaneousCombustion.SearingBlazeProcKey or SpontaneousCombustion.EngulfingFlamesProcKey
                ? false : null); // Spontaneous Combustion never empowers a tick

            f.Cast<SearingBlaze>();
            f.Cast<EngulfingFlames>();
            f.Cast<FireBall>();
            f.Wait(1.0); // the ball lands and sets the enemy on fire
            f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend

            f.Cast<Detonate>(crit: false);

            return f.Damage<Detonate>();
        }

        var talents = new List<int>();
        if (firestarter) talents.Add(Firestarter.TalentId);
        if (spontaneousCombustion) talents.Add(SpontaneousCombustion.TalentId);
        var plainHits = DetonateHits();
        var talentHits = DetonateHits([.. talents]);

        using var check = new Checks();
        check.Equal(3, talentHits.Count);
        for (int i = 0; i < Math.Min(plainHits.Count, talentHits.Count); i++)
        {
            check.Ratio(1.0, talentHits[i].Value / plainHits[i].Value, what: $"hit {i + 1}");
            check.Equal(plainHits[i].CritChance, talentHits[i].CritChance, what: $"crit chance of hit {i + 1}");
        }
    }

    // ---- Flare Up

    /// <summary>
    /// With Flare Up, Infernal Wave's hit makes the other enemies with Searing Blaze on them take 50% of the damage it dealt to its target,
    /// and the enemies without Searing Blaze take nothing: of 3 other enemies, the 2 with Searing Blaze each take half the wave's hit.
    /// </summary>
    [Fact]
    public void Flare_Up_Deals_50_Percent_Of_The_Wave_Hit_To_Enemies_With_Searing_Blaze()
    {
        var f = StepFights.Ardeos(enemies: 4, talents: [FlareUp.TalentId]).WithoutDamageSpread();
        var enemies = f.Enemies();

        f.Cast<SearingBlaze>(target: enemies[1]);
        f.Cast<SearingBlaze>(target: enemies[2]);

        f.Cast<InfernalWave>(target: enemies[0]);
        f.Wait(1.0); // the wave lands
        double waveDamage = f.Damage<InfernalWave>().Single().Value;
        var flares = f.Damage("Flare Up");

        using var check = new Checks(f);
        check.Equal(2, flares.Count);
        check.True(flares.Select(flare => flare.Target).Order().SequenceEqual([enemies[1].Name, enemies[2].Name]));
        foreach (var flare in flares) check.Ratio(0.5, flare.Value / waveDamage, what: flare.Target);
    }

    /// <summary>
    /// With Flare Up, the wave's own target flares up too when it has Searing Blaze: it takes the wave's hit, then a Flare Up hit of 50%
    /// of it. Without Searing Blaze on it, it takes no Flare Up hit.
    /// </summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void Flare_Up_Hits_The_Waves_Target_When_It_Has_Searing_Blaze(bool searingBlaze, int flareHits)
    {
        var f = StepFights.Ardeos(talents: [FlareUp.TalentId]).WithoutDamageSpread();

        if (searingBlaze) f.Cast<SearingBlaze>();

        f.Cast<InfernalWave>();
        f.Wait(1.0); // the wave lands
        double waveDamage = f.Damage<InfernalWave>().Single().Value;
        var flares = f.Damage("Flare Up");

        using var check = new Checks(f);
        check.Equal(flareHits, flares.Count);
        foreach (var flare in flares)
        {
            check.True(flare.Target == f.Enemy.Name, "the flare is on the wave's target");
            check.Ratio(0.5, flare.Value / waveDamage);
        }
    }

    /// <summary>
    /// With Flare Up, the share falls off past 3 enemies flaring up: each takes 50% x sqrt(3 / N) of the wave's hit, N the enemies with
    /// Searing Blaze, and 50% up to 3.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void Flare_Up_Falls_Off_Past_3_Enemies(int burning)
    {
        var f = StepFights.Ardeos(enemies: burning + 1, talents: [FlareUp.TalentId]).WithoutDamageSpread();
        var enemies = f.Enemies();

        for (int i = 1; i <= burning; i++) f.Cast<SearingBlaze>(target: enemies[i]);

        f.Cast<InfernalWave>(target: enemies[0]);
        f.Wait(1.0); // the wave lands
        double waveDamage = f.Damage<InfernalWave>().Single().Value;
        var flares = f.Damage("Flare Up");
        double share = 0.5 * (burning > 3 ? Math.Sqrt(3.0 / burning) : 1.0);

        using var check = new Checks(f);
        check.Equal(burning, flares.Count);
        foreach (var flare in flares) check.Ratio(share, flare.Value / waveDamage, what: flare.Target);
    }

    /// <summary>
    /// With Flare Up, the wave's target with Searing Blaze is one of the enemies flaring up for the falloff: with 4 enemies burning, the
    /// target among them, each of the 4 takes 50% x sqrt(3 / 4) of the wave's hit.
    /// </summary>
    [Fact]
    public void Flare_Up_Counts_The_Waves_Burning_Target_In_The_Falloff()
    {
        var f = StepFights.Ardeos(enemies: 4, talents: [FlareUp.TalentId]).WithoutDamageSpread();
        var enemies = f.Enemies();

        foreach (var enemy in enemies) f.Cast<SearingBlaze>(target: enemy);

        f.Cast<InfernalWave>(target: enemies[0]);
        f.Wait(1.0); // the wave lands
        double waveDamage = f.Damage<InfernalWave>().Single().Value;
        var flares = f.Damage("Flare Up");

        using var check = new Checks(f);
        check.Equal(4, flares.Count);
        check.Equal(4, flares.Select(flare => flare.Target).Distinct().Count(), what: "enemies flaring up");
        foreach (var flare in flares) check.Ratio(0.5 * Math.Sqrt(3.0 / 4), flare.Value / waveDamage, what: flare.Target);
    }

    // ---- Frog Squad

    /// <summary>With Frog Squad, Fire Frogs conjures 2 more frogs: 7 instead of 5.</summary>
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 7)]
    public void Frog_Squad_Conjures_2_More_Frogs(bool frogSquad, int frogs)
    {
        var f = StepFights.Ardeos(talents: frogSquad ? [FrogSquad.TalentId] : null);
        var procs = f.ForceProcs(_ => null); // to count the frogs conjured

        f.Cast<FireFrogs>();

        Assert.Equal(frogs, procs.ChancesOf(FireFrogs.ConjuredProcKey).Count);
    }

    /// <summary>
    /// With Frog Squad, every Fire Frog leaps on enemies one more time, 4 times instead of 3: its 7 frogs hit 28 times, where Fire Frogs'
    /// 5 frogs hit 15 times without it.
    /// </summary>
    [Theory]
    [InlineData(false, 5 * 3)]
    [InlineData(true, 7 * 4)]
    public void Frog_Squad_Makes_Every_Frog_Leap_One_More_Time(bool frogSquad, int hits)
    {
        var f = StepFights.Ardeos(talents: frogSquad ? [FrogSquad.TalentId] : null);

        f.Cast<FireFrogs>();
        f.Wait(10); // every frog has leapt

        Assert.Equal(hits, f.Ticks(FireFrogAttack.DamageEffectId).Count);
    }

    /// <summary>With Frog Squad, Fire Frogs deal 20% more damage: each frog's hit deals 1.2 times its damage without the talent.</summary>
    [Fact]
    public void Frog_Squad_Makes_Frog_Hits_Deal_20_Percent_More()
    {
        double FirstFrogHit(params int[] talents)
        {
            var f = StepFights.Ardeos(talents: talents).WithoutDamageSpread();

            f.Cast<FireFrogs>();
            f.Wait(10); // every frog has leapt

            return f.Ticks(FireFrogAttack.DamageEffectId)[0].Value;
        }

        AssertRatio(1.2, FirstFrogHit(FrogSquad.TalentId) / FirstFrogHit());
    }

    /// <summary>
    /// With Frog Squad, the 20% more damage applies to the frogs' burn as well: an enemy a single frog leaps on burns for 120% of that hit,
    /// where it burns for 100% without the talent.
    /// </summary>
    [Theory]
    [InlineData(false, 1.0)]
    [InlineData(true, 1.2)]
    public void Frog_Squad_Makes_The_Frogs_Burn_Deal_20_Percent_More(bool frogSquad, double burnShare)
    {
        var f = StepFights.Ardeos(talents: frogSquad ? [FrogSquad.TalentId] : null).WithoutDamageSpread();

        f.HitWith(f.Ability<FireFrogs>().Attack, f.Enemy); // one frog's leap
        f.Wait(30); // the burn ticks out
        double hit = f.Ticks(FireFrogAttack.DamageEffectId).Single().Value;
        double burn = f.Ticks(FireFrogsDot.EffectId).Sum(tick => tick.Value);

        AssertRatio(burnShare, burn / hit);
    }

    // ---- Great Balls of Fire

    /// <summary>With Great Balls of Fire, Fire Ball deals 50% more damage: its hit deals 1.5 times its damage without the talent.</summary>
    [Fact]
    public void Great_Balls_Of_Fire_Makes_Fire_Ball_Deal_50_Percent_More()
    {
        double BallHit(params int[] talents)
        {
            var f = StepFights.Ardeos(talents: talents).WithoutDamageSpread();

            f.Cast<FireBall>();
            f.Wait(1.0); // the ball lands

            return f.DirectHits<FireBall>().Single().Value;
        }

        AssertRatio(1.5, BallHit(GreatBallsOfFire.TalentId) / BallHit());
    }

    /// <summary>
    /// With Great Balls of Fire, an additional 10% of the damage Fire Ball dealt is added to its burn: the burn deals 30% of the ball's hit,
    /// where it deals 20% without the talent.
    /// </summary>
    [Theory]
    [InlineData(false, 0.2)]
    [InlineData(true, 0.3)]
    public void Great_Balls_Of_Fire_Adds_10_Percent_Of_The_Hit_To_The_Burn(bool greatBallsOfFire, double burnShare)
    {
        var f = StepFights.Ardeos(talents: greatBallsOfFire ? [GreatBallsOfFire.TalentId] : null).WithoutDamageSpread();

        f.Cast<FireBall>();
        f.Wait(20); // the ball lands, and the burn ticks out
        double ballDamage = f.DirectHits<FireBall>().Single().Value;
        double burnDamage = f.Ticks(FireBallDot.EffectId).Sum(tick => tick.Value);

        AssertRatio(burnShare, burnDamage / ballDamage);
    }

    // ---- Intensifying Inferno

    /// <summary>
    /// With Intensifying Inferno, Infernal Wave deals 15% more for each damage over time effect on the target, added up, and each Engulfing
    /// Flames counts on its own: 1 + 0.15 x N, so two Engulfing Flames and a Searing Blaze give 1.45.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void Intensifying_Inferno_Adds_15_Percent_Per_Effect_Counting_Each_Engulfing_Flames(int engulfingFlames, bool searingBlaze)
    {
        double WaveDamage(bool intensifyingInferno)
        {
            var f = StepFights.Ardeos(talents: intensifyingInferno ? [IntensifyingInferno.TalentId] : null).WithoutDamageSpread();

            if (searingBlaze) f.Cast<SearingBlaze>();
            for (int i = 0; i < engulfingFlames; i++) f.Cast<EngulfingFlames>();

            f.Cast<InfernalWave>();
            f.Wait(1.0); // the wave lands

            return f.Damage<InfernalWave>().Single().Value;
        }

        int effects = engulfingFlames + (searingBlaze ? 1 : 0);
        AssertRatio(1 + 0.15 * effects, WaveDamage(intensifyingInferno: true) / WaveDamage(intensifyingInferno: false));
    }

    // ---- Reign of Fire

    /// <summary>
    /// With Reign of Fire, each Detonate cast has a chance at 1.5 procs per minute: a Detonate 10 seconds into the fight rolls with a chance
    /// of 1.5 x 10 / 60.
    /// </summary>
    [Fact]
    public void Reign_Of_Fire_Procs_From_Detonate_At_1_5_Procs_Per_Minute()
    {
        var f = StepFights.Ardeos(talents: [ReignOfFire.TalentId]);
        var procs = f.ForceProcs(key => key == ReignOfFire.ProcKey ? false : null); // only the chance matters

        f.Cast<SearingBlaze>(); // an effect for Detonate to detonate
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
        f.WaitUntil(10.0);

        f.Cast<Detonate>();

        Assert.Equal(1.5 * f.CompletedAt("Detonate") / 60, procs.ChancesOf(ReignOfFire.ProcKey).Single(), 9);
    }

    /// <summary>
    /// With Reign of Fire, a proc grants a Fire Ball charge at once: with both charges spent, a Detonate that procs leaves 1 charge. The
    /// recharge under way is not restarted: the next charge comes back 30 seconds after the first Fire Ball, as it would without the proc.
    /// </summary>
    [Fact]
    public void Reign_Of_Fire_Grants_A_Fire_Ball_Charge()
    {
        var f = StepFights.Ardeos(talents: [ReignOfFire.TalentId]);
        f.ForceProcs(key => key == ReignOfFire.ProcKey ? true : null);

        f.Cast<SearingBlaze>(); // an effect for Detonate to detonate
        f.Cast<FireBall>();
        double end = f.CompletedAt("Fire Ball") + 30.0;
        f.Cast<FireBall>(); // both charges spent
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
        Assert.Equal(0, f.Charges<FireBall>());

        f.Cast<Detonate>();

        Assert.Equal(1, f.Charges<FireBall>());
        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<FireBall>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<FireBall>());
    }

    /// <summary>
    /// With Reign of Fire, a proc gives your next Fire Ball +200% critical strike chance: on a hero with no crit, the next Fire Ball's hit has
    /// a crit chance of 2, and the one after it 0.
    /// </summary>
    [Fact]
    public void Reign_Of_Fire_Gives_The_Next_Fire_Ball_200_Percent_Crit_Chance()
    {
        var f = StepFights.Ardeos(talents: [ReignOfFire.TalentId]);
        f.ForceProcs(key => key == ReignOfFire.ProcKey ? true : null);

        f.Cast<SearingBlaze>(); // an effect for Detonate to detonate
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
        f.Cast<Detonate>();

        f.Cast<FireBall>();
        f.Cast<FireBall>();
        f.Wait(1.0); // the second ball lands
        var balls = f.DirectHits<FireBall>();

        using var check = new Checks(f);
        check.Equal(2, balls.Count);
        check.Equal(2.0, balls[0].CritChance, 6);
        check.Equal(0.0, balls[^1].CritChance, 6);
    }

    /// <summary>
    /// With Reign of Fire, the next Fire Ball's crit chance waits 12 seconds from the Detonate that procced, then is gone.
    /// </summary>
    [Fact]
    public void Reign_Of_Fire_Lasts_12_Seconds()
    {
        var f = StepFights.Ardeos(talents: [ReignOfFire.TalentId]);
        f.ForceProcs(key => key == ReignOfFire.ProcKey ? true : null);

        f.Cast<SearingBlaze>(); // an effect for Detonate to detonate
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend
        f.Cast<Detonate>();
        double end = f.CompletedAt("Detonate") + 12.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(ReignOfFire.Buff));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(ReignOfFire.Buff));
    }

    /// <summary>
    /// Reign of Fire stacks up to 2 times: three Detonates that proc it leave 2 stacks, and the next Fire Ball uses one of them.
    /// </summary>
    [Fact]
    public void Reign_Of_Fire_Stacks_Up_To_2()
    {
        var f = StepFights.Ardeos(talents: [ReignOfFire.TalentId]);
        f.ForceProcs(key => key == ReignOfFire.ProcKey ? true : null); // every Detonate procs

        f.Cast<SearingBlaze>(); // an effect for Detonate to detonate
        f.Set(Ardeos.Cinders, 300); // three Burning Embers to spend
        f.Cast<Detonate>();
        f.Cast<Detonate>();
        f.Cast<Detonate>();

        using var check = new Checks(f);
        check.Equal(2, f.Stacks(ReignOfFire.Buff));

        f.Cast<FireBall>();

        check.Equal(1, f.Stacks(ReignOfFire.Buff));
    }

    // ---- Rolling Flames

    /// <summary>
    /// With Rolling Flames, the recharge of Engulfing Flames drops in a step each time Searing Blaze ticks, 0.25 seconds for each enemy it
    /// ticks on: with Searing Blaze on 4 enemies in sync, 1 second off at every tick, and only the time passing in between.
    ///
    /// The recharge is read directly, just before and just after each of the next ticks, which are 2 seconds apart.
    /// </summary>
    [Fact]
    public void Rolling_Flames_Takes_0_25_Seconds_Off_Engulfing_Flames_For_Each_Searing_Blaze_Tick()
    {
        var f = StepFights.Ardeos(enemies: 4, talents: [RollingFlames.TalentId]);

        f.Cast<Apocalypse>(); // Searing Blaze on all 4 enemies, in sync
        double applied = f.CompletedAt("Apocalypse");
        f.Cast<EngulfingFlames>(); // a charge recharging

        using var check = new Checks(f);
        double previousAfter = f.RechargeRemaining<EngulfingFlames>();
        double previousTime = f.Seconds();
        for (int tick = 1; tick <= 4; tick++)
        {
            double tickTime = applied + 2.0 * tick;
            f.WaitUntil(tickTime - 0.05);
            double before = f.RechargeRemaining<EngulfingFlames>();
            f.WaitUntil(tickTime + 0.05);
            double after = f.RechargeRemaining<EngulfingFlames>();

            check.Seconds(previousAfter - (f.Seconds() - 0.1 - previousTime), before, what: $"nothing but time passing before tick {tick}");
            check.Seconds(before - (4 * 0.25 + 0.1), after, what: $"step at tick {tick}: 4 x 0.25s, and the 0.1s that passed");
            previousAfter = after;
            previousTime = f.Seconds();
        }
    }

    /// <summary>
    /// With Rolling Flames, each Infernal Wave you cast takes 1 second off Engulfing Flames' recharge: a charge spent before a wave comes back
    /// 19 seconds after the cast.
    ///
    /// From the tooltip and the game data (ReductionInSeconds 1).
    /// </summary>
    [Fact]
    public void Rolling_Flames_Takes_1_Second_Off_Engulfing_Flames_For_Each_Infernal_Wave()
    {
        var f = StepFights.Ardeos(talents: [RollingFlames.TalentId]);

        f.Cast<EngulfingFlames>(); // a charge recharging
        double end = f.CompletedAt("Engulfing Flames") + 20.0 - 1.0;

        f.Cast<InfernalWave>();

        f.WaitUntil(end - 0.1);
        Assert.Equal(1, f.Charges<EngulfingFlames>());
        f.WaitUntil(end + 0.1);
        Assert.Equal(2, f.Charges<EngulfingFlames>());
    }
}
