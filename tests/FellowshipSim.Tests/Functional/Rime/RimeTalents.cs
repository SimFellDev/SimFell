using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's talents: each talent's own rule first, then how it works with other talents and abilities. Their effects on cooldowns are in
/// RimeCooldowns.cs, and on Anima and Winter Orbs in RimeResourceEconomy.cs.
/// </summary>
public class RimeTalents
{
    /// <summary>Casts a Cold Snap, first waiting for a charge if none is left.</summary>
    private static void ColdSnapWhenReady(StepDriver f)
    {
        while (f.Charges<ColdSnap>() == 0) f.Wait(0.5);
        f.Cast<ColdSnap>();
    }

    // ---- Greater Glacial Blast

    /// <summary>With Greater Glacial Blast, Glacial Blast deals 40% more damage.</summary>
    [Fact]
    public void Greater_Glacial_Blast_Adds_40_Percent_Damage_To_Glacial_Blast()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();
        plainScenario.RimeOf().WinterOrbs = 2;

        plainScenario.Cast<GlacialBlast>();
        double plainBlastDamage = plainScenario.Damage<GlacialBlast>().First().Value;

        var greaterScenario = StepFights.Rime(talents: [GreaterGlacialBlast.TalentId]).WithoutDamageSpread();
        greaterScenario.RimeOf().WinterOrbs = 2;

        greaterScenario.Cast<GlacialBlast>();
        double greaterBlastDamage = greaterScenario.Damage<GlacialBlast>().First().Value;

