using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's abilities, on a bare Gunde against one enemy: what each one does, who it hits, how much Rend it applies and how Rend ticks,
/// then what they do with each other: Rend from several sources, Reign in Blood with Serrated Edge, Owed in Blood's Rend with Heart
/// Splitter's Exsanguinate and Slaughter, and when a Spirit Refund comes.
///
/// No test checks a damage value: a hit is compared with another hit, and Rend with the damage it comes from. Damage has no spread and no
/// crit unless the test is about it, so the ratios are exact. Rend is read from its ticks once they are all out: "the Rend applied" is
/// what the ticks of a fight add up to.
/// </summary>
public class GundeAbilities
{
    /// <summary>All the Rend that <paramref name="stacks"/> stacks of Owed in Blood put on the enemy.</summary>
    private static double OwedInBloodRend(int stacks) => GundeRend.Of(f =>
    {
        f.Apply(OwedInBlood.Buff, stacks: stacks);
        f.Cast<OwedInBlood>();
    });

    // ---- Damage and targeting

    /// <summary>Every damaging ability deals damage.</summary>
    [Theory]
    [InlineData("Double Strike")]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Heart Splitter")]
    [InlineData("Grim Carve")]
    [InlineData("Rupture")]
    [InlineData("Warbound")]
    [InlineData("Butcher's Hook")]
    [InlineData("Bloodbound Spirit")]
    public void Damaging_Abilities_Deal_Damage(string ability)
    {
        var f = StepFights.Gunde();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast(ability);
        f.Wait(7); // the spins and pulses land

        Assert.True(f.Damage(ability).Sum(hit => hit.Value) > 0);
    }

    /// <summary>Double Strike hits twice, Grim Carve spins three times and Bloodbound Spirit pulses five times, each with the same damage.</summary>
    [Theory]
    [InlineData("Double Strike", 2)]
    [InlineData("Grim Carve", 3)]
    [InlineData("Bloodbound Spirit", 5)]
    public void Multi_Hit_Abilities_Hit_The_Expected_Number_Of_Times(string ability, int hits)
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast(ability);
        f.Wait(7); // every hit lands
        var damage = f.Damage(ability);

