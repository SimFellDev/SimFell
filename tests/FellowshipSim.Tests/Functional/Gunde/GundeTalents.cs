using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using FellowshipSim.Core.Heroes.Gunde.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's talents, one at a time, as their tooltips describe them.
///
/// Damage has no spread unless the test says otherwise, and random procs are forced, the chance of each read from its rolls. A
/// talent's bonus is compared with a plain cast of the same ability. No test checks a damage value. Rend is read from its ticks once they
/// are all out (GundeRend.cs). Talents no combat log has shown are marked unverified in the engine; the tests follow the tooltips.
/// </summary>
public class GundeTalents
{
    /// <summary>The first hit of a plain cast of <typeparamref name="T"/> on one enemy: what a talent's bonus is compared with.</summary>
    private static double PlainHit<T>() where T : Ability
    {
        var f = StepFights.Gunde().WithoutDamageSpread();
        f.Cast<T>();
        f.Wait(0.3); // its first hit lands
        return f.Damage<T>().First().Value;
    }

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
    /// With Raven's Precision, each Blood Feather you pick up makes an explosion of blood that hits nearby enemies.
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
        Assert.True(explosions[0].Value > 0);
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
    /// With Sundered Flesh, after a tick of Rend, an enemy with less health left than the damage the Rend still holds takes its health
    /// plus one at once, logged as Rend (Blueprint GA_Gunde_Passive_Talent_InstantDeathFromDot). The check runs after each tick and at
    /// no other time: an enemy with 1.4 times a Reaver's Edge's hit as health, which the hit leaves with less health than its Rend
    /// (half the hit), takes nothing at once from the hit. On Rend's first tick it takes the tick, then its health plus one, and no more
    /// after it. Without the talent it takes the tick alone.
    /// </summary>
    [Fact]
    public void Sundered_Flesh_Deals_The_Enemys_Health_After_A_Rend_Tick_When_Rend_Exceeds_It()
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
        check.Equal(0, sunderedScenario.Damage("Rend").Count, what: "Rend damage at once from the hit");
        double healthAfterHit = sunderedScenario.Enemy.CurrentHealth;

        sunderedScenario.Wait(Rend.TickSeconds);
        plainScenario.Wait(Rend.TickSeconds);
        var rend = sunderedScenario.Damage("Rend");
        check.Equal(2, rend.Count, what: "Rend hits after the first tick: the tick, then Sundered Flesh's");
        if (rend.Count == 2) check.Ratio(1.0, rend[1].Value / (healthAfterHit - rend[0].Value + 1.0));
        check.Equal(1, plainScenario.Damage("Rend").Count, what: "Rend hits after the first tick without the talent");

        // The bleed stays on the enemy and ticks on, but an enemy with no health left takes no more from Sundered Flesh.
        sunderedScenario.Wait(40);
        plainScenario.Wait(40);
        check.Equal(plainScenario.Damage("Rend").Count + 1, sunderedScenario.Damage("Rend").Count, what: "Rend hits in the whole fight");
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
        double rend = GundeRend.Added(f => f.Cast<BloodArc>(), f => f.Cast<ReaversEdge>(), out var f, [CrimsonStrikes.TalentId]);
        double reaverDamage = f.Damage<ReaversEdge>().First().Value;

        using var check = new Checks(f);
        check.Ratio(1.15, reaverDamage / plainReaverDamage);
        check.Ratio(0.7, rend / reaverDamage);
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
    /// Deep Rend does not change the Rend stacks Owed in Blood adds: 10 stacks of Owed in Blood deal the same Rend with the talent as
    /// without it, in 11 ticks.
    /// </summary>
    [Fact(Skip = "Per the user: the tooltip may be wrong and Deep Rend's 1.1 on Owed in Blood's Rend may be real")]
    public void Deep_Rend_Does_Not_Change_The_Rend_Stacks_Owed_In_Blood_Adds()
    {
        void TenStacks(StepDriver fight)
        {
            fight.Apply(OwedInBlood.Buff, stacks: 10);
            fight.Cast<OwedInBlood>();
        }
        double plainRend = GundeRend.Of(TenStacks);
        var f = StepFights.Gunde(talents: [DeepRend.TalentId]).WithoutDamageSpread();
        TenStacks(f);
        double deepRend = GundeRend.Dealt(f);

        using var check = new Checks(f);
        check.Equal(11, f.Damage("Rend").Count);
        check.Ratio(1.0, deepRend / plainRend);
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
    /// gives 0.75%, gone 8 seconds after the cast.
    /// </summary>
    [Fact]
    public void Massacre_Gives_A_Quarter_Percent_Spirit_Per_Rend_Consumed_For_8_Seconds()
    {
        var f = StepFights.Gunde(enemies: 3, talents: [Massacre.TalentId]);

        f.Cast<ReaversEdge>();

        f.Cast<Slaughter>();
        double end = f.CompletedAt("Slaughter") + 8;

        using var check = new Checks(f);
        check.Equal(0.0075, f.Spirit(), 1e-9);
        f.WaitUntil(end - 0.1);
        check.Equal(0.0075, f.Spirit(), 1e-9);
        f.WaitUntil(end + 0.1);
        check.Equal(0.0, f.Spirit(), 1e-9);
    }

    // ---- Slayer's Grin

    /// <summary>
    /// With Slayer's Grin, each Blood Feather you pick up takes 0.4 seconds off Rupture's cooldown: after a Rupture, a Reaver's Edge with
    /// a Spirit Refund (5 feathers) makes Rupture ready 2 seconds sooner, 58 seconds after the Rupture.
    ///
    /// The feathers are picked up at the refund; only the first 5 feather rolls (the refund's) are forced to succeed, so no Rend tick adds one.
    /// </summary>
    [Fact]
    public void Slayers_Grin_Takes_0_4_Seconds_Off_Rupture_Per_Feather()
    {
        var f = StepFights.Gunde(spirit: 0.2, talents: [SlayersGrin.TalentId]);
        int feathers = 0;
        f.ForceProcs(key => key == Core.Heroes.Gunde.Gunde.FeatherProcKey ? feathers++ < 5 : null); // the refund's 5, none from a Rend tick
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // no refund from Rupture

        f.Cast<Rupture>();
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null); // a refund from Reaver's Edge

        f.Cast<ReaversEdge>();

        double end = f.CompletedAt("Rupture") + 60 - 2;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<Rupture>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<Rupture>());
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
        double rend = GundeRend.Dealt(f);

