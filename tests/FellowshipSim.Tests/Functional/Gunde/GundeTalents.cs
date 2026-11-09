using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using FellowshipSim.Core.Heroes.Gunde.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's talents, one at a time, as their tooltips describe them.
///
/// Damage has no spread unless the test says otherwise, and random procs are forced, the chance of each read from its rolls. A
/// talent's bonus is compared with a plain cast of the same ability, or with the main stat measured from an auto attack swing (30% of
/// it). Talents no combat log has shown are marked unverified in the engine; the tests follow the tooltips.
/// </summary>
public class GundeTalents
{
    /// <summary>The main stat, measured as an auto attack swing on a bare Gunde, which deals 30% of it.</summary>
    private static readonly double MainStat = MainStatFromAnAttack();

    private static double MainStatFromAnAttack()
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Cast<Attack>();
        return f.Damage<Attack>().First().Value / 0.3;
    }

    /// <summary>The first hit of a plain cast of <typeparamref name="T"/> on one enemy: what a talent's bonus is compared with.</summary>
    private static double PlainHit<T>() where T : Ability
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Cast<T>();
        f.Wait(0.3); // its first hit lands
        return f.Damage<T>().First().Value;
    }

    /// <summary>All the Rend applied to the enemy so far: what it still has to deal, and what it dealt.</summary>
    private static double RendApplied(StepDriver f) => f.GundeOf().Rend.Remaining(f.Enemy) + f.Damage("Rend").Sum(tick => tick.Value);

    // ---- Death's Arc

    /// <summary>
    /// With Death's Arc, each Blood Arc has a 30% chance to reset its own cooldown at once and give your next Blood Arc 100% more crit
    /// chance.
    /// </summary>
    [Fact]
    public void Deaths_Arc_Resets_Blood_Arc_And_Gives_The_Next_One_100_Percent_Crit()
    {
        var f = StepFights.Gunde(talents: [DeathsArc.TalentId]);
        var procs = f.ForceProcs(key => key == DeathsArc.ProcKey ? true : null);

        f.Cast<BloodArc>();

        Assert.All(procs.ChancesOf(DeathsArc.ProcKey), chance => Assert.Equal(0.3, chance, 1e-9));
        Assert.True(f.IsReady<BloodArc>());

        f.Cast<BloodArc>();

        var hits = f.Damage<BloodArc>();
        Assert.Equal(1.0, hits[1].CritChance - hits[0].CritChance, 1e-9);
    }

    /// <summary>
    /// Death's Arc's 100% more crit chance is for the next Blood Arc only: after a reset, the second Blood Arc has 100% more crit chance
    /// than the first, and a third one, after its cooldown, none.
    ///
    /// The reset is forced on for the first Blood Arc, then off.
    /// </summary>
    [Fact]
    public void Deaths_Arcs_Crit_Is_Used_Up_By_One_Blood_Arc()
    {
        var f = StepFights.Gunde(talents: [DeathsArc.TalentId]);
        f.ForceProcs(key => key == DeathsArc.ProcKey ? true : null); // the first Blood Arc resets itself

        f.Cast<BloodArc>();
        f.ForceProcs(key => key == DeathsArc.ProcKey ? false : null); // no more resets

        f.Cast<BloodArc>();
        f.Wait(9); // its cooldown
        f.Cast<BloodArc>();

        var hits = f.Damage<BloodArc>();
        using var check = new Checks(f);
        check.Equal(1.0, hits[1].CritChance - hits[0].CritChance);
        check.Equal(0.0, hits[2].CritChance - hits[0].CritChance);
    }

    // ---- Raven's Precision

    /// <summary>
    /// With Raven's Precision, each Blood Feather you pick up makes an explosion of blood that deals 37.4% of your main stat to nearby
    /// enemies.
    ///
    /// The feather comes from the first Rend tick of a Reaver's Edge (3s), forced.
    /// </summary>
    [Fact]
    public void Ravens_Precision_Explodes_On_Every_Blood_Feather()
    {
        var f = StepFights.Gunde(talents: [RavensPrecision.TalentId]).WithoutDamageSpread();
        f.ForceProcs(key => key == Core.Heroes.Gunde.Gunde.FeatherProcKey ? true : null);

        f.Cast<ReaversEdge>();
        f.Wait(3.0); // the first Rend tick, and its feather

        var explosions = f.Ticks(RavensPrecision.DamageEffectId);
        Assert.Single(explosions);
        AssertRatio(0.374, explosions[0].Value / MainStat);
    }

    /// <summary>Raven's Precision's explosion deals less beyond 5 enemies: sqrt(5 / enemies) of its damage on each.</summary>
    [Fact]
    public void Ravens_Precision_Falls_Off_Beyond_5_Enemies()
    {
        double Explosion(int enemies)
        {
            var f = StepFights.Gunde(enemies: enemies, talents: [RavensPrecision.TalentId]).WithoutDamageSpread();
            f.ForceProcs(key => key == Core.Heroes.Gunde.Gunde.FeatherProcKey ? true : null);
            f.Cast<ReaversEdge>();
            f.Wait(3.0); // the first Rend tick, and its feather
            return f.Ticks(RavensPrecision.DamageEffectId)[0].Value;
        }

        AssertRatio(Math.Sqrt(5.0 / 10.0), Explosion(10) / Explosion(5));
    }

    // ---- Grim Harvest

    /// <summary>
    /// With Grim Harvest, Heart Splitter can reset Grim Carve's cooldown and give your next Grim Carve 100% more crit chance: after a Grim
    /// Carve, a Heart Splitter whose roll succeeds makes Grim Carve ready at once, and its spins crit 100% more often.
    /// </summary>
    [Fact]
    public void Grim_Harvest_Resets_Grim_Carve_And_Gives_The_Next_One_100_Percent_Crit()
    {
        var f = StepFights.Gunde(talents: [GrimHarvest.TalentId]);
        f.ForceProcs(key => key == GrimHarvest.ProcKey ? false : null); // no reset by the first Grim Carve's

        f.Cast<GrimCarve>();
        f.Wait(1.5); // its spins
        f.ForceProcs(key => key == GrimHarvest.ProcKey ? true : null); // Heart Splitter's roll succeeds
        f.Cast<HeartSplitter>();

        Assert.True(f.IsReady<GrimCarve>());

        f.Cast<GrimCarve>();
        f.Wait(1.5); // its spins

        var spins = f.Damage<GrimCarve>();
        Assert.Equal(1.0, spins[3].CritChance - spins[0].CritChance, 1e-9);
    }

    /// <summary>Grim Harvest has a 6% chance on each cast of Reaver's Edge, Blood Arc, Heart Splitter and Rupture: one roll each.</summary>
    [Theory]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Heart Splitter")]
    [InlineData("Rupture")]
    public void Grim_Harvest_Rolls_A_6_Percent_Chance_On_Each_Cast(string ability)
    {
        var f = StepFights.Gunde(talents: [GrimHarvest.TalentId]);
        var procs = f.ForceProcs(_ => null); // only to read the chance

        f.Cast(ability);

        var chances = procs.ChancesOf(GrimHarvest.ProcKey);
        Assert.Single(chances);
        Assert.Equal(0.06, chances[0], 1e-9);
    }

    // ---- Sundered Flesh

    /// <summary>
    /// With Sundered Flesh, when your Rend holds more damage than the enemy has health left, the enemy takes it at once: an enemy with 1.4
    /// times a Reaver's Edge's hit as health takes the whole Rend (half the hit) in one tick after the hit. Without the talent it takes
    /// none at once.
    /// </summary>
    [Fact]
    public void Sundered_Flesh_Deals_Rend_At_Once_When_It_Exceeds_Health()
    {
        double plainHit = PlainHit<ReaversEdge>();
        StepDriver Fight(int[] talents)
        {
            var f = StepFights.Gunde(talents: talents).WithoutDamageSpread();
            f.Enemy.MaxHealth = 1.4 * plainHit; // an enemy the hit leaves with less health than its Rend
            f.Enemy.CurrentHealth = 1.4 * plainHit;
            f.Cast<ReaversEdge>();
            return f;
        }

        var sunderedScenario = Fight([SunderedFlesh.TalentId]);
        var plainScenario = Fight([]);

        using var check = new Checks();
        check.Equal(1, sunderedScenario.Damage("Rend").Count);
        check.Ratio(0.5, sunderedScenario.Damage("Rend")[0].Value / plainHit);
        check.Equal(0, sunderedScenario.GundeOf().Rend.Remaining(sunderedScenario.Enemy));
        check.Equal(0, plainScenario.Damage("Rend").Count);
    }

    // ---- Crimson Strikes

    /// <summary>
    /// With Crimson Strikes, Blood Arc makes your next ability that applies Rend deal 15% more damage, until used: a Reaver's Edge 20
    /// seconds later deals 15% more, and the next one is plain again.
    /// </summary>
    [Fact]
    public void Crimson_Strikes_Empowers_The_Next_Rend_Ability_By_15_Percent_Until_Used()
    {
        double plainReaverDamage = PlainHit<ReaversEdge>();
        var f = StepFights.Gunde(talents: [CrimsonStrikes.TalentId]).WithoutDamageSpread();

        f.Cast<BloodArc>();
        f.Wait(20);

        f.Cast<ReaversEdge>();
        f.Wait(5); // its cooldown
        f.Cast<ReaversEdge>();

        var hits = f.Damage<ReaversEdge>();
        using var check = new Checks(f);
        check.Ratio(1.15, hits[0].Value / plainReaverDamage);
        check.Ratio(1.0, hits[1].Value / plainReaverDamage);
    }

    /// <summary>Crimson Strikes empowers every hit of the next ability: each of Grim Carve's three spins deals 15% more.</summary>
    [Fact]
    public void Crimson_Strikes_Empowers_Every_Spin_Of_Grim_Carve()
    {
        double plainSpinDamage = PlainHit<GrimCarve>();
        var f = StepFights.Gunde(talents: [CrimsonStrikes.TalentId]).WithoutDamageSpread();

        f.Cast<BloodArc>();

        f.Cast<GrimCarve>();
        f.Wait(1.5); // its spins

        Assert.All(f.Damage<GrimCarve>(), spin => AssertRatio(1.15, spin.Value / plainSpinDamage));
    }

    /// <summary>
    /// Crimson Strikes leaves Bloodbound Spirit alone: Bloodbound Spirit deals its plain damage and does not use it up, so a Reaver's Edge
    /// after it deals 15% more.
    /// </summary>
    [Fact]
    public void Crimson_Strikes_Does_Not_Interact_With_Bloodbound_Spirit()
    {
        var plainScenario = StepFights.Gunde().WithoutDamageSpread();
        plainScenario.Set(Hero.SpiritPoints, 100); // enough to cast it

        plainScenario.Cast<BloodboundSpirit>();
        plainScenario.Wait(1); // its first pulse
        double plainSpiritDamage = plainScenario.Damage<BloodboundSpirit>().First().Value;
        double plainReaverDamage = PlainHit<ReaversEdge>();

        var crimsonScenario = StepFights.Gunde(talents: [CrimsonStrikes.TalentId]).WithoutDamageSpread();

        crimsonScenario.Cast<BloodArc>();
        crimsonScenario.Set(Hero.SpiritPoints, 100); // enough to cast it

        crimsonScenario.Cast<BloodboundSpirit>();
        crimsonScenario.Wait(1); // its first pulse
        crimsonScenario.Cast<ReaversEdge>();
        double crimsonSpiritDamage = crimsonScenario.Damage<BloodboundSpirit>().First().Value;
        double crimsonReaverDamage = crimsonScenario.Damage<ReaversEdge>().First().Value;

        using var check = new Checks(crimsonScenario);
        check.Ratio(1.0, crimsonSpiritDamage / plainSpiritDamage);
        check.Ratio(1.15, crimsonReaverDamage / plainReaverDamage);
    }

    /// <summary>
    /// Crimson Strikes and Serrated Edge both empower the next ability that applies Rend: after a Blood Arc, a Reaver's Edge deals 15% more,
    /// and adds 50% + 20% of that stronger hit to Rend.
    /// </summary>
    [Fact]
    public void Crimson_Strikes_And_Serrated_Edge_Empower_The_Same_Cast()
    {
        double plainReaverDamage = PlainHit<ReaversEdge>();
        var f = StepFights.Gunde(talents: [CrimsonStrikes.TalentId]).WithoutDamageSpread();

        f.Cast<BloodArc>();
        double rendBefore = RendApplied(f);

        f.Cast<ReaversEdge>();
        double reaverDamage = f.Damage<ReaversEdge>().First().Value;

        using var check = new Checks(f);
        check.Ratio(1.15, reaverDamage / plainReaverDamage);
        check.Ratio(0.7, (RendApplied(f) - rendBefore) / reaverDamage);
    }

    // ---- Deep Rend

    /// <summary>
    /// With Deep Rend, your Rend ticks 10% faster, dealing 10% more damage: after a Rupture, 11 ticks every 2.733 seconds (82 server
    /// frames) instead of 10 every 3, each a tenth of the Rend applied, so 110% of it in all.
    ///
    /// The tick times are checked within 50ms.
    /// </summary>
    [Fact]
    public void Deep_Rend_Ticks_10_Percent_Faster_For_10_Percent_More()
    {
        var f = StepFights.Gunde(talents: [DeepRend.TalentId]).WithoutDamageSpread();

        f.Cast<Rupture>();
        double applied = 0.5 * f.Damage<Rupture>().First().Value;
        f.Wait(32); // every tick

        var ticks = f.Damage("Rend");
        using var check = new Checks(f);
        check.Equal(11, ticks.Count);
        for (int i = 0; i < Math.Min(11, ticks.Count); i++)
            check.Equal(2.733 * (i + 1), ticks[i].Time.TotalSeconds, 0.05, what: $"tick {i + 1}");
        check.Ratio(1.1, ticks.Sum(tick => tick.Value) / applied);
    }

    /// <summary>
    /// Deep Rend does not change the Rend stacks Owed in Blood adds: 10 stacks of Owed in Blood are still 10 times your main stat of Rend
    /// to deal, dealt in 11 ticks.
    ///
    /// The Rend left over the main stat within 0.01.
    /// </summary>
    [Fact]
    public void Deep_Rend_Does_Not_Change_The_Rend_Stacks_Owed_In_Blood_Adds()
    {
        var f = StepFights.Gunde(talents: [DeepRend.TalentId]).WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10);

        f.Cast<OwedInBlood>();

        using var check = new Checks(f);
        check.Equal(10, f.GundeOf().Rend.Remaining(f.Enemy) / MainStat, 0.01);
        f.Wait(32); // every tick
        check.Equal(11, f.Damage("Rend").Count);
        check.Ratio(10, f.Damage("Rend").Sum(tick => tick.Value) / MainStat);
    }

    /// <summary>
    /// Slaughter bleeds 160% of the Rend still to deal, Deep Rend's extra included: after a Reaver's Edge, the bleed is 160% of 110% of the
    /// Rend applied.
    /// </summary>
    [Fact]
    public void Slaughter_Counts_Deep_Rends_Extra_Damage()
    {
        var f = StepFights.Gunde(talents: [DeepRend.TalentId]).WithoutDamageSpread();

        f.Cast<ReaversEdge>();
        double applied = 0.5 * f.Damage<ReaversEdge>().First().Value;

        f.Cast<Slaughter>();
        f.Wait(3.5); // every tick of the bleed

        AssertRatio(1.6 * 1.1, f.Ticks(Slaughter.DotEffectId).Sum(tick => tick.Value) / applied);
    }

    // ---- Darkening Hearts

    /// <summary>
    /// With Darkening Hearts, Heart Splitter deals 10% more direct damage, and has 100% more crit chance against an enemy at 30% health or
    /// less: at 20% health its crit chance is 100% above that at 40%.
    /// </summary>
    [Fact]
    public void Darkening_Hearts_Adds_10_Percent_Damage_And_100_Percent_Crit_Below_30_Percent_Health()
    {
        double plainSplitterDamage = PlainHit<HeartSplitter>();
        double CritChance(double health)
        {
            var f = StepFights.Gunde(talents: [DarkeningHearts.TalentId]);
            f.SetHealth(health);
            f.Cast<HeartSplitter>();
            return f.Damage<HeartSplitter>()[0].CritChance;
        }

        var darkeningScenario = StepFights.Gunde(talents: [DarkeningHearts.TalentId]).WithoutDamageSpread();

        darkeningScenario.Cast<HeartSplitter>();
        double darkeningSplitterDamage = darkeningScenario.Damage<HeartSplitter>().First().Value;

        using var check = new Checks();
        check.Ratio(1.1, darkeningSplitterDamage / plainSplitterDamage);
        check.Equal(1.0, CritChance(0.2) - CritChance(0.4));
    }

    // ---- Murder of Crows

    /// <summary>With Murder of Crows, Rupture gives you 2 stacks of Murder of Crows.</summary>
    [Fact]
    public void Rupture_Gives_2_Stacks_Of_Murder_Of_Crows()
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        Assert.Equal(0, f.Stacks(MurderOfCrows.Buff));

        f.Cast("Rupture");

        Assert.Equal(2, f.Stacks(MurderOfCrows.Buff));
    }

    /// <summary>
    /// Your next ability that applies Rend uses up a stack of Murder of Crows and always triggers a Spirit Refund: you get a Rising
    /// Spirit stack.
    ///
    /// The Spirit Refund's own roll is forced to fail, so the refund can only come from Murder of Crows.
    /// </summary>
    [Theory]
    [InlineData("Double Strike")]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Grim Carve")]
    [InlineData("Heart Splitter")]
    public void An_Ability_That_Applies_Rend_Uses_A_Stack_And_Always_Triggers_A_Spirit_Refund(string ability)
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund but Murder of Crows'
        f.FillResources(); // enough to cast it

        f.Cast("Rupture");

        f.Cast(ability);
        f.Wait(1); // its first hit lands

        using var check = new Checks(f);
        check.Equal(1, f.Stacks(MurderOfCrows.Buff));
        check.Equal(1, f.Stacks(Hero.RisingSpirit));
    }

    /// <summary>
    /// With a stack of Murder of Crows up, an ability that applies Rend does not roll for a Spirit Refund: it uses up the stack and gets
    /// the refund, even when the roll would have succeeded.
    ///
    /// The roll is forced to succeed, so an ability that rolled would keep the stack. Rupture's own roll is counted before.
    /// </summary>
    [Fact]
    public void An_Ability_Uses_A_Stack_Instead_Of_Rolling_For_A_Spirit_Refund()
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null); // a roll would succeed
        f.FillResources(); // enough to cast it

        f.Cast("Rupture");
        f.Wait(1); // its first hit lands
        int rupturesRolls = procs.ChancesOf(Hero.SpiritRefundProc).Count;

        f.Cast("Double Strike");
        f.Wait(1); // its first hit lands

        using var check = new Checks(f);
        check.Equal(rupturesRolls, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls");
        check.Equal(1, f.Stacks(MurderOfCrows.Buff));
        check.Equal(2, f.Stacks(Hero.RisingSpirit)); // Rupture's, and Double Strike's
    }

    /// <summary>
    /// The 2 stacks give 2 certain Spirit Refunds: of three abilities that apply Rend after Rupture, the first two are refunded and the
    /// third is not.
    ///
    /// The Spirit Refund's own roll is forced to fail, as in the test above.
    /// </summary>
    [Fact]
    public void Two_Stacks_Give_Two_Certain_Spirit_Refunds()
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund but Murder of Crows'
        f.FillResources(); // enough to cast them

        f.Cast("Rupture");

        f.Cast("Double Strike");
        f.Cast("Blood Arc");
        f.Cast("Heart Splitter");
        f.Wait(1); // their first hits land

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(MurderOfCrows.Buff));
        check.Equal(2, f.Stacks(Hero.RisingSpirit));
    }

    /// <summary>An ability that does not apply Rend leaves the Murder of Crows stacks alone.</summary>
    [Theory]
    [InlineData("Jawbreaker")]
    [InlineData("Reign in Blood")]
    public void An_Ability_That_Applies_No_Rend_Uses_No_Stack(string ability)
    {
        var f = StepFights.Gunde(talents: [MurderOfCrows.TalentId]);
        f.FillResources(); // enough to cast it

        f.Cast("Rupture");

        f.Cast(ability);
        f.Wait(1); // any hit lands

        Assert.Equal(2, f.Stacks(MurderOfCrows.Buff));
    }

    // ---- Massacre

    /// <summary>
    /// With Massacre, each Rend your Slaughter uses up gives 0.25% Spirit for 8 seconds, stacking: Slaughter on three enemies with Rend
    /// gives 0.75%, gone 9 seconds later.
    /// </summary>
    [Fact]
    public void Massacre_Gives_A_Quarter_Percent_Spirit_Per_Rend_Consumed_For_8_Seconds()
    {
        var f = StepFights.Gunde(enemies: 3, talents: [Massacre.TalentId]);

        f.Cast<ReaversEdge>();

        f.Cast<Slaughter>();

        Assert.Equal(0.0075, f.Spirit(), 1e-9);
        f.Wait(9);
        Assert.Equal(0.0, f.Spirit(), 1e-9);
    }

    // ---- Slayer's Grin

    /// <summary>
    /// With Slayer's Grin, each Blood Feather you pick up takes 0.4 seconds off Rupture's cooldown: after a Rupture, a Reaver's Edge with
    /// a Spirit Refund (5 feathers) makes Rupture ready 2 seconds sooner, at 58 seconds.
    ///
    /// Rupture's ready time is the engine's, read within 20ms as the Reaver's Edge's cast returns (3s): the feathers were picked up at 2s,
    /// and a Rend tick's feather would come later.
    /// </summary>
    [Fact]
    public void Slayers_Grin_Takes_0_4_Seconds_Off_Rupture_Per_Feather()
    {
        var f = StepFights.Gunde(spirit: 0.2, talents: [SlayersGrin.TalentId]);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund from Rupture

        f.Cast<Rupture>();
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null); // a refund from Reaver's Edge

        f.Cast<ReaversEdge>();

        double ruptureReadyAt = f.Seconds() + f.Hero.GetCooldownRemains(f.Ability<Rupture>().Name, f.Now);
        Assert.Equal(60.0 - 2.0, ruptureReadyAt, 0.02);
    }

    // ---- Frenzied Reign

    /// <summary>
    /// With Frenzied Reign, Reign in Blood makes your Rend abilities add 25% more of their direct damage to Rend: a Double Strike during
    /// Reign in Blood adds 50% + 30% + 25% of its damage.
    /// </summary>
    [Fact]
    public void Frenzied_Reign_Adds_25_Percent_To_Rend_During_Reign_In_Blood()
    {
        var f = StepFights.Gunde(talents: [FrenziedReign.TalentId]);

        f.Cast<ReignInBlood>();

        f.Cast<DoubleStrike>();

        AssertRatio(1.05, RendApplied(f) / f.Damage<DoubleStrike>().Sum(hit => hit.Value));
    }

    /// <summary>
    /// Every source of Rend adds up on one Heart Splitter: its own 100%, Reign in Blood's 30%, Frenzied Reign's 25%, Serrated Edge's 20%
    /// and Superior Serration's 10%, so 185% of its damage.
    ///
    /// 185% is the figure seen in a combat log.
    /// </summary>
    [Fact]
    public void Heart_Splitter_Under_Every_Rend_Bonus_Applies_185_Percent()
    {
        var f = StepFights.Gunde(talents: [FrenziedReign.TalentId, SuperiorSerration.TalentId]).WithoutDamageSpread();

        f.Cast<BloodArc>();
        f.Cast<ReignInBlood>();
        double rendBefore = RendApplied(f);

        f.Cast<HeartSplitter>();

        AssertRatio(1.85, (RendApplied(f) - rendBefore) / f.Damage<HeartSplitter>().First().Value);
    }

    // ---- Bloodbath

    /// <summary>
    /// With Bloodbath, Grim Carve gives Bloodbath for 3 seconds, which makes Reign in Blood's cooldown recover 200% faster (three times as
    /// fast): after a Reign in Blood and a Grim Carve at the start, Reign in Blood is ready at 90 - 2 x 3 = 84 seconds.
    ///
    /// The ready time is checked within 50ms. TODO: fails: the engine's Reign in Blood is ready at about 90 seconds, as if Bloodbath did
    /// nothing, though the cooldown reports the faster rate while it holds (the time passed does not seem to be credited at it). Check in
    /// game that Bloodbath shortens a cooldown already running.
    /// </summary>
    [Fact]
    public void Bloodbath_Triples_Reign_In_Blood_Cooldown_Recovery_For_3_Seconds()
    {
        var f = StepFights.Gunde(talents: [Bloodbath.TalentId]);

        f.Cast<ReignInBlood>();
        f.Cast<GrimCarve>();
        f.WaitUntil(3.1); // Bloodbath is over

        f.WaitUntil(84.0 - 0.05);
        Assert.False(f.IsReady<ReignInBlood>());
        f.WaitUntil(84.0 + 0.05);
        Assert.True(f.IsReady<ReignInBlood>());
    }

    // ---- Bloodcraze

    /// <summary>
    /// With Bloodcraze, Owed in Blood makes you pulse damage every 3 seconds for 6 seconds, 25% of your main stat increased by 150% per
    /// stack used: with 10 stacks, two pulses, 3 and 6 seconds after, each 25% x (1 + 1.5 x 10) of the main stat.
    ///
    /// The pulse times are checked within 50ms.
    /// </summary>
    [Fact]
    public void Bloodcraze_Pulses_Twice_For_150_Percent_More_Per_Stack()
    {
        var f = StepFights.Gunde(talents: [Bloodcraze.TalentId]).WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10);

        f.Cast<OwedInBlood>();
        f.Wait(7); // both pulses

        var pulses = f.Ticks(Bloodcraze.DamageEffectId);
        Assert.Equal(2, pulses.Count);
        using var check = new Checks(f);
        check.Equal(3.0, pulses[0].Time.TotalSeconds, 0.05);
        check.Equal(6.0, pulses[1].Time.TotalSeconds, 0.05);
        foreach (var pulse in pulses) check.Ratio(0.25 * (1 + 1.5 * 10), pulse.Value / MainStat, what: $"pulse at {pulse.Time.TotalSeconds:F3}s");
    }

    // ---- Oathshatter

    /// <summary>
    /// With Oathshatter, Heart Splitter has 30% more crit chance, and when its direct damage crits, Exsanguinate explodes for 50% of its
    /// damage on every other enemy near the target: on 3 enemies with 25% crit, the crit with the talent makes two explosions of 50%, and
    /// the hit that does not crit without it makes none.
    ///
    /// The crits are decided (with the talent, without it): what is checked is each hit's crit chance, and what a crit does.
    /// </summary>
    [Fact]
    public void Oathshatter_Adds_30_Percent_Crit_And_Explodes_Exsanguinate_On_A_Crit()
    {
        const double baseCrit = 0.25, bonus = 0.3;

        var plainScenario = StepFights.Gunde(enemies: 3, crit: baseCrit).WithoutDamageSpread();

        plainScenario.Cast<HeartSplitter>(crit: false);
        var plainSplit = plainScenario.Damage<HeartSplitter>()[0];

        var oathshatterScenario = StepFights.Gunde(enemies: 3, talents: [Oathshatter.TalentId], crit: baseCrit).WithoutDamageSpread();

        oathshatterScenario.Cast<HeartSplitter>(crit: true);
        var split = oathshatterScenario.Damage<HeartSplitter>()[0];
        var exsanguinates = oathshatterScenario.Damage("Exsanguinate");

        using var check = new Checks();
        check.Equal(baseCrit, plainSplit.CritChance);
        check.Equal(baseCrit + bonus, split.CritChance);
        check.False(plainSplit.IsCrit);
        check.True(split.IsCrit);
        check.Equal(1, plainScenario.Damage("Exsanguinate").Count);
        check.Equal(3, exsanguinates.Count);
        var onTarget = exsanguinates.Single(hit => hit.Target == oathshatterScenario.Enemy.Name);
        var explosions = exsanguinates.Where(hit => hit.Target != oathshatterScenario.Enemy.Name).ToList();
        check.Equal(2, explosions.Count);
        foreach (var explosion in explosions) check.Ratio(0.5, explosion.Value / onTarget.Value, what: $"explosion on {explosion.Target}");
    }

    /// <summary>
    /// Oathshatter's explosion deals less beyond 8 other enemies: sqrt(8 / others) of its damage on each. With Heart Splitter forced to
    /// crit, the explosions on 12 others are sqrt(8 / 12) of those on 8.
    ///
    /// TODO: the tooltip says the reduction starts beyond 10 targets; combat logs (1640 on 15 others of 4492, and 0.2773 on 26 others) fit
    /// 8, which the engine and this test use. Check in game.
    /// </summary>
    [Fact]
    public void Oathshatter_Explosion_Falls_Off_Beyond_8_Other_Enemies()
    {
        double Explosion(int enemies)
        {
            var f = StepFights.Gunde(enemies: enemies, talents: [Oathshatter.TalentId]).WithoutDamageSpread();
            f.Cast<HeartSplitter>(crit: true);
            return f.Damage("Exsanguinate").First(hit => hit.Target != f.Enemy.Name).Value;
        }

        AssertRatio(Math.Sqrt(8.0 / 12.0), Explosion(13) / Explosion(9));
    }

    // ---- Carnage

    /// <summary>
    /// With Carnage, each spin of Grim Carve takes 1 second off the cooldowns of Reaver's Edge, Blood Arc, Heart Splitter and Rupture, and
    /// Grim Carve deals 25% more damage: after Rupture, Heart Splitter and Blood Arc (at 0, 1.5 and 3s), a Grim Carve's three spins take 3
    /// seconds off each, and each spin deals 25% more.
    ///
    /// Reaver's Edge, with 5 seconds, would be ready already when read. Each ready time is checked within 20ms.
    /// </summary>
    [Fact]
    public void Carnage_Takes_1_Second_Off_Four_Cooldowns_Per_Spin_And_Adds_25_Percent_Damage()
    {
        double plainSpinDamage = PlainHit<GrimCarve>();
        var f = StepFights.Gunde(talents: [Carnage.TalentId]).WithoutDamageSpread();

        f.Cast<Rupture>();
        f.Cast<HeartSplitter>();
        f.Cast<BloodArc>();

        f.Cast<GrimCarve>();
        f.Wait(1.5); // its spins

        using var check = new Checks(f);
        foreach (var spin in f.Damage<GrimCarve>()) check.Ratio(1.25, spin.Value / plainSpinDamage, what: $"spin at {spin.Time.TotalSeconds:F3}s");
        f.WaitUntil(3.0 + 9.0 - 3 - 0.02);
        check.False(f.IsReady<BloodArc>());
        f.WaitUntil(3.0 + 9.0 - 3 + 0.02);
        check.True(f.IsReady<BloodArc>());
        f.WaitUntil(1.5 + 12.0 - 3 - 0.02);
        check.False(f.IsReady<HeartSplitter>());
        f.WaitUntil(1.5 + 12.0 - 3 + 0.02);
        check.True(f.IsReady<HeartSplitter>());
        f.WaitUntil(60.0 - 3 - 0.02);
        check.False(f.IsReady<Rupture>());
        f.WaitUntil(60.0 - 3 + 0.02);
        check.True(f.IsReady<Rupture>());
    }

    // ---- Superior Serration

    /// <summary>
    /// With Superior Serration, Blood Arc's Serrated Edge adds 10% more damage to Rend, 30% in all: a Reaver's Edge after a Blood Arc adds
    /// 50% + 30% of its hit to Rend.
    /// </summary>
    [Fact]
    public void Superior_Serration_Makes_Serrated_Edge_Add_30_Percent()
    {
        var f = StepFights.Gunde(talents: [SuperiorSerration.TalentId]).WithoutDamageSpread();

        f.Cast<BloodArc>();
        double rendBefore = RendApplied(f);

        f.Cast<ReaversEdge>();

        AssertRatio(0.8, (RendApplied(f) - rendBefore) / f.Damage<ReaversEdge>().First().Value);
    }

    // ---- Ancestral Instinct

    /// <summary>
    /// With Ancestral Instinct, Reaver's Edge and Grim Carve have a 40% chance to give 6% of your main stat for 6 seconds: after a Reaver's
    /// Edge whose roll succeeds, a Warbound deals 6% more than a plain one, and 7 seconds later it is plain again.
    /// </summary>
    [Fact]
    public void Ancestral_Instinct_Gives_6_Percent_Main_Stat_For_6_Seconds()
    {
        double plainWarboundDamage = PlainHit<Warbound>();
        var f = StepFights.Gunde(talents: [AncestralInstinct.TalentId]).WithoutDamageSpread();
        var procs = f.ForceProcs(key => key == AncestralInstinct.ProcKey ? true : null);

        f.Cast<ReaversEdge>();

        f.Cast<Warbound>();
        f.Wait(7);
        f.Cast<Warbound>();

        var hits = f.Damage<Warbound>();
        using var check = new Checks(f);
        foreach (double chance in procs.ChancesOf(AncestralInstinct.ProcKey)) check.Equal(0.4, chance, what: "Ancestral Instinct's chance");
        check.Ratio(1.06, hits[0].Value / plainWarboundDamage);
        check.Ratio(1.0, hits[1].Value / plainWarboundDamage);
    }

    // ---- Harvester's Toll

    /// <summary>
    /// With Harvester's Toll, Rupture makes you deal 7.5% more damage for 10 seconds: Rupture's own hit deals 7.5% more (the buff comes
    /// first), so does a Reaver's Edge at 5 seconds, and one at 11 seconds is plain.
    ///
    /// TODO: the effect's own text says 900% (value 10); the tooltip's 7.5% is used.
    /// </summary>
    [Fact]
    public void Harvesters_Toll_Adds_7_5_Percent_Damage_For_10_Seconds()
    {
        double plainRuptureDamage = PlainHit<Rupture>(), plainReaverDamage = PlainHit<ReaversEdge>();
        var f = StepFights.Gunde(talents: [HarvestersToll.TalentId]).WithoutDamageSpread();

        f.Cast<Rupture>();
        f.WaitUntil(5);
        f.Cast<ReaversEdge>();
        f.WaitUntil(11);
        f.Cast<ReaversEdge>();

        var reaverHits = f.Damage<ReaversEdge>();
        using var check = new Checks(f);
        check.Ratio(1.075, f.Damage<Rupture>().First().Value / plainRuptureDamage);
        check.Ratio(1.075, reaverHits[0].Value / plainReaverDamage);
        check.Ratio(1.0, reaverHits[1].Value / plainReaverDamage);
    }

    /// <summary>
    /// Rend and Exsanguinate take no damage multipliers of their own: whatever made the hits stronger (Harvester's Toll's 7.5%, Darkening
    /// Hearts' 10%), Exsanguinate is 30% of the Rend on the target, no more. After a Rupture (50% to Rend), a Heart Splitter's (100%)
    /// Exsanguinate is 30% of the two.
    /// </summary>
    [Fact]
    public void Exsanguinate_Takes_No_Damage_Multipliers()
    {
        var f = StepFights.Gunde(talents: [HarvestersToll.TalentId, DarkeningHearts.TalentId]).WithoutDamageSpread();

        f.Cast<Rupture>();

        f.Cast<HeartSplitter>();
        double rend = 0.5 * f.Damage<Rupture>().First().Value + f.Damage<HeartSplitter>().First().Value;

        AssertRatio(0.3, f.Damage("Exsanguinate").First().Value / rend);
    }
}
