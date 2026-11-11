using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's legendary items.
/// </summary>
public class RimeLegendaries
{
    // ---- Skandi's Decree

    /// <summary>
    /// With Skandi's Decree, Bursting Ice lasts 2 seconds longer: its debuff on the target, and the Winter's Embrace it gives you, last 5
    /// seconds instead of 3.
    /// </summary>
    [Theory]
    [InlineData(false, 3.0)]
    [InlineData(true, 5.0)]
    public void Skandis_Decree_Makes_Bursting_Ice_Last_5_Seconds(bool legendary, double duration)
    {
        var f = StepFights.Rime(legendaries: legendary ? [SkandisDecree.LegsItemId] : null);

        f.Cast<BurstingIce>();
        double end = f.CompletedAt("Bursting Ice") + duration;

        using var check = new Checks(f);
        f.WaitUntil(end - 0.1);
        check.True(f.Has(BurstingIce.Debuff, on: f.Enemy));
        check.True(f.Has(BurstingIce.WintersEmbrace));
        f.WaitUntil(end + 0.1);
        check.False(f.Has(BurstingIce.Debuff, on: f.Enemy));
        check.False(f.Has(BurstingIce.WintersEmbrace));
    }

    /// <summary>
    /// With Skandi's Decree, Bursting Ice pulses on its usual rhythm for the whole 5 seconds: once per full hasted half second,
    /// floor(5 / 0.5 x (1 + haste)), plus a last partial pulse when at least 0.1 seconds are left.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void Skandis_Decree_Bursting_Ice_Pulses_Every_Hasted_Half_Second_For_5_Seconds(double haste)
    {
        double tick = 0.5 / (1 + haste);
        int pulses = FullTicks(5.0, tick) + PartialTick(5.0, tick, 0.1);
        var f = StepFights.Rime(haste: haste, legendaries: [SkandisDecree.LegsItemId]);

        f.Cast<BurstingIce>();
        f.Wait(6.0); // the 5 second debuff runs out

        Assert.Equal(pulses, f.Damage<BurstingIce>().Count);
    }

    // ---- Frostwyrm's Spite (the tooltip's "you can now move while channeling" is not testable: the fights do not move)

    /// <summary>
    /// With Frostwyrm's Spite, each tick of Freezing Torrent gives you a stack of Frostwyrm's Spite, which lasts 15 seconds after the last
    /// tick.
    /// </summary>
    [Fact]
    public void Each_Freezing_Torrent_Tick_Gives_A_Frostwyrms_Spite_Stack_For_15_Seconds()
    {
        var f = StepFights.Rime(legendaries: [FrostwyrmsSpite.HelmItemId]);

        f.Cast<FreezingTorrent>();
        f.Wait(1.0); // the last tick lands

        var ticks = f.Damage<FreezingTorrent>();
        double end = ticks.Last().Time.TotalSeconds + 15.0;

        using var check = new Checks(f);
        check.Equal(ticks.Count, f.Stacks(FrostwyrmsSpite.Buff));
        f.WaitUntil(end - 0.1);
        check.Equal(ticks.Count, f.Stacks(FrostwyrmsSpite.Buff));
        f.WaitUntil(end + 0.1);
        check.Equal(0, f.Stacks(FrostwyrmsSpite.Buff));
    }

    /// <summary>Frostwyrm's Spite stacks up to 30: a Freezing Torrent with 28 stacks already up leaves 30.</summary>
    [Fact]
    public void Frostwyrms_Spite_Stacks_Up_To_30()
    {
        var f = StepFights.Rime(legendaries: [FrostwyrmsSpite.HelmItemId]);
        f.Apply(FrostwyrmsSpite.Buff, stacks: 28); // the stacks of earlier torrents

        f.Cast<FreezingTorrent>(); // 6 ticks

        Assert.Equal(30, f.Stacks(FrostwyrmsSpite.Buff));
    }