        AssertRatio(1.4, greaterBlastDamage / plainBlastDamage);
    }

    /// <summary>With Greater Glacial Blast and Icy Talons, Talon Strike deals 40% more damage.</summary>
    [Fact]
    public void Greater_Glacial_Blast_Adds_40_Percent_Damage_To_Talon_Strike()
    {
        var plainScenario = StepFights.Rime(talents: [IcyTalons.TalentId]).WithoutDamageSpread();
        plainScenario.RimeOf().WinterOrbs = 1;

        plainScenario.Cast<TalonStrike>();
        double plainStrikeDamage = plainScenario.Damage<TalonStrike>().First().Value;

        var greaterScenario = StepFights.Rime(talents: [IcyTalons.TalentId, GreaterGlacialBlast.TalentId]).WithoutDamageSpread();
        greaterScenario.RimeOf().WinterOrbs = 1;

        greaterScenario.Cast<TalonStrike>();
        double greaterStrikeDamage = greaterScenario.Damage<TalonStrike>().First().Value;

        AssertRatio(1.4, greaterStrikeDamage / plainStrikeDamage);
    }

    // ---- Burstbolter (its extra Anima is in RimeResourceEconomy.cs)

    /// <summary>With Burstbolter, Bursting Ice deals 30% more damage.</summary>
    [Fact]
    public void Burstbolter_Adds_30_Percent_Damage_To_Bursting_Ice()
    {
        double FirstPulse(params int[] talents)
        {
            var f = StepFights.Rime(talents: talents).WithoutDamageSpread();

            f.Cast<BurstingIce>();
            f.Wait(4.0); // the pulses land
            return f.Damage<BurstingIce>().First().Value;
        }

        AssertRatio(1.3, FirstPulse(Burstbolter.TalentId) / FirstPulse());
    }

    // ---- Soulfrost Torrent (its faster ticks are in RimeHasteBreakpoints.cs)

    /// <summary>
    /// With Soulfrost Torrent active, every tick of the empowered Freezing Torrent is a critical strike, grievous past 100% crit chance,
    /// and deals (1 + your crit chance) times more on top: at 20% crit, (2 + 0.2) x 1.2 = 2.64 times a plain tick.
    ///
    /// The tooltip only gives +100% crit chance: the (1 + crit chance) factor is the game's own behaviour, a developer bug read from
    /// its Blueprint (SoulfrostTorrent.DamageScaleBase). The buff is applied directly, without haste, so every tick is a full one.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Soulfrost_Torrent_Ticks_Are_Certain_Crits_Scaled_Again_By_Crit_Chance(double crit)
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();

        plainScenario.Cast<FreezingTorrent>();
        double plainTickDamage = plainScenario.Damage<FreezingTorrent>().First().Value;

        var soulfrostScenario = StepFights.Rime(crit: crit, talents: [SoulfrostTorrent.TalentId]).WithoutDamageSpread();
        soulfrostScenario.Apply(SoulfrostTorrent.Buff); // the empowered torrent

        soulfrostScenario.Cast<FreezingTorrent>();
        var ticks = soulfrostScenario.Damage<FreezingTorrent>();

        using var check = new Checks(soulfrostScenario);
        check.True(ticks.Count > 0, "the torrent ticked");
        for (int i = 0; i < ticks.Count; i++)
            check.Ratio((2 + crit) * (1 + crit), ticks[i].Value / plainTickDamage, what: $"tick {i + 1}");
    }

    // ---- Biting Cold (how critical strikes and grievous ones are computed is in Generic/CriticalStrike.cs)

    /// <summary>With Biting Cold, your critical strikes deal 10% more damage.</summary>
    [Fact]
    public void Biting_Cold_Makes_Critical_Strikes_10_Percent_Stronger()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();

        plainScenario.Cast<FrostBolt>(crit: true);
        double plainCritDamage = plainScenario.Damage<FrostBolt>().First().Value;

        var bitingColdScenario = StepFights.Rime(talents: [BitingCold.TalentId]).WithoutDamageSpread();

        bitingColdScenario.Cast<FrostBolt>(crit: true);
        double bitingCritDamage = bitingColdScenario.Damage<FrostBolt>().First().Value;

        AssertRatio(1.1, bitingCritDamage / plainCritDamage);
    }

    // ---- Icy Talons

    /// <summary>With Icy Talons, Talon Strike and Rising Talons replace Glacial Blast and Ice Comet: you cannot cast those, even with orbs.</summary>
    [Fact]
    public void Icy_Talons_Replaces_Glacial_Blast_And_Ice_Comet()
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId]);
        f.RimeOf().WinterOrbs = 5;

        using var check = new Checks(f);
        check.False(f.TryCast<GlacialBlast>().Succeeded);
        check.False(f.TryCast<IceComet>().Succeeded);
        check.Equal(5, f.RimeOf().WinterOrbs);
    }

    // ---- Glacial Assault

    /// <summary>With Glacial Assault, each Cold Snap gives a stack, up to 4.</summary>
    [Fact]
    public void Glacial_Assault_Gains_A_Stack_From_Each_Cold_Snap_Up_To_4()
    {
        var f = StepFights.Rime(talents: [GlacialAssault.TalentId]);

        using var check = new Checks(f);
        for (int stacks = 1; stacks <= 5; stacks++)
        {
            ColdSnapWhenReady(f);
            check.Equal(Math.Min(stacks, 4), f.Stacks(GlacialAssault.Buff), what: $"stacks after Cold Snap {stacks}");
        }
    }

    /// <summary>
    /// At 4 stacks of Glacial Assault, a Glacial Blast is free, deals 40% more damage and uses up the stacks; the next one costs orbs
    /// again.
    /// </summary>
    [Fact]
    public void Glacial_Assault_Free_Blast_Deals_40_Percent_More_And_Uses_Up_The_Stacks()
    {
        var plainScenario = StepFights.Rime().WithoutDamageSpread();
        plainScenario.RimeOf().WinterOrbs = 2;

        plainScenario.Cast<GlacialBlast>();
        double plainBlastDamage = plainScenario.Damage<GlacialBlast>().First().Value;

        var glacialAssaultScenario = StepFights.Rime(talents: [GlacialAssault.TalentId]).WithoutDamageSpread();
        glacialAssaultScenario.Apply(GlacialAssault.Buff, stacks: 4);
        glacialAssaultScenario.RimeOf().WinterOrbs = 0; // no orbs: the blast is free, and the next one cannot be paid for

        glacialAssaultScenario.Cast<GlacialBlast>();
        double freeBlastDamage = glacialAssaultScenario.Damage<GlacialBlast>().First().Value;

        using var check = new Checks(glacialAssaultScenario);
        check.Ratio(1.4, freeBlastDamage / plainBlastDamage);
        check.Equal(0, glacialAssaultScenario.Stacks(GlacialAssault.Buff));
        check.False(glacialAssaultScenario.TryCast<GlacialBlast>().Succeeded);
    }

    /// <summary>With Glacial Assault and Icy Talons, at 4 stacks Talon Strike is free and strikes 5 times, as with a full 5 Winter Orbs.</summary>
    [Fact]
    public void A_Free_Glacial_Assault_Talon_Strike_Strikes_5_Times()
    {
        var f = StepFights.Rime(talents: [GlacialAssault.TalentId, IcyTalons.TalentId]);
        f.Apply(GlacialAssault.Buff, stacks: 4);

        f.Cast<TalonStrike>(); // with no Winter Orbs
        f.Wait(3.0); // the spikes land

        Assert.Equal(5, f.Damage<TalonStrike>().Count);
    }

    // ---- Icy Flow

    /// <summary>With Icy Flow, a Cold Snap gives a stack for 15 seconds.</summary>
    [Fact]
    public void Icy_Flow_Gains_A_Stack_From_Cold_Snap_For_15_Seconds()
    {
        var f = StepFights.Rime(talents: [IcyFlow.TalentId]);

        f.Cast<ColdSnap>();
        double start = f.CompletedAt("Cold Snap");

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(IcyFlow.Buff));

        f.WaitUntil(start + 14.9);
        check.Equal(1, f.Stacks(IcyFlow.Buff));
        f.WaitUntil(start + 15.1);
        check.Equal(0, f.Stacks(IcyFlow.Buff));
    }

    /// <summary>
    /// With Icy Flow, the next Glacial Blast casts with 50% more haste and 20% more crit chance, and uses up a stack. With Greater
    /// Glacial Blast, it takes 2 seconds before haste instead of 1.5.
    /// </summary>
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(false, 0.2)]
    [InlineData(false, 0.4)]
    [InlineData(true, 0.0)]
    [InlineData(true, 0.2)]
    [InlineData(true, 0.4)]
    public void Icy_Flow_Hastes_And_Crits_The_Next_Glacial_Blast(bool greater, double haste)
    {
        var f = StepFights.Rime(haste: haste, talents: greater ? [IcyFlow.TalentId, GreaterGlacialBlast.TalentId] : [IcyFlow.TalentId]);

        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();
        f.RimeOf().WinterOrbs = 2;

        using var check = new Checks(f);
        check.Equal(2, f.Stacks(IcyFlow.Buff));

        f.Cast<GlacialBlast>();

        check.Seconds((greater ? 2.0 : 1.5) / (1 + haste + 0.5), f.LastCastDuration("Glacial Blast"));
        check.Equal(0.2, f.Damage<GlacialBlast>().First().CritChance);
        check.Equal(1, f.Stacks(IcyFlow.Buff));
    }

    /// <summary>With Icy Flow, the next Ice Comet has 20% more crit chance, and uses up a stack.</summary>
    [Fact]
    public void Icy_Flow_Crits_The_Next_Ice_Comet()
    {
        var f = StepFights.Rime(talents: [IcyFlow.TalentId]);

        f.Cast<ColdSnap>();
        f.RimeOf().WinterOrbs = 2;

        f.Cast<IceComet>();
        f.Wait(4.0); // the comet falls

        using var check = new Checks(f);
        check.Equal(0.2, f.Damage<IceComet>().First().CritChance);
        check.Equal(0, f.Stacks(IcyFlow.Buff));
    }

    // ---- Frostweaver's Wrath

    /// <summary>With Frostweaver's Wrath, gaining a Winter Orb sometimes gives a stack, for 20 seconds.</summary>
    [Fact]
    public void Frostweavers_Wrath_Gives_A_Stack_For_20_Seconds_When_An_Orb_Is_Gained()
    {
        var f = StepFights.Rime(talents: [FrostweaversWrath.TalentId]);
        f.ForceProcs(key => key == FrostweaversWrath.ProcKey ? true : null);

        f.Cast<ColdSnap>(); // gives an orb
        double start = f.CompletedAt("Cold Snap");

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(FrostweaversWrath.Buff));

        f.WaitUntil(start + 19.9);
        check.Equal(1, f.Stacks(FrostweaversWrath.Buff));
        f.WaitUntil(start + 20.1);
        check.Equal(0, f.Stacks(FrostweaversWrath.Buff));
    }

    /// <summary>
    /// Frostweaver's Wrath has a 20% chance to give a stack each time you gain Winter Orbs.
    ///
    /// TODO: check in game whether a gain of several orbs at once rolls once or once per orb (the engine rolls once per gain).
    /// </summary>
    [Fact]
    public void Frostweavers_Wrath_Has_A_20_Percent_Chance_When_Orbs_Are_Gained()
    {
        var f = StepFights.Rime(talents: [FrostweaversWrath.TalentId]);
        var procs = f.ForceProcs(key => key == FrostweaversWrath.ProcKey ? false : null); // only to read the chance

        f.Cast<ColdSnap>(); // gives an orb

        Assert.Equal([0.2], procs.ChancesOf(FrostweaversWrath.ProcKey));
    }

    /// <summary>Frostweaver's Wrath stacks up to 2.</summary>
    [Fact]
    public void Frostweavers_Wrath_Stacks_Up_To_2()
    {
        var f = StepFights.Rime(talents: [FrostweaversWrath.TalentId]);
        f.ForceProcs(key => key == FrostweaversWrath.ProcKey ? true : null);

        ColdSnapWhenReady(f);
        ColdSnapWhenReady(f);
        ColdSnapWhenReady(f); // the third waits 12s for a charge, well within the stacks' 20s

        Assert.Equal(2, f.Stacks(FrostweaversWrath.Buff));
    }

    /// <summary>A Frostweaver's Wrath stack makes the next Glacial Blast or Ice Comet a certain critical strike, and is used up.</summary>
    [Theory]
    [InlineData("Glacial Blast")]
    [InlineData("Ice Comet")]
    public void Frostweavers_Wrath_Makes_The_Next_Spender_A_Certain_Crit(string spender)
    {
        var f = StepFights.Rime(talents: [FrostweaversWrath.TalentId]);
        f.ForceProcs(key => key == FrostweaversWrath.ProcKey ? true : null);

        f.Cast<ColdSnap>(); // the stack
        f.RimeOf().WinterOrbs = 2;

        f.Cast(spender);
        f.Wait(4.0); // the comet falls

        using var check = new Checks(f);
        check.Equal(1.0, f.Damage(spender).First().CritChance);
        check.Equal(0, f.Stacks(FrostweaversWrath.Buff));
    }

    /// <summary>
    /// With Icy Talons and Frostweaver's Wrath, only the first spike of a Talon Strike, or the first wave of a Rising Talons, is the
    /// certain critical strike, and it uses up one stack.
    /// </summary>
    [Theory]
    [InlineData("Talon Strike")]
    [InlineData("Rising Talons")]
    public void Frostweavers_Wrath_Crits_Only_The_First_Spike_Or_Wave(string spender)
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId, FrostweaversWrath.TalentId]);
        f.ForceProcs(key => key == FrostweaversWrath.ProcKey ? true : null);

        f.Cast<ColdSnap>(); // a Frostweaver's Wrath stack
        f.RimeOf().WinterOrbs = 3; // 3 spikes or waves

        f.Cast(spender);
        f.Wait(5.0); // the waves land

        var hits = f.Damage(spender);

        using var check = new Checks(f);
        check.Equal(3, hits.Count);
        check.Equal(1.0, hits[0].CritChance, what: "first hit's crit chance");
        check.Equal(0.0, hits[1].CritChance, what: "second hit's crit chance");
        check.Equal(0.0, hits[2].CritChance, what: "third hit's crit chance");
        check.Equal(0, f.Stacks(FrostweaversWrath.Buff));
    }

    // ---- Navir's Keeper

    /// <summary>With Navir's Keeper, the next 2 Cold Snaps after Flight of the Navir cost no charge: you can cast 4 in a row, not 5.</summary>
    [Fact]
    public void Navirs_Keeper_Makes_The_Next_2_Cold_Snaps_Free_Of_Charges()
    {
        var f = StepFights.Rime(talents: [NavirsKeeper.TalentId]);

        f.Cast<FlightOfTheNavir>();

        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>();
        f.Cast<ColdSnap>(); // the 5th comes 7.5s in, before a charge comes back (12s)

        Assert.False(f.TryCast<ColdSnap>().Succeeded);
    }

    /// <summary>
    /// With Navir's Keeper and Glacial Assault, the Cold Snaps that cost no charge still give Glacial Assault stacks: after Flight of
    /// the Navir, four Cold Snaps in a row give 4 stacks.
    /// </summary>
    [Fact]
    public void Free_Cold_Snaps_From_Navirs_Keeper_Still_Give_Glacial_Assault_Stacks()
    {
        var f = StepFights.Rime(talents: [NavirsKeeper.TalentId, GlacialAssault.TalentId]);

        f.Cast<FlightOfTheNavir>();

        f.Cast<ColdSnap>(); // free
        f.Cast<ColdSnap>(); // free
        f.Cast<ColdSnap>(); // a charge
        f.Cast<ColdSnap>(); // a charge

        Assert.Equal(4, f.Stacks(GlacialAssault.Buff));
    }

    // ---- Cold Shower

    /// <summary>With Cold Shower, Freezing Torrent ticks sometimes throw a free Ice Comet.</summary>
    [Fact]
    public void Cold_Shower_Throws_A_Free_Ice_Comet_From_A_Torrent_Tick()
    {
        var f = StepFights.Rime(talents: [ColdShower.TalentId]);
        f.ForceProcs(key => key == ColdShower.ProcKey ? true : null);

        f.Cast<FreezingTorrent>();
        f.Wait(4.0); // the comets fall

        using var check = new Checks(f);
        check.Equal(f.Damage<FreezingTorrent>().Count, f.Damage<IceComet>().Count);
        check.Equal(0, f.Spends("Winter Orbs").Count);
    }

    /// <summary>With Cold Shower, each Freezing Torrent tick has a 7% chance to throw a free Ice Comet.</summary>
    [Fact]
    public void Cold_Shower_Has_A_7_Percent_Chance_Per_Torrent_Tick()
    {
        var f = StepFights.Rime(talents: [ColdShower.TalentId]);
        var procs = f.ForceProcs(key => key == ColdShower.ProcKey ? false : null); // only to read the chance

        f.Cast<FreezingTorrent>();

        using var check = new Checks(f);
        check.Equal(f.Damage<FreezingTorrent>().Count, procs.ChancesOf(ColdShower.ProcKey).Count);
        check.True(procs.ChancesOf(ColdShower.ProcKey).All(chance => chance == 0.07));
    }

    // ---- Avalanche

    /// <summary>
    /// With Avalanche, a struck Ice Comet has an 18% chance to be followed by 1 more free comet, and 9% by 2.
    ///
    /// The engine rolls twice: any extra comet, then two rather than one among those. The test reads both chances and checks the
    /// game's 18% and 9% from them, whatever the split.
    /// </summary>
    [Fact]
    public void Avalanche_Has_An_18_Percent_Chance_Of_1_Extra_Comet_And_9_Percent_Of_2()
    {
        var f = StepFights.Rime(talents: [Avalanche.TalentId]);
        var procs = f.ForceProcs(key => key == Avalanche.ProcKey ? true : key == Avalanche.SecondProcKey ? false : null);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<IceComet>();
        f.Wait(4.0); // the comets fall

        double anyExtra = procs.ChancesOf(Avalanche.ProcKey).Single();
        double twoRatherThanOne = procs.ChancesOf(Avalanche.SecondProcKey).Single();

        using var check = new Checks(f);
        check.Equal(0.18, anyExtra * (1 - twoRatherThanOne), what: "the chance of 1 extra comet");
        check.Equal(0.09, anyExtra * twoRatherThanOne, what: "the chance of 2 extra comets");
    }

    /// <summary>
    /// Avalanche's extra comets do not trigger Avalanche themselves, however lucky you are.
    ///
    /// TODO: check in game.
    /// </summary>
    [Fact(Skip = "To check in game: Avalanche's own comets cannot trigger Avalanche")]
    public void Avalanche_Extra_Comets_Do_Not_Trigger_Avalanche()
    {
        var f = StepFights.Rime(talents: [Avalanche.TalentId]);
        f.ForceProcs(key => key == Avalanche.ProcKey || key == Avalanche.SecondProcKey ? true : null); // 2 extra comets, every time
        f.RimeOf().WinterOrbs = 2;

        f.Cast<IceComet>();
        f.Wait(4.0); // the comets fall

        Assert.Equal(3, f.Damage<IceComet>().Count);
    }

    // ---- Coalescing Frost (its area damage reduction is in RimeAbilities.cs)

    /// <summary>With Coalescing Frost, Freezing Torrent hits stack the enemy, and the stacks erupt 3 seconds after the last one.</summary>
    [Fact]
    public void Coalescing_Frost_Erupts_3_Seconds_After_The_Last_Stack()
    {
        var f = StepFights.Rime(talents: [CoalescingFrost.TalentId]);

        f.Cast<FreezingTorrent>();
        f.Wait(6.0); // the eruption lands

        AssertSeconds(f.Damage<FreezingTorrent>().Last().Time.TotalSeconds + 3.0, f.Damage("Coalescing Frost").Single().Time.TotalSeconds);
    }

    /// <summary>
    /// The eruption of Coalescing Frost deals damage for each stack, one per Freezing Torrent hit.
    ///
    /// Compared per hit at 0% and 50% haste (6 and 9 hits).
    /// </summary>
    [Fact]
    public void Coalescing_Frost_Eruption_Grows_With_The_Number_Of_Hits()
    {
        double EruptionPerHit(double haste)
        {
            var f = StepFights.Rime(haste: haste, talents: [CoalescingFrost.TalentId]).WithoutDamageSpread();

            f.Cast<FreezingTorrent>();
            f.Wait(6.0); // the eruption lands
            return f.Damage("Coalescing Frost").Single().Value / f.Damage<FreezingTorrent>().Count;
        }

        AssertRatio(1.0, EruptionPerHit(0.5) / EruptionPerHit(0.0));
    }

    /// <summary>With Coalescing Frost, each critical Freezing Torrent hit has a 50% chance to give 2 stacks instead of 1.</summary>
    [Fact]
    public void Coalescing_Frost_Has_A_50_Percent_Chance_Of_2_Stacks_On_A_Critical_Hit()
    {
        var f = StepFights.Rime(talents: [CoalescingFrost.TalentId], crit: 1.0); // every hit is critical
        var procs = f.ForceProcs(key => key == CoalescingFrost.DoubleStackProc ? false : null); // only to read the chance

        f.Cast<FreezingTorrent>();

        using var check = new Checks(f);
        check.Equal(f.Damage<FreezingTorrent>().Count, procs.ChancesOf(CoalescingFrost.DoubleStackProc).Count);
        check.True(procs.ChancesOf(CoalescingFrost.DoubleStackProc).All(chance => chance == 0.5));
    }

    /// <summary>With Coalescing Frost, a critical Freezing Torrent hit sometimes gives 2 stacks instead of 1.</summary>
    [Fact]
    public void Coalescing_Frost_Critical_Torrent_Hits_Can_Give_2_Stacks()
    {
        double Eruption(bool doubled)
        {
            var f = StepFights.Rime(talents: [CoalescingFrost.TalentId], crit: 1.0).WithoutDamageSpread();
            f.ForceProcs(key => key == CoalescingFrost.DoubleStackProc ? doubled : null);

            f.Cast<FreezingTorrent>();
            f.Wait(6.0); // the eruption lands
            return f.Damage("Coalescing Frost").Single().Value;
        }

        AssertRatio(2.0, Eruption(doubled: true) / Eruption(doubled: false));
    }

    // ---- Harrowing Ice (its 31-stack effect on Flight of the Navir is in RimeCooldowns.cs)

    /// <summary>With Harrowing Ice, each Bursting Ice pulse gives a stack, and each pulse deals 3% more damage per stack.</summary>
    [Fact]
    public void Harrowing_Ice_Pulses_Deal_3_Percent_More_For_Each_Earlier_Pulse()
    {
        var f = StepFights.Rime(talents: [HarrowingIce.TalentId]).WithoutDamageSpread();

        f.Cast<BurstingIce>();
        f.Wait(4.0); // the pulses land

        var pulses = f.Damage<BurstingIce>();

        using var check = new Checks(f);
        check.Equal(6, pulses.Count);
        for (int k = 1; k < pulses.Count; k++)
            check.Ratio(1 + 0.03 * k, pulses[k].Value / pulses[0].Value, $"pulse {k + 1}");
    }

    // ---- Cascading Blitz

    /// <summary>With Cascading Blitz, while Ice Blitz is up, each Anima you gain sends a Frost Swallow: 3 for a Frost Bolt. Without Ice Blitz, none.</summary>
    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 0)]
    public void Cascading_Blitz_Sends_A_Swallow_For_Each_Anima_During_Ice_Blitz(bool blitz, int swallows)
    {
        var f = StepFights.Rime(talents: [CascadingBlitz.TalentId]);

        if (blitz) f.Cast<IceBlitz>();

        f.Cast<FrostBolt>();
        f.Wait(2.0); // the swallows land

        Assert.Equal(swallows, f.Damage("Frost Swallows").Count);
    }

    /// <summary>
    /// With Cascading Blitz and Bursting Swallows, during Ice Blitz a swallow's hit can trigger a Bursting Ice pulse, whose Anima sends
    /// another swallow, which can trigger another pulse, and so on until one does not. A Cold Snap sends 3 swallows; if N of them
    /// trigger, there are N pulses, N Anima and 3 + N swallow hits.
    ///
    /// The chance is forced on for the first N rolls and off after: a chain that always triggers never ends.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cascading_Blitz_And_Bursting_Swallows_Chain_Until_A_Swallow_Does_Not_Trigger(int triggers)
    {
        var f = StepFights.Rime(talents: [CascadingBlitz.TalentId, BurstingSwallows.TalentId]);
        int rolls = 0;
        f.ForceProcs(key => key == BurstingSwallows.ProcKey ? rolls++ < triggers : null); // on for the first N rolls only

        f.Cast<IceBlitz>();

        f.Cast<ColdSnap>(); // 1 orb: 3 swallows
        f.Wait(3.0); // the chain ends

        using var check = new Checks(f);
        check.Equal(triggers, f.Damage<BurstingIce>().Count);
        check.Equal(triggers, f.Gains("Anima").Count);
        check.Equal(3 + triggers, f.Damage("Frost Swallows").Count);
    }
}