        AssertRatio(1.05, rend / f.Damage<DoubleStrike>().Sum(hit => hit.Value));
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
        double rend = GundeRend.Added(f =>
        {
            f.Cast<BloodArc>();
            f.Cast<ReignInBlood>();
        }, f => f.Cast<HeartSplitter>(), out var f, [FrenziedReign.TalentId, SuperiorSerration.TalentId]);

        AssertRatio(1.85, rend / f.Damage<HeartSplitter>().First().Value);
    }

    // ---- Bloodbath

    /// <summary>
    /// With Bloodbath, Grim Carve gives Bloodbath for 3 seconds, which makes Reign in Blood's cooldown recover 200% faster (three times as
    /// fast): after a Reign in Blood and a Grim Carve at the start, Reign in Blood is ready at 90 - 2 x 3 = 84 seconds.
    /// </summary>
    [Fact]
    public void Bloodbath_Triples_Reign_In_Blood_Cooldown_Recovery_For_3_Seconds()
    {
        var f = StepFights.Gunde(talents: [Bloodbath.TalentId]);

        f.Cast<ReignInBlood>();
        double end = f.CompletedAt("Reign in Blood") + 90 - 2 * 3;
        f.Cast<GrimCarve>();

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<ReignInBlood>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<ReignInBlood>());
    }

    // ---- Bloodcraze

    /// <summary>
    /// With Bloodcraze, Owed in Blood makes you pulse damage every 3 seconds for 6 seconds, increased by 150% per stack used: with N
    /// stacks, two pulses, 3 and 6 seconds after, each (1 + 1.5 x N) / (1 + 1.5 x 2) times a pulse of 2 stacks.
    ///
    /// The pulse times are checked within 50ms.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public void Bloodcraze_Pulses_Twice_For_150_Percent_More_Per_Stack(int stacks)
    {
        StepDriver Fight(int stacks)
        {
            var fight = StepFights.Gunde(talents: [Bloodcraze.TalentId]).WithoutDamageSpread();
            fight.Apply(OwedInBlood.Buff, stacks: stacks);
            fight.Cast<OwedInBlood>();
            fight.Wait(7); // both pulses
            return fight;
        }
        var f = Fight(stacks);
        var twoStacksPulses = Fight(2).Ticks(Bloodcraze.DamageEffectId);

        var pulses = f.Ticks(Bloodcraze.DamageEffectId);
        Assert.Equal(2, pulses.Count);
        using var check = new Checks(f);
        check.Equal(3.0, pulses[0].Time.TotalSeconds, 0.05);
        check.Equal(6.0, pulses[1].Time.TotalSeconds, 0.05);
        for (int i = 0; i < pulses.Count; i++)
            check.Ratio((1 + 1.5 * stacks) / (1 + 1.5 * 2), pulses[i].Value / twoStacksPulses[i].Value, what: $"pulse {i + 1}");
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
    /// Oathshatter's explosion deals less beyond 10 other enemies: sqrt(10 / others) of its damage on each. With Heart Splitter forced to
    /// crit, the explosions on 12 others are sqrt(10 / 12) of those on 8.
    /// </summary>
    [Fact]
    public void Oathshatter_Explosion_Falls_Off_Beyond_10_Other_Enemies()
    {
        double Explosion(int enemies)
        {
            var f = StepFights.Gunde(enemies: enemies, talents: [Oathshatter.TalentId]).WithoutDamageSpread();
            f.Cast<HeartSplitter>(crit: true);
            return f.Damage("Exsanguinate").First(hit => hit.Target != f.Enemy.Name).Value;
        }

        AssertRatio(Math.Sqrt((double)Oathshatter.Constants.TargetThresholdScaling / 12.0), Explosion(13) / Explosion(9));
    }

    // ---- Carnage

    /// <summary>
    /// With Carnage, each spin of Grim Carve takes 1 second off the cooldowns of Reaver's Edge, Blood Arc, Heart Splitter and Rupture, and
    /// Grim Carve deals 25% more damage: after Rupture, Heart Splitter and Blood Arc (at 0, 1.5 and 3s), a Grim Carve's three spins take 3
    /// seconds off each, and each spin deals 25% more.
    ///
    /// Reaver's Edge, with 5 seconds, would be ready already when read.
    /// </summary>
    [Fact]
    public void Carnage_Takes_1_Second_Off_Four_Cooldowns_Per_Spin_And_Adds_25_Percent_Damage()
    {
        double plainSpinDamage = PlainHit<GrimCarve>();
        var f = StepFights.Gunde(talents: [Carnage.TalentId]).WithoutDamageSpread();

        f.Cast<Rupture>();
        double ruptureEnd = f.CompletedAt("Rupture") + 60 - 3;
        f.Cast<HeartSplitter>();
        double heartSplitterEnd = f.CompletedAt("Heart Splitter") + 12 - 3;
        f.Cast<BloodArc>();
        double bloodArcEnd = f.CompletedAt("Blood Arc") + 9 - 3;

        f.Cast<GrimCarve>();
        f.Wait(1.5); // its spins

        using var check = new Checks(f);
        foreach (var spin in f.Damage<GrimCarve>()) check.Ratio(1.25, spin.Value / plainSpinDamage, what: $"spin at {spin.Time.TotalSeconds:F3}s");
        f.WaitUntil(bloodArcEnd - 0.1);
        check.False(f.IsReady<BloodArc>());
        f.WaitUntil(bloodArcEnd + 0.1);
        check.True(f.IsReady<BloodArc>());
        f.WaitUntil(heartSplitterEnd - 0.1);
        check.False(f.IsReady<HeartSplitter>());
        f.WaitUntil(heartSplitterEnd + 0.1);
        check.True(f.IsReady<HeartSplitter>());
        f.WaitUntil(ruptureEnd - 0.1);
        check.False(f.IsReady<Rupture>());
        f.WaitUntil(ruptureEnd + 0.1);
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
        double rend = GundeRend.Added(f => f.Cast<BloodArc>(), f => f.Cast<ReaversEdge>(), out var f, [SuperiorSerration.TalentId]);

        AssertRatio(0.8, rend / f.Damage<ReaversEdge>().First().Value);
    }

    // ---- Ancestral Instinct

    /// <summary>
    /// With Ancestral Instinct, Reaver's Edge and Grim Carve have a 40% chance to give 6% of your main stat for 6 seconds: after a Reaver's
    /// Edge whose roll succeeds, a Warbound 0.1 seconds before the end deals 6% more than a plain one, and one 0.1 seconds after it is
    /// plain.
    /// </summary>
    [Theory]
    [InlineData(-0.1, 1.06)]
    [InlineData(0.1, 1.0)]
    public void Ancestral_Instinct_Gives_6_Percent_Main_Stat_For_6_Seconds(double offset, double multiplier)
    {
        double plainWarboundDamage = PlainHit<Warbound>();
        var f = StepFights.Gunde(talents: [AncestralInstinct.TalentId]).WithoutDamageSpread();
        var procs = f.ForceProcs(key => key == AncestralInstinct.ProcKey ? true : null);

        f.Cast<ReaversEdge>();
        f.WaitUntil(f.CompletedAt("Reaver's Edge") + 6 + offset);
        f.Cast<Warbound>();

        using var check = new Checks(f);
        foreach (double chance in procs.ChancesOf(AncestralInstinct.ProcKey)) check.Equal(0.4, chance, what: "Ancestral Instinct's chance");
        check.Ratio(multiplier, f.Damage<Warbound>().First().Value / plainWarboundDamage);
    }

    // ---- Harvester's Toll

    /// <summary>
    /// With Harvester's Toll, Rupture makes you deal 7.5% more damage for 10 seconds from the cast: Rupture's own hit deals 7.5% more
    /// (the buff comes first), so does a Warbound 0.1 seconds before the end, and a Warbound 0.1 seconds after it is plain.
    /// </summary>
    [Theory]
    [InlineData(-0.1, 1.075)]
    [InlineData(0.1, 1.0)]
    public void Harvesters_Toll_Adds_7_5_Percent_Damage_For_10_Seconds(double offset, double multiplier)
    {
        double plainRuptureDamage = PlainHit<Rupture>(), plainWarboundDamage = PlainHit<Warbound>();
        var f = StepFights.Gunde(talents: [HarvestersToll.TalentId]).WithoutDamageSpread();

        f.Cast<Rupture>();
        f.WaitUntil(f.CompletedAt("Rupture") + 10 + offset);
        f.Cast<Warbound>();

        using var check = new Checks(f);
        check.Ratio(1.075, f.Damage<Rupture>().First().Value / plainRuptureDamage);
        check.Ratio(multiplier, f.Damage<Warbound>().First().Value / plainWarboundDamage);
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