    /// <summary>
    /// Cold Snap consumes every stack of Frostwyrm's Spite and deals 30% more damage per stack, added up: 1 + 0.3 x stacks.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    public void Cold_Snap_Consumes_Every_Stack_For_30_Percent_More_Damage_Each(int stacks)
    {
        double ColdSnapHit(int stacks, out int stacksLeft)
        {
            var f = StepFights.Rime(legendaries: [FrostwyrmsSpite.HelmItemId]).WithoutDamageSpread();
            if (stacks > 0) f.Apply(FrostwyrmsSpite.Buff, stacks: stacks); // the stacks of earlier torrents

            f.Cast<ColdSnap>();

            stacksLeft = f.Stacks(FrostwyrmsSpite.Buff);
            return f.Damage<ColdSnap>().Single().Value;
        }

        double plainHit = ColdSnapHit(0, out _);
        double spiteHit = ColdSnapHit(stacks, out int stacksLeft);

        using var check = new Checks();
        check.Ratio(1 + 0.3 * stacks, spiteHit / plainHit);
        check.Equal(0, stacksLeft);
    }

    /// <summary>
    /// Cold Snap hits 1 more enemy near its target for each Frostwyrm's Spite stack it consumes, as long as there are enemies: with 2
    /// stacks and 4 more enemies, 2 more hits; with 5 stacks and 2 more enemies, 2.
    /// </summary>
    [Theory]
    [InlineData(2, 5, 2)]
    [InlineData(4, 5, 4)]
    [InlineData(5, 3, 2)]
    public void Cold_Snap_Hits_One_More_Enemy_Per_Stack_Consumed(int stacks, int enemies, int extraHits)
    {
        var f = StepFights.Rime(enemies: enemies, legendaries: [FrostwyrmsSpite.HelmItemId]);
        f.Apply(FrostwyrmsSpite.Buff, stacks: stacks); // the stacks of earlier torrents

        f.Cast<ColdSnap>();

        var spiteHits = f.Damage(FrostwyrmsSpite.DamageName);

        using var check = new Checks(f);
        check.Equal(1, f.Damage<ColdSnap>().Count);
        check.Equal(extraHits, spiteHits.Count);
        check.True(spiteHits.All(hit => hit.Target != f.Enemy.Name), "no extra hit on the target");
        check.Equal(extraHits, spiteHits.Select(hit => hit.Target).Distinct().Count(), what: "enemies hit once each");
    }

    /// <summary>
    /// Each extra hit deals what Cold Snap deals its target, bonus included, up to 3 extra enemies; beyond 3, each deals sqrt(3 / N) of
    /// it, N the number of extra enemies hit. Cold Snap's own hit on its target is not reduced.
    ///
    /// Source: the tooltip ("Frostwyrm's Spite damage is reduced beyond 3 targets") and combat logs (FrostwyrmsSpite.cs).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void Each_Extra_Hit_Deals_Cold_Snaps_Damage_Reduced_Beyond_3_Enemies(int extraEnemies)
    {
        var f = StepFights.Rime(enemies: extraEnemies + 1, legendaries: [FrostwyrmsSpite.HelmItemId]).WithoutDamageSpread();
        f.Apply(FrostwyrmsSpite.Buff, stacks: extraEnemies); // one stack per extra enemy

        f.Cast<ColdSnap>();

        double targetHit = f.Damage<ColdSnap>().Single().Value;
        var spiteHits = f.Damage(FrostwyrmsSpite.DamageName);
        double expected = extraEnemies > 3 ? Math.Sqrt(3.0 / extraEnemies) : 1.0;

        using var check = new Checks(f);
        check.Equal(extraEnemies, spiteHits.Count);
        foreach (var hit in spiteHits) check.Ratio(expected, hit.Value / targetHit, what: $"extra hit on {hit.Target}");
    }

    /// <summary>
    /// Cold Snap's extra hits give no Winter Orb: a Cold Snap that hits 3 more enemies still gives 1.
    /// </summary>
    [Fact]
    public void Cold_Snaps_Extra_Hits_Give_No_Winter_Orb()
    {
        var f = StepFights.Rime(enemies: 4, legendaries: [FrostwyrmsSpite.HelmItemId]);
        f.Apply(FrostwyrmsSpite.Buff, stacks: 3); // one stack per extra enemy

        f.Cast<ColdSnap>();

        using var check = new Checks(f);
        check.Equal(3, f.Damage(FrostwyrmsSpite.DamageName).Count);
        check.Equal(1, f.RimeOf().WinterOrbs);
    }