        using var check = new Checks(f);
        check.Equal(hits, damage.Count);
        foreach (var hit in damage) check.Ratio(1.0, hit.Value / damage[0].Value, what: "a hit over the first");
    }

    /// <summary>Heart Splitter, Rupture, Double Strike, Warbound and Butcher's Hook hit their target alone: with three enemies, only the
    /// first is hit.</summary>
    [Theory]
    [InlineData("Heart Splitter")]
    [InlineData("Rupture")]
    [InlineData("Double Strike")]
    [InlineData("Warbound")]
    [InlineData("Butcher's Hook")]
    public void Single_Target_Abilities_Hit_Only_Their_Target(string ability)
    {
        var f = StepFights.Gunde(enemies: 3);

        f.Cast(ability);

        Assert.All(f.Damage(ability), hit => Assert.Equal(f.Enemy.Name, hit.Target));
    }

    /// <summary>
    /// Reaver's Edge and Blood Arc hit every enemy near you, and beyond 8 of them each hit is weaker: multiplied by sqrt(8 / enemies).
    /// Grim Carve's spins do the same beyond 5 enemies, and Bloodbound Spirit beyond 1.
    ///
    /// One cast at the threshold and one over it; the first hit on each enemy. Every enemy is hit, and the hits over the threshold are in
    /// the ratio of the square roots.
    /// </summary>
    [Theory]
    [InlineData("Reaver's Edge", 8, 12)]
    [InlineData("Blood Arc", 8, 12)]
    [InlineData("Grim Carve", 5, 10)]
    [InlineData("Bloodbound Spirit", 1, 4)]
    public void Area_Abilities_Hit_Every_Enemy_And_Fall_Off_Beyond_Their_Threshold(string ability, int threshold, int many)
    {
        List<StepEvent> FirstHits(int enemies)
        {
            var f = StepFights.Gunde(enemies: enemies).WithoutDamageSpread();
            f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit
            f.Cast(ability);
            f.Wait(1.2); // the spins and pulses land
            return f.Damage(ability).Take(enemies).ToList();
        }

        var atThresholdHits = FirstHits(threshold);
        var overThresholdHits = FirstHits(many);

        using var check = new Checks();
        check.Equal(threshold, atThresholdHits.Select(hit => hit.Target).Distinct().Count());
        check.Equal(many, overThresholdHits.Select(hit => hit.Target).Distinct().Count());
        check.Ratio(Math.Sqrt((double)threshold / many), overThresholdHits[0].Value / atThresholdHits[0].Value);
    }

    // ---- Rend

    /// <summary>
    /// Abilities that apply Rend add a share of the damage they deal to it: Double Strike, Reaver's Edge, Blood Arc, Grim Carve and
    /// Rupture 50%, Heart Splitter and Bloodbound Spirit 100%.
    ///
    /// One cast at the start of a fight; the damage of all the Rend ticks over the damage of the hits.
    /// </summary>
    [Theory]
    [InlineData("Double Strike", 0.5)]
    [InlineData("Reaver's Edge", 0.5)]
    [InlineData("Blood Arc", 0.5)]
    [InlineData("Grim Carve", 0.5)]
    [InlineData("Rupture", 0.5)]
    [InlineData("Heart Splitter", 1.0)]
    [InlineData("Bloodbound Spirit", 1.0)]
    public void Abilities_Add_A_Share_Of_Their_Damage_To_Rend(string ability, double share)
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast(ability);
        f.Wait(40); // every Rend tick: 30 seconds after the last application (Bloodbound Spirit's last pulse is at 6s)
        double rend = f.Damage("Rend").Sum(tick => tick.Value);

        AssertRatio(share, rend / f.Damage(ability).Sum(hit => hit.Value));
    }

    /// <summary>
    /// An ability with several hits adds each hit's own damage, times its share, to Rend: with damage spread, so the hits differ, the Rend
    /// is the share of the sum of the hits, not of the first hit times their number.
    ///
    /// Double Strike (2 hits) and Grim Carve (3 spins) at 50%, Bloodbound Spirit (5 pulses) at 100%, with the damage spread on.
    ///
    /// TODO: fails for Bloodbound Spirit: with spread, its Rend is 1.2% above its pulses' logged damage; without spread the two agree.
    /// The cause is not found (its pulses are the one ability whose hits are snapshotted).
    /// </summary>
    [Theory]
    [InlineData("Double Strike", 0.5)]
    [InlineData("Grim Carve", 0.5)]
    [InlineData("Bloodbound Spirit", 1.0)]
    public void Each_Hit_Of_An_Ability_Adds_Its_Own_Damage_To_Rend(string ability, double share)
    {
        var f = StepFights.Gunde();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast(ability);
        f.Wait(40); // every Rend tick
        var hits = f.Damage(ability);

        using var check = new Checks(f);
        check.True(hits.Max(hit => hit.Value) > hits.Min(hit => hit.Value), "the hits differ");
        check.Ratio(share, f.Damage("Rend").Sum(tick => tick.Value) / hits.Sum(hit => hit.Value));
    }

    /// <summary>
    /// A hit takes Reign in Blood's Rend share as it lands, not when its cast starts: Reign in Blood ending between the start of a Grim
    /// Carve and its spins takes its share off the spins. A Grim Carve 0.1 seconds before Reign in Blood ends, whose 3 spins all land
    /// after the end (the first 0.17 seconds in), adds 50% of every spin to Rend.
    /// </summary>
    [Fact]
    public void A_Hit_Takes_Reign_In_Blood_Rend_Share_As_It_Lands()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<ReignInBlood>();
        f.WaitUntil(f.CompletedAt("Reign in Blood") + 12 - 0.1);
        f.Cast<GrimCarve>();
        f.Wait(40); // every Rend tick
        var spins = f.Damage<GrimCarve>();

        using var check = new Checks(f);
        check.True(spins.All(spin => spin.Time.TotalSeconds > f.CompletedAt("Reign in Blood") + 12), "every spin lands after the end");
        check.Ratio(0.5, f.Damage("Rend").Sum(tick => tick.Value) / spins.Sum(spin => spin.Value));
    }

    /// <summary>The auto attack and Warbound apply no Rend.</summary>
    [Theory]
    [InlineData("Attack")]
    [InlineData("Warbound")]
    public void Some_Abilities_Apply_No_Rend(string ability)
    {
        var f = StepFights.Gunde();

        f.Cast(ability);
        f.Wait(40); // every tick there would be

        Assert.Empty(f.Damage("Rend"));
    }

    /// <summary>
    /// Butcher's Hook applies no Rend.
    /// </summary>
    [Fact]
    public void Butchers_Hook_Applies_No_Rend()
    {
        var f = StepFights.Gunde();

        f.Cast<ButchersHook>();
        f.Wait(40); // every tick there would be

        Assert.Empty(f.Damage("Rend"));
    }

    /// <summary>
    /// Each application of Rend deals its damage over 10 ticks, one tenth each, every 3 seconds, and haste does not speed them up: after a
    /// Rupture, 10 equal ticks at 3, 6, ... 30 seconds.
    ///
    /// Times within 50ms.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Rend_Deals_10_Equal_Ticks_Every_3_Seconds_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Gunde(haste: haste).WithoutDamageSpread();

        f.Cast<Rupture>();
        f.Wait(31); // every tick
        var ticks = f.Damage("Rend");

        using var check = new Checks(f);
        check.Equal(10, ticks.Count);
        for (int i = 0; i < Math.Min(10, ticks.Count); i++)
        {
            check.Equal(3.0 * (i + 1), ticks[i].Time.TotalSeconds, 0.05, what: $"tick {i + 1}'s time");
            check.Ratio(1.0, ticks[i].Value / ticks[0].Value, what: $"tick {i + 1} over the first");
        }
    }

    /// <summary>
    /// Rend never crits and takes no damage multipliers: its ticks are a tenth of the damage the hits dealt, crit included. At 100% crit, a
    /// Rupture's first tick is 5% of its hit (50% Rend over 10 ticks), so it was not rolled for a crit again.
    /// </summary>
    [Fact]
    public void Rend_Ticks_Never_Crit()
    {
        var f = StepFights.Gunde(crit: 1.0).WithoutDamageSpread();

        f.Cast<Rupture>();
        f.Wait(3.1); // the first tick

        AssertRatio(0.05, f.Damage("Rend")[0].Value / f.Damage<Rupture>().First().Value);
    }

    /// <summary>
    /// Each application of Rend ticks on its own: a later application joins the enemy's tick rhythm (every 3 seconds from the first) and
    /// ends after its own 10 ticks. Two Double Strikes, at 0s and 4s, apply equal Rend: ticks at 3s (one share), 6s to 30s (two shares
    /// each) and 33s (one share), 11 ticks.
    ///
    /// Times within 50ms.
    /// </summary>
    [Fact]
    public void A_Later_Application_Keeps_The_Enemys_Tick_Rhythm_And_Ends_After_Its_Own_Ticks()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<DoubleStrike>();
        f.Wait(2.5); // the second Double Strike at 4s
        f.Cast<DoubleStrike>();
        f.Wait(30); // every tick
        var ticks = f.Damage("Rend");

        using var check = new Checks(f);
        check.Equal(11, ticks.Count);
        check.Equal(3.0, ticks[0].Time.TotalSeconds, 0.05);
        check.Equal(33.0, ticks[10].Time.TotalSeconds, 0.05);
        check.Ratio(2.0, ticks[1].Value / ticks[0].Value, what: "a tick of both applications over a tick of one");
        check.Ratio(2.0, ticks[9].Value / ticks[0].Value, what: "the last tick of both over a tick of one");
        check.Ratio(1.0, ticks[10].Value / ticks[0].Value, what: "the last tick over a tick of one");
    }

    /// <summary>
    /// Every enemy has its own Rend, ticking on its own timer from its first application: a Double Strike on each of two enemies, one
    /// global cooldown (1.5s) apart, and their first ticks come 3 seconds after each, at 3s and 4.5s.
    ///
    /// Times within 50ms.
    /// </summary>
    [Fact]
    public void Each_Enemy_Has_Its_Own_Rend()
    {
        var f = StepFights.Gunde(enemies: 2);
        var other = f.Enemies()[1];

        f.Cast<DoubleStrike>();
        f.Cast<DoubleStrike>(other);
        f.Wait(3); // the first tick on each
        var firstEnemyTick = f.Damage("Rend").First(tick => tick.Target == f.Enemy.Name);
        var otherEnemyTick = f.Damage("Rend").First(tick => tick.Target == other.Name);

        using var check = new Checks(f);
        check.Equal(3.0, firstEnemyTick.Time.TotalSeconds, 0.05);
        check.Equal(4.5, otherEnemyTick.Time.TotalSeconds, 0.05);
    }

    // ---- Blood Arc

    /// <summary>
    /// Blood Arc grants Serrated Edge: your next Heart Splitter, Rupture, Reaver's Edge or Grim Carve adds 20% more of its damage to Rend,
    /// and uses it up.
    ///
    /// Blood Arc's own Rend is taken out of the reading: the ability's Rend over its damage is its share plus 0.2.
    /// </summary>
    [Theory]
    [InlineData("Heart Splitter", 1.0)]
    [InlineData("Rupture", 0.5)]
    [InlineData("Reaver's Edge", 0.5)]
    [InlineData("Grim Carve", 0.5)]
    public void Serrated_Edge_Adds_20_Percent_To_The_Next_Rend_Ability(string ability, double share)
    {
        bool usedUp = false;
        double rend = GundeRend.Added(f => f.Cast<BloodArc>(), f =>
        {
            f.Cast(ability);
            usedUp = !f.Has(BloodArc.SerratedEdge);
        }, out var f);

        using var check = new Checks(f);
        check.Ratio(share + 0.2, rend / f.Damage(ability).Sum(hit => hit.Value));
        check.True(usedUp);
    }

    /// <summary>Reign in Blood, Owed in Blood, Slaughter and Warbound do not use Serrated Edge up.</summary>
    [Theory]
    [InlineData("Reign in Blood")]
    [InlineData("Owed in Blood")]
    [InlineData("Slaughter")]
    [InlineData("Warbound")]
    public void Some_Abilities_Leave_Serrated_Edge(string ability)
    {
        var f = StepFights.Gunde();
        f.Apply(OwedInBlood.Buff, stacks: 1); // enough to cast Owed in Blood

        f.Cast<BloodArc>();

        f.Cast(ability);

        Assert.True(f.Has(BloodArc.SerratedEdge));
    }

    /// <summary>Serrated Edge does not interact with Bloodbound Spirit: it neither adds to its Rend (100% of its damage) nor is used up.</summary>
    [Fact]
    public void Serrated_Edge_Does_Not_Interact_With_Bloodbound_Spirit()
    {
        bool stillUp = false;
        double rend = GundeRend.Added(f => f.Cast<BloodArc>(), f =>
        {
            f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit
            f.Cast<BloodboundSpirit>();
            stillUp = f.Has(BloodArc.SerratedEdge);
        }, out var f);

        using var check = new Checks(f);
        check.True(stillUp);
        check.Ratio(1.0, rend / f.Damage<BloodboundSpirit>().Sum(hit => hit.Value));
    }

    /// <summary>
    /// Serrated Edge lasts 8 seconds from the Blood Arc.
    /// </summary>
    [Fact]
    public void Serrated_Edge_Lasts_8_Seconds()
    {
        var f = StepFights.Gunde();

        f.Cast<BloodArc>();
        double end = f.CompletedAt("Blood Arc") + 8;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(BloodArc.SerratedEdge));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(BloodArc.SerratedEdge));
    }

    // ---- Reign in Blood

    /// <summary>
    /// Reign in Blood makes your abilities add 30% more of their damage to Rend for 12 seconds from the cast: a Double Strike (50% Rend)
    /// 0.1 seconds before the end adds 80%, and one 0.1 seconds after it 50%.
    /// </summary>
    [Theory]
    [InlineData(-0.1, 0.8)]
    [InlineData(0.1, 0.5)]
    public void Reign_In_Blood_Adds_30_Percent_To_Rend_For_12_Seconds(double offset, double share)
    {
        double RendAfterReign(bool strike, out StepDriver fight)
        {
            fight = StepFights.Gunde().WithoutDamageSpread();
            fight.Cast<ReignInBlood>();
            fight.WaitUntil(fight.CompletedAt("Reign in Blood") + 12 + offset);
            if (strike) fight.Cast<DoubleStrike>();
            return GundeRend.Dealt(fight);
        }

        double rend = RendAfterReign(true, out var f) - RendAfterReign(false, out _);

        AssertRatio(share, rend / f.Damage<DoubleStrike>().Sum(hit => hit.Value));
    }

    /// <summary>
    /// Serrated Edge and Reign in Blood add up on the same ability: Heart Splitter under Reign in Blood with Serrated Edge applies
    /// 100% + 30% + 20% of its damage to Rend.
    /// </summary>
    [Fact]
    public void Reign_In_Blood_And_Serrated_Edge_Add_Up()
    {
        double rend = GundeRend.Added(f =>
        {
            f.Cast<BloodArc>(); // Serrated Edge
            f.Cast<ReignInBlood>();
        }, f => f.Cast<HeartSplitter>(), out var f);

        AssertRatio(1.5, rend / f.Damage<HeartSplitter>().First().Value);
    }

    // ---- Owed in Blood (its stacks are in GundeResourceEconomy.cs)

    /// <summary>
    /// Owed in Blood turns all your stacks into Rend stacks on the target (a Rend stack is what one stack gives, as Rend): one each, at
    /// every count, and all its stacks are used up (Blueprints BP_Gunde_DotTransfer_Orb and GA_Gunde_DotTransfer: each stack saves a
    /// Strength of Rend).
    /// </summary>
    [Theory]
    [InlineData(10, 10.0)]
    [InlineData(15, 15.0)]
    [InlineData(50, 50.0)]
    [InlineData(150, 150.0)]
    public void Owed_In_Blood_Turns_Stacks_Into_Rend_Stacks(int stacks, double rendStacks)
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: stacks);

        f.Cast<OwedInBlood>();
        int stacksLeft = f.Stacks(OwedInBlood.Buff);
        double rend = GundeRend.Dealt(f);

        using var check = new Checks(f);
        check.Ratio(rendStacks, rend / OwedInBloodRend(1));
        check.Equal(0, stacksLeft);
    }

    /// <summary>
    /// The Rend stacks Owed in Blood adds are Rend like any other: the enemy shows them as stacks of Rend, and they tick in tenths. With 10
    /// stacks of Owed in Blood, the enemy holds 10 stacks of Rend, and the first tick is 10 times the first tick of one stack.
    /// </summary>
    [Fact]
    public void Owed_In_Blood_Stacks_Are_Rend_Stacks_That_Tick()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10);

        f.Cast<OwedInBlood>();

        using var check = new Checks(f);
        check.Equal(10, f.Stacks(Rend.Debuff, on: f.Enemy));
        f.Wait(3); // the first tick
        double firstTick = f.Damage("Rend")[0].Value;
        var oneStack = StepFights.Gunde().WithoutDamageSpread();
        oneStack.Apply(OwedInBlood.Buff, stacks: 1);
        oneStack.Cast<OwedInBlood>();
        oneStack.Wait(3); // the first tick
        check.Ratio(10, firstTick / oneStack.Damage("Rend")[0].Value);
    }

    // ---- Heart Splitter

    /// <summary>
    /// Heart Splitter exsanguinates the target: it deals 30% of the Rend still on the target, counting the Rend its own strike just added.
    /// On a fresh enemy, Exsanguinate is 30% of the strike's Rend, 100% of the strike.
    /// </summary>
    [Fact]
    public void Exsanguinate_Deals_30_Percent_Of_The_Rend_Including_The_Strikes_Own()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<HeartSplitter>();

        AssertRatio(0.3, f.Damage("Exsanguinate").First().Value / f.Damage<HeartSplitter>().First().Value);
    }

    /// <summary>
    /// Exsanguinate counts the Rend still to deal, not the Rend already dealt: after a Rupture (50% of its hit as Rend) and one of its 10
    /// ticks, a Heart Splitter's Exsanguinate is 30% of 90% of the Rupture's Rend plus the strike's own (100% of it).
    /// </summary>
    [Fact]
    public void Exsanguinate_Leaves_Out_The_Rend_Already_Dealt()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<Rupture>();
        f.WaitUntil(3.1); // the first tick
        Assert.Single(f.Damage("Rend"));

        f.Cast<HeartSplitter>();
        double strikeRend = f.Damage<HeartSplitter>().First().Value;
        double ruptureRendLeft = 0.9 * 0.5 * f.Damage<Rupture>().First().Value;
        double expectedExsanguinate = 0.3 * (ruptureRendLeft + strikeRend);

        AssertRatio(expectedExsanguinate, f.Damage("Exsanguinate").First().Value);
    }

    /// <summary>Exsanguinate never crits and takes no damage multipliers: at 100% crit, it is 30% of the (crit) strike's Rend.</summary>
    [Fact]
    public void Exsanguinate_Never_Crits()
    {
        var f = StepFights.Gunde(crit: 1.0).WithoutDamageSpread();

        f.Cast<HeartSplitter>();

        AssertRatio(0.3, f.Damage("Exsanguinate").First().Value / f.Damage<HeartSplitter>().First().Value);
    }

    /// <summary>
    /// Heart Splitter's Exsanguinate counts the Rend Owed in Blood put on the target: it deals 30% of that Rend (10 stacks) and the
    /// strike's own.
    /// </summary>
    [Fact]
    public void Exsanguinate_Counts_The_Rend_From_Owed_In_Blood()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10);

        f.Cast<OwedInBlood>();

        f.Cast<HeartSplitter>();
        double strikeRend = f.Damage<HeartSplitter>().First().Value;
        double expectedExsanguinate = 0.3 * (OwedInBloodRend(10) + strikeRend);

        AssertRatio(expectedExsanguinate, f.Damage("Exsanguinate").First().Value);
    }

    // ---- Slaughter

    /// <summary>
    /// Slaughter consumes the Rend on every enemy it hits and deals 160% of it as a bleed over 3 seconds: 10 equal ticks, 0.3 seconds
    /// apart, from 0.3 seconds after the cast. After a Reaver's Edge (no Open Wounds), the Rend is gone (no Rend tick), and the bleed
    /// deals 160% of the Rend a Reaver's Edge applies.
    ///
    /// Times within 20ms.
    /// </summary>
    [Fact]
    public void Slaughter_Consumes_Rend_And_Bleeds_160_Percent_Of_It()
    {
        double rend = GundeRend.Of(f => f.Cast<ReaversEdge>());
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<ReaversEdge>();
        double cast = f.Seconds();

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed
        var ticks = f.Ticks(Slaughter.DotEffectId);

        using var check = new Checks(f);
        check.Equal(10, ticks.Count);
        for (int i = 0; i < Math.Min(10, ticks.Count); i++)
        {
            check.Equal(cast + 0.3 * (i + 1), ticks[i].Time.TotalSeconds, 0.02, what: $"tick {i + 1}'s time");
            check.Ratio(1.0, ticks[i].Value / ticks[0].Value, what: $"tick {i + 1} over the first");
        }
        check.Ratio(1.6, ticks.Sum(tick => tick.Value) / rend, what: "the bleed over the Rend");
        check.Equal(0, f.Damage("Rend").Count);
    }

    /// <summary>Slaughter takes the Rend still to deal: after a Reaver's Edge and one of its 10 ticks, the bleed is 160% of the 9 tenths
    /// left.</summary>
    [Fact]
    public void Slaughter_Takes_Only_The_Rend_Still_To_Deal()
    {
        double rend = GundeRend.Of(f => f.Cast<ReaversEdge>());
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<ReaversEdge>();
        f.WaitUntil(3.1); // the first tick

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed

        AssertRatio(1.6 * 0.9 * rend, f.Ticks(Slaughter.DotEffectId).Sum(tick => tick.Value));
    }

    /// <summary>
    /// Slaughter hits every enemy around you that has Rend, each with 160% of its own Rend: a Double Strike on one enemy and a Heart
    /// Splitter on the other, then Slaughter, and each bleeds 160% of its own.
    /// </summary>
    [Fact]
    public void Slaughter_Bleeds_Each_Enemy_For_Its_Own_Rend()
    {
        var rendFight = StepFights.Gunde(enemies: 2).WithoutDamageSpread();
        rendFight.Cast<DoubleStrike>();
        rendFight.Cast<HeartSplitter>(rendFight.Enemies()[1]);
        double firstEnemyRend = GundeRend.Dealt(rendFight, rendFight.Enemy), otherEnemyRend = GundeRend.Dealt(rendFight, rendFight.Enemies()[1]);

        var f = StepFights.Gunde(enemies: 2).WithoutDamageSpread();
        var other = f.Enemies()[1];
        f.Cast<DoubleStrike>();
        f.Cast<HeartSplitter>(other);

        double RendLeft(EnemyTarget enemy, double total) =>
            total - f.Damage("Rend").Where(tick => tick.Target == enemy.Name).Sum(tick => tick.Value);
        double firstEnemyLeft = RendLeft(f.Enemy, firstEnemyRend), otherEnemyLeft = RendLeft(other, otherEnemyRend);

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed
        var ticks = f.Ticks(Slaughter.DotEffectId);

        using var check = new Checks(f);
        check.Equal(20, ticks.Count);
        check.Ratio(1.6 * firstEnemyLeft, ticks.Where(tick => tick.Target == f.Enemy.Name).Sum(tick => tick.Value));
        check.Ratio(1.6 * otherEnemyLeft, ticks.Where(tick => tick.Target == other.Name).Sum(tick => tick.Value));
    }

    /// <summary>
    /// Slaughter turns the Rend from Owed in Blood into its bleed: 160% of it. With 10 stacks of Owed in Blood, the bleed deals 1.6 times
    /// their Rend.
    /// </summary>
    [Fact]
    public void Slaughter_Bleeds_The_Rend_From_Owed_In_Blood()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10);

        f.Cast<OwedInBlood>();

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed

        AssertRatio(1.6 * OwedInBloodRend(10), f.Ticks(Slaughter.DotEffectId).Sum(tick => tick.Value));
    }

    /// <summary>
    /// Slaughter's bleed takes none of the general damage amplification: it is 160% of the Rend consumed whatever your crit chance,
    /// Expertise and haste, none of its ticks crits, and the only amplification in it is what the Rend already holds from the hits that
    /// applied it.
    ///
    /// A Reaver's Edge, then Slaughter: the bleed is 1.6 x 50% of the Reaver's Edge's damage (the Rend it applied), and no tick is a crit.
    /// </summary>
    [Theory]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(0.0, 0.3, 0.0)]
    [InlineData(0.0, 0.0, 0.5)]
    public void Slaughter_Takes_No_Damage_Amplification_Of_Its_Own(double crit, double expertise, double haste)
    {
        var f = StepFights.Gunde(crit: crit, expertise: expertise, haste: haste).WithoutDamageSpread();

        f.Cast<ReaversEdge>();
        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed
        var ticks = f.Ticks(Slaughter.DotEffectId);

        double expectedDamage = 1.6 * 0.5 * f.Damage<ReaversEdge>().Sum(hit => hit.Value);
        double actualDamage = ticks.Sum(tick => tick.Value);

        using var check = new Checks(f);
        check.Ratio(expectedDamage, actualDamage);
        check.False(ticks.Any(tick => tick.IsCrit), "a crit tick");
    }

    // ---- Rupture

    /// <summary>
    /// Rupture's Open Wounds makes your next Slaughter deal 20% more to that enemy, and is used up by it. It lasts 18 seconds from the
    /// Rupture: a Slaughter 0.1 seconds before the end bleeds 160% x 120% of the Rend left, and one 0.1 seconds after it 160% only.
    /// </summary>
    [Theory]
    [InlineData(-0.1, 1.6 * 1.2)]
    [InlineData(0.1, 1.6)]
    public void Open_Wounds_Adds_20_Percent_To_The_Next_Slaughter_For_18_Seconds(double offset, double multiplier)
    {
        double rupturesRend = GundeRend.Of(f => f.Cast<Rupture>());
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<Rupture>();
        f.WaitUntil(f.CompletedAt("Rupture") + 18 + offset);
        double rendLeft = rupturesRend - f.Damage("Rend").Sum(tick => tick.Value);

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed

        using var check = new Checks(f);
        check.Ratio(multiplier * rendLeft, f.Ticks(Slaughter.DotEffectId).Sum(tick => tick.Value));
        check.False(f.Has(Rupture.OpenWounds, on: f.Enemy));
    }

    // ---- Bloodbound Spirit (its Spirit cost and Spirit of Heroism are in Generic/Ultimate.cs)

    /// <summary>
    /// Bloodbound Spirit pulses as it emerges and every 1.5 seconds for 6 seconds, whatever your haste: 5 pulses 0.035s, 1.535s, 3.035s,
    /// 4.535s and 6.035s after the cast.
    ///
    /// Within 10ms. Spirit of Heroism adds 30% haste during it, so the pulses at 50% haste are checked against the same times.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Bloodbound_Spirit_Pulses_Every_1_5_Seconds_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Gunde(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast<BloodboundSpirit>();
        f.Wait(7); // every pulse
        var times = f.Damage<BloodboundSpirit>().Select(hit => hit.Time.TotalSeconds).ToList();

        using var check = new Checks(f);
        check.Equal(5, times.Count);
        for (int i = 0; i < Math.Min(5, times.Count); i++) check.Equal(0.035 + 1.5 * i, times[i], 0.01, what: $"pulse {i + 1}'s time");
    }

    /// <summary>
    /// Bloodbound Spirit makes your Heart Splitter and Grim Carve deal 25% more damage for 20 seconds from the cast: 0.1 seconds before
    /// the end both deal 1.25 times a plain hit, 0.1 seconds after it a plain hit.
    ///
    /// Grim Carve's first spin lands 0.3 seconds into its cast, so before the end it is cast 0.5 seconds early.
    /// </summary>
    [Theory]
    [InlineData("Heart Splitter", -0.1, 1.25)]
    [InlineData("Heart Splitter", 0.1, 1.0)]
    [InlineData("Grim Carve", -0.5, 1.25)]
    [InlineData("Grim Carve", 0.1, 1.0)]
    public void Bloodbound_Spirit_Makes_Heart_Splitter_And_Grim_Carve_Deal_25_Percent_More_For_20_Seconds(string ability, double offset,
        double multiplier)
    {
        var plainScenario = StepFights.Gunde().WithoutDamageSpread();
        plainScenario.Cast(ability);
        double plainHit = plainScenario.Damage(ability).First().Value;

        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit
        f.Cast<BloodboundSpirit>();
        f.WaitUntil(f.CompletedAt("Bloodbound Spirit") + 20 + offset);

        f.Cast(ability);

        AssertRatio(multiplier, f.Damage(ability).First().Value / plainHit);
    }

    // ---- Spirit Refund (the rest is in GundeResourceEconomy.cs and Generic/SpiritRefund.cs)

    /// <summary>
    /// A Spirit Refund comes with the first hit of the cast, not after its last: a Grim Carve's refund, and its Spirit Point, are there
    /// after its first spin.
    ///
    /// The cast is started and read at 0.3s, after the first spin only, since a whole cast would end after the last.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Comes_With_The_First_Hit()
    {
        var f = StepFights.Gunde(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Remove(OwedInBlood.Buff); // the stack the engine starts the fight with

        f.StartCast<GrimCarve>();
        f.Wait(0.3);

        using var check = new Checks(f);
        check.Equal(1, f.Damage<GrimCarve>().Count);
        check.Equal(1, f.Amount(Hero.SpiritPoints), 0.01);
    }
}
