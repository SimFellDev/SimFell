using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's abilities, on a bare Gunde against one enemy: each ability's damage, targeting, Rend and buffs, then what they do with each
/// other: Rend from several sources, Reign in Blood with Serrated Edge, Owed in Blood's Rend with Heart Splitter's Exsanguinate and
/// Slaughter, and when a Spirit Refund comes.
///
/// Damage has no spread and no crit unless the test is about it, so the ratios are exact. Rend is read from the enemy, as what it still
/// has to deal, and from its ticks.
/// </summary>
public class GundeAbilities
{
    /// <summary>The main stat, measured as an auto attack swing on a bare Gunde, which deals 30% of it: the unit Owed in Blood's Rend is
    /// worth one of per stack.</summary>
    private static readonly double MainStat = MainStatFromAnAttack();

    private static double MainStatFromAnAttack()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Cast<Attack>();
        return f.Damage<Attack>().First().Value / 0.3;
    }

    /// <summary>All the Rend applied to the enemy so far: what it still has to deal, and what it dealt.</summary>
    private static double RendApplied(StepDriver f) => f.GundeOf().Rend.Remaining(f.Enemy) + f.Damage("Rend").Sum(tick => tick.Value);

    /// <summary>The Rend the enemy, or <paramref name="enemy"/>, still has to deal.</summary>
    private static double RendLeft(StepDriver f, EnemyTarget? enemy = null) => f.GundeOf().Rend.Remaining(enemy ?? f.Enemy);

    // ---- Damage and targeting

    /// <summary>
    /// Each ability deals the share of your main stat its tooltip gives: Double Strike 82.5% per hit, Reaver's Edge 203.17%, Blood Arc
    /// 270.92%, Heart Splitter 470%, Grim Carve 207.6% per spin, Rupture 2126%, Warbound 20%, Butcher's Hook 37%, Bloodbound Spirit 465.3%
    /// per pulse.
    ///
    /// The first hit of one cast, over the main stat measured from an auto attack swing (30% of it).
    /// </summary>
    [Theory]
    [InlineData("Double Strike", 0.825)]
    [InlineData("Reaver's Edge", 2.0317)]
    [InlineData("Blood Arc", 2.7092)]
    [InlineData("Heart Splitter", 4.7)]
    [InlineData("Grim Carve", 2.076)]
    [InlineData("Rupture", 21.26)]
    [InlineData("Warbound", 0.2)]
    [InlineData("Butcher's Hook", 0.37)]
    [InlineData("Bloodbound Spirit", 4.653)]
    public void Each_Ability_Deals_Its_Share_Of_The_Main_Stat(string ability, double coefficient)
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast(ability);
        f.Wait(1.2); // the spins and pulses land

        AssertRatio(coefficient, f.Damage(ability).First().Value / MainStat);
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
    /// One cast at the start of a fight, with time for all its hits; the Rend the enemy has had (still to deal and already dealt) over the
    /// damage of the hits.
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
        f.Wait(1.2); // the spins land
        if (ability == "Bloodbound Spirit") f.Wait(5); // every pulse

        AssertRatio(share, RendApplied(f) / f.Damage(ability).Sum(hit => hit.Value));
    }

    /// <summary>The auto attack and Warbound apply no Rend.</summary>
    [Theory]
    [InlineData("Attack")]
    [InlineData("Warbound")]
    public void Some_Abilities_Apply_No_Rend(string ability)
    {
        var f = StepFights.Gunde();

        f.Cast(ability);

        Assert.Equal(0, RendLeft(f));
    }

    /// <summary>
    /// Butcher's Hook applies no Rend.
    ///
    /// TODO: the game data's text for it has a 50% Rend placeholder, but no combat log shows it applying Rend. Check in game.
    /// </summary>
    [Fact(Skip = "Whether Butcher's Hook applies Rend is unverified (the game data text has a 50% placeholder)")]
    public void Butchers_Hook_Applies_No_Rend()
    {
        var f = StepFights.Gunde();

        f.Cast<ButchersHook>();

        Assert.Equal(0, RendLeft(f));
    }

    /// <summary>
    /// Each application of Rend deals its damage over 10 ticks, one tenth each, every 3 seconds, and haste does not speed them up: after a
    /// Rupture, 10 ticks at 3, 6, ... 30 seconds, each a tenth of its Rend, and none left.
    ///
    /// Times within 50ms.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Rend_Deals_A_Tenth_Every_3_Seconds_For_10_Ticks_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Gunde(haste: haste).WithoutDamageSpread();

        f.Cast<Rupture>();
        double rend = RendLeft(f);
        f.Wait(31); // every tick
        var ticks = f.Damage("Rend");

        using var check = new Checks(f);
        check.Equal(10, ticks.Count);
        for (int i = 0; i < Math.Min(10, ticks.Count); i++)
        {
            check.Equal(3.0 * (i + 1), ticks[i].Time.TotalSeconds, 0.05, what: $"tick {i + 1}'s time");
            check.Ratio(0.1, ticks[i].Value / rend, what: $"tick {i + 1} over the Rend");
        }
        check.Equal(0, RendLeft(f), 0.01);
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
    /// ends after its own 10 ticks. Two Double Strikes, at 0s and 4s, apply equal Rend A: ticks at 3s (A/10), 6s to 30s (2A/10 each) and
    /// 33s (A/10), 11 ticks.
    ///
    /// Times within 50ms.
    /// </summary>
    [Fact]
    public void A_Later_Application_Keeps_The_Enemys_Tick_Rhythm_And_Ends_After_Its_Own_Ticks()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<DoubleStrike>();
        double application = RendLeft(f);
        f.Wait(2.5); // the second Double Strike at 4s
        f.Cast<DoubleStrike>();
        f.Wait(30); // every tick
        var ticks = f.Damage("Rend");

        using var check = new Checks(f);
        check.Equal(11, ticks.Count);
        check.Equal(3.0, ticks[0].Time.TotalSeconds, 0.05);
        check.Equal(33.0, ticks[10].Time.TotalSeconds, 0.05);
        check.Ratio(0.1, ticks[0].Value / application);
        check.Ratio(0.2, ticks[1].Value / application);
        check.Ratio(0.2, ticks[9].Value / application);
        check.Ratio(0.1, ticks[10].Value / application);
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

    /// <summary>
    /// Rend adds up: the applications of different abilities are one pool, and every tick deals a tenth of what the applications still
    /// ticking hold. After a Reaver's Edge (50% of its damage as Rend) and a Heart Splitter (100%), the first tick is a tenth of the two.
    /// </summary>
    [Fact]
    public void Applications_Of_Different_Abilities_Tick_Together()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<ReaversEdge>();
        f.Cast<HeartSplitter>();
        double applied = 0.5 * f.Damage<ReaversEdge>().First().Value + 1.0 * f.Damage<HeartSplitter>().First().Value;
        f.Wait(1.5); // the first tick, at 3s

        AssertRatio(0.1, f.Damage("Rend")[0].Value / applied);
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
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<BloodArc>();
        double rendBefore = RendApplied(f);

        f.Cast(ability);
        f.Wait(1.2); // the spins land

        using var check = new Checks(f);
        check.Ratio(share + 0.2, (RendApplied(f) - rendBefore) / f.Damage(ability).Sum(hit => hit.Value));
        check.False(f.Has(BloodArc.SerratedEdge));
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
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<BloodArc>();
        double rendBefore = RendApplied(f);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast<BloodboundSpirit>();

        using var check = new Checks(f);
        check.True(f.Has(BloodArc.SerratedEdge));
        f.Wait(6); // every pulse
        check.Ratio(1.0, (RendApplied(f) - rendBefore) / f.Damage<BloodboundSpirit>().Sum(hit => hit.Value));
    }

    /// <summary>
    /// Serrated Edge lasts 8 seconds: still on 7.5 seconds after the Blood Arc, gone at 8.5.
    ///
    /// TODO: the tooltip says 10 seconds; the combat logs show 8 and the test follows them. Check in game.
    /// </summary>
    [Fact]
    public void Serrated_Edge_Lasts_8_Seconds()
    {
        var f = StepFights.Gunde();

        f.Cast<BloodArc>();

        f.WaitUntil(7.5);
        Assert.True(f.Has(BloodArc.SerratedEdge));
        f.WaitUntil(8.5);
        Assert.False(f.Has(BloodArc.SerratedEdge));
    }

    // ---- Reign in Blood

    /// <summary>
    /// Reign in Blood makes your abilities add 30% more of their damage to Rend for 12 seconds: a Double Strike (50% Rend) 11 seconds after
    /// it adds 80%, and one 13 seconds after it 50%.
    /// </summary>
    [Theory]
    [InlineData(11.0, 0.8)]
    [InlineData(13.0, 0.5)]
    public void Reign_In_Blood_Adds_30_Percent_To_Rend_For_12_Seconds(double wait, double share)
    {
        var f = StepFights.Gunde();

        f.Cast<ReignInBlood>();
        f.Wait(wait);
        double rendBefore = RendApplied(f);

        f.Cast<DoubleStrike>();

        AssertRatio(share, (RendApplied(f) - rendBefore) / f.Damage<DoubleStrike>().Sum(hit => hit.Value));
    }

    /// <summary>
    /// Serrated Edge and Reign in Blood add up on the same ability: Heart Splitter under Reign in Blood with Serrated Edge applies
    /// 100% + 30% + 20% of its damage to Rend.
    /// </summary>
    [Fact]
    public void Reign_In_Blood_And_Serrated_Edge_Add_Up()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<BloodArc>(); // Serrated Edge
        f.Cast<ReignInBlood>();
        double rendBefore = RendApplied(f);

        f.Cast<HeartSplitter>();
        double heartSplitterRend = RendApplied(f) - rendBefore;

        AssertRatio(1.5, heartSplitterRend / f.Damage<HeartSplitter>().First().Value);
    }

    // ---- Owed in Blood (its stacks are in GundeResourceEconomy.cs)

    /// <summary>
    /// Owed in Blood turns all your stacks into Rend stacks on the target (a Rend stack is a main stat of Rend still to deal): up to 15
    /// stacks one each, then 0.85 per stack beyond, and all its stacks are used up. 10 -> 10, 15 -> 15, 50 -> 15 + 0.85 x 35,
    /// 150 -> 15 + 0.85 x 135.
    ///
    /// The Rend over the main stat within 0.01.
    /// </summary>
    [Theory]
    [InlineData(10, 10.0)]
    [InlineData(15, 15.0)]
    [InlineData(50, 15 + 0.85 * 35)]
    [InlineData(150, 15 + 0.85 * 135)]
    public void Owed_In_Blood_Turns_Stacks_Into_Rend_Stacks(int stacks, double rendStacks)
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: stacks);

        f.Cast<OwedInBlood>();

        using var check = new Checks(f);
        check.Equal(rendStacks, RendLeft(f) / MainStat, 0.01);
        check.Equal(0, f.Stacks(OwedInBlood.Buff));
    }

    /// <summary>
    /// The Rend stacks Owed in Blood adds are Rend like any other: the enemy shows them as stacks of Rend, and they tick in tenths. With 10
    /// stacks of Owed in Blood, the enemy holds 10 stacks of Rend, and the first tick is a tenth of their 10 x main stat.
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
        check.Ratio(10.0 / 10, f.Damage("Rend")[0].Value / MainStat);
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
    /// Exsanguinate counts the Rend still to deal, not the Rend already dealt: after a Rupture and one of its 10 ticks, a Heart Splitter's
    /// Exsanguinate is 30% of 90% of the Rupture's Rend plus the strike's own.
    ///
    /// Within 0.001 of the strike's damage.
    /// </summary>
    [Fact]
    public void Exsanguinate_Leaves_Out_The_Rend_Already_Dealt()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<Rupture>();
        double ruptureRend = RendLeft(f);
        f.WaitUntil(3.1); // the first tick
        Assert.Single(f.Damage("Rend"));

        f.Cast<HeartSplitter>();
        double strike = f.Damage<HeartSplitter>().First().Value;
        double exsanguinate = f.Damage("Exsanguinate").First().Value;

        AssertRatio(0.3 * (0.9 * ruptureRend + strike) / strike, exsanguinate / strike);
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
    /// Heart Splitter's Exsanguinate counts the Rend Owed in Blood put on the target: it deals 30% of that Rend (10 stacks, 10 x main stat)
    /// and the strike's own.
    ///
    /// Within 0.001 of the strike's damage.
    /// </summary>
    [Fact]
    public void Exsanguinate_Counts_The_Rend_From_Owed_In_Blood()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10);

        f.Cast<OwedInBlood>();

        f.Cast<HeartSplitter>();
        double strike = f.Damage<HeartSplitter>().First().Value;
        double exsanguinate = f.Damage("Exsanguinate").First().Value;

        AssertRatio(0.3 * (10 * MainStat + strike) / strike, exsanguinate / strike);
    }

    // ---- Slaughter

    /// <summary>
    /// Slaughter consumes the Rend on every enemy it hits and deals 160% of it as a bleed over 3 seconds: 10 ticks, 0.3 seconds apart, from
    /// 0.3 seconds after the cast. After a Reaver's Edge (no Open Wounds), the Rend is gone, and the bleed ticks a tenth of 160% of it.
    ///
    /// Times within 20ms.
    /// </summary>
    [Fact]
    public void Slaughter_Consumes_Rend_And_Bleeds_160_Percent_Of_It()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<ReaversEdge>();
        double rend = RendLeft(f);
        double cast = f.Seconds();

        f.Cast<Slaughter>();

        using var check = new Checks(f);
        check.Equal(0, RendLeft(f));
        f.Wait(3.5); // every tick of the bleed
        var ticks = f.Ticks(Slaughter.DotEffectId);
        check.Equal(10, ticks.Count);
        for (int i = 0; i < Math.Min(10, ticks.Count); i++)
        {
            check.Equal(cast + 0.3 * (i + 1), ticks[i].Time.TotalSeconds, 0.02, what: $"tick {i + 1}'s time");
            check.Ratio(1.6 / 10, ticks[i].Value / rend, what: $"tick {i + 1} over the Rend");
        }
        check.Equal(0, f.Damage("Rend").Count);
    }

    /// <summary>Slaughter takes the Rend still to deal: after a Reaver's Edge and one of its 10 ticks, the bleed is 160% of the 9 tenths
    /// left.</summary>
    [Fact]
    public void Slaughter_Takes_Only_The_Rend_Still_To_Deal()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<ReaversEdge>();
        double rend = RendLeft(f);
        f.WaitUntil(3.1); // the first tick

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed

        AssertRatio(1.6 * 0.9, f.Ticks(Slaughter.DotEffectId).Sum(tick => tick.Value) / rend);
    }

    /// <summary>
    /// Slaughter hits every enemy around you that has Rend, each with 160% of its own Rend: a Double Strike on one enemy and a Heart
    /// Splitter on the other, then Slaughter, and each bleeds 160% of its own.
    /// </summary>
    [Fact]
    public void Slaughter_Bleeds_Each_Enemy_For_Its_Own_Rend()
    {
        var f = StepFights.Gunde(enemies: 2).WithoutDamageSpread();
        var other = f.Enemies()[1];

        f.Cast<DoubleStrike>();
        f.Cast<HeartSplitter>(other);
        double firstEnemyRend = RendLeft(f), otherEnemyRend = RendLeft(f, other);

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed
        var ticks = f.Ticks(Slaughter.DotEffectId);

        using var check = new Checks(f);
        check.Equal(20, ticks.Count);
        check.Ratio(1.6, ticks.Where(tick => tick.Target == f.Enemy.Name).Sum(tick => tick.Value) / firstEnemyRend);
        check.Ratio(1.6, ticks.Where(tick => tick.Target == other.Name).Sum(tick => tick.Value) / otherEnemyRend);
    }

    /// <summary>
    /// Slaughter turns the Rend from Owed in Blood into its bleed: 160% of it. With 10 stacks of Owed in Blood (10 x main stat of Rend),
    /// the bleed deals 1.6 x 10 x main stat.
    /// </summary>
    [Fact]
    public void Slaughter_Bleeds_The_Rend_From_Owed_In_Blood()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10);

        f.Cast<OwedInBlood>();

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed
        double bleed = f.Ticks(Slaughter.DotEffectId).Sum(tick => tick.Value);

        AssertRatio(1.6 * 10, bleed / MainStat);
    }

    // ---- Rupture

    /// <summary>
    /// Rupture's Open Wounds makes your next Slaughter deal 20% more to that enemy, and is used up by it. It lasts 18 seconds: a Slaughter
    /// right after the Rupture bleeds 160% x 120% of the Rend, and one 19 seconds later 160% only.
    /// </summary>
    [Theory]
    [InlineData(0.0, 1.6 * 1.2)]
    [InlineData(19.0, 1.6)]
    public void Open_Wounds_Adds_20_Percent_To_The_Next_Slaughter_For_18_Seconds(double wait, double multiplier)
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<Rupture>();
        f.Wait(wait);
        double rend = RendLeft(f);

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed

        using var check = new Checks(f);
        check.Ratio(multiplier, f.Ticks(Slaughter.DotEffectId).Sum(tick => tick.Value) / rend);
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
    /// Bloodbound Spirit makes your Heart Splitter and Grim Carve deal 25% more damage for 20 seconds, and gives Spirit of Heroism (30%
    /// haste): during it, both deal 1.25 times as much and haste is 30%; 21 seconds later, both are back to normal and haste to 0.
    /// </summary>
    [Fact]
    public void Bloodbound_Spirit_Makes_Heart_Splitter_And_Grim_Carve_Deal_25_Percent_More_For_20_Seconds()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();

        f.Cast<HeartSplitter>();
        f.Cast<GrimCarve>();
        f.Wait(1.2); // the spins land
        double plainHeartSplitter = f.Damage<HeartSplitter>().First().Value, plainGrimCarve = f.Damage<GrimCarve>().First().Value;
        f.Wait(16); // the cooldowns come back
        f.Set(Hero.SpiritPoints, 100); // enough to cast Bloodbound Spirit

        f.Cast<BloodboundSpirit>();

        using var check = new Checks(f);
        check.Equal(0.3, f.Haste(), 0.001);
        f.Cast<HeartSplitter>();
        f.Cast<GrimCarve>();
        f.Wait(1.2); // the spins land
        check.Ratio(1.25, f.Damage<HeartSplitter>()[1].Value / plainHeartSplitter);
        check.Ratio(1.25, f.Damage<GrimCarve>()[3].Value / plainGrimCarve);

        f.Wait(20);
        f.Cast<HeartSplitter>();
        check.Ratio(1.0, f.Damage<HeartSplitter>()[2].Value / plainHeartSplitter);
        check.Equal(0.0, f.Haste(), 0.001);
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