    // ---- Undulating Spirit

    /// <summary>With Undulating Spirit, Winter's Blessing gives you 2 stacks of Undulating Spirit.</summary>
    [Fact]
    public void Winters_Blessing_Gives_2_Stacks_Of_Undulating_Spirit()
    {
        var f = StepFights.Rime(legendaries: [UndulatingSpirit.ChestItemId]);
        Assert.Equal(0, f.Stacks(UndulatingSpirit.Buff));

        f.Cast<WintersBlessing>();

        Assert.Equal(2, f.Stacks(UndulatingSpirit.Buff));
    }

    /// <summary>
    /// Your next ability that spends Winter Orbs uses up a stack of Undulating Spirit and always triggers a Spirit Refund: you get its orbs
    /// back and a Rising Spirit stack.
    ///
    /// The Spirit Refund's own roll is forced to fail, so the refund can only come from Undulating Spirit.
    /// </summary>
    [Theory]
    [InlineData("Glacial Blast")]
    [InlineData("Ice Comet")]
    public void A_Winter_Orb_Spender_Uses_A_Stack_And_Always_Triggers_A_Spirit_Refund(string spender)
    {
        var f = StepFights.Rime(legendaries: [UndulatingSpirit.ChestItemId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund but Undulating Spirit's
        f.RimeOf().WinterOrbs = 2; // enough to cast it

        f.Cast<WintersBlessing>();

        f.Cast(spender);
        f.Wait(0.5); // the refund lands

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(UndulatingSpirit.Buff));
        check.Equal(2, f.RimeOf().WinterOrbs);
        check.Equal(1, f.Stacks(Hero.RisingSpirit));
    }

    /// <summary>
    /// With a stack of Undulating Spirit up, the spender does not roll for a Spirit Refund: it uses up the stack and gets the refund,
    /// even when the roll would have succeeded.
    /// </summary>
    [Fact]
    public void A_Spender_Uses_A_Stack_Instead_Of_Rolling_For_A_Spirit_Refund()
    {
        var f = StepFights.Rime(legendaries: [UndulatingSpirit.ChestItemId]);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null); // a roll would succeed
        f.RimeOf().WinterOrbs = 2; // enough to cast it

        f.Cast<WintersBlessing>();

        f.Cast<GlacialBlast>();
        f.Wait(0.5); // the refund lands

        using var check = new Checks(f);
        check.Equal(0, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls");
        check.Equal(1, f.Stacks(UndulatingSpirit.Buff));
        check.Equal(2, f.RimeOf().WinterOrbs);
        check.Equal(1, f.Stacks(Hero.RisingSpirit));
    }

    /// <summary>
    /// The 2 stacks give 2 certain Spirit Refunds: of three Glacial Blasts after Winter's Blessing, the first two are refunded and the
    /// third is not.
    ///
    /// The Spirit Refund's own roll is forced to fail.
    /// </summary>
    [Fact]
    public void Two_Stacks_Give_Two_Certain_Spirit_Refunds()
    {
        var f = StepFights.Rime(legendaries: [UndulatingSpirit.ChestItemId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund but Undulating Spirit's
        f.RimeOf().WinterOrbs = 5; // the most you can hold: enough for two, and a third with the refunds

        f.Cast<WintersBlessing>();

        for (int i = 0; i < 3; i++) f.Cast<GlacialBlast>();
        f.Wait(0.5); // the refunds land

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(UndulatingSpirit.Buff));
        check.Equal(3, f.RimeOf().WinterOrbs); // 5, less the third blast's 2
        check.Equal(2, f.Stacks(Hero.RisingSpirit));
    }

    /// <summary>An ability that does not spend Winter Orbs leaves the Undulating Spirit stacks alone.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Cold Snap")]
    [InlineData("Bursting Ice")]
    public void An_Ability_That_Spends_No_Winter_Orbs_Uses_No_Stack(string ability)
    {
        var f = StepFights.Rime(legendaries: [UndulatingSpirit.ChestItemId]);

        f.Cast<WintersBlessing>();

        f.Cast(ability);

        Assert.Equal(2, f.Stacks(UndulatingSpirit.Buff));
    }
}
