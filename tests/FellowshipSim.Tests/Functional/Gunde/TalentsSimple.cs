using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using GundeTalents = FellowshipSim.Core.Heroes.Gunde.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's talents one at a time, as their tooltips describe them.
/// Dev: damage is the expected value, so ratios are exact; random procs are forced, and the chance the engine gives each is read from
/// <c>ForceProcs</c>'s <c>Rolls</c>. A hit is a crit when its crit chance is above 50%. Talents no combat log has shown are marked unverified in the
/// engine; the tests follow the tooltips. Hits are compared with an auto attack swing (30% of Strength).
/// </summary>
public class GundeTalentsSimple
{
    private const double Ratio = 0.001;

    private static readonly double Strength = GundeFights.StrengthUnit();

    private static StepDriver With(int talent, int enemies = 1, double haste = 0, double crit = 0, double spirit = 0) =>
        StepFights.Gunde(haste, enemies, [talent], crit: crit, spirit: spirit);

    private static double Sum(IEnumerable<StepEvent> events) => events.Sum(e => e.Value);

    private static double RendTotal(StepDriver f) => f.RendLeft() + Sum(f.Damage("Rend"));

    /// <summary>Spirit Refunds so far: each one is a gain of Rising Spirit.</summary>
    private static int SpiritRefunds(StepDriver f) => f.Events.Count(e => e.Name == "Rising Spirit");

    private static double ReadyAt(StepDriver f, Ability ability) =>
        f.Now.TotalSeconds + f.Hero.GetCooldownRemains(ability.Name, f.Now);

    /// <summary>The hit of a plain cast of an ability on one enemy: what a talent's bonus is compared with.</summary>
    private static double Plain<T>() where T : Ability
    {
        var f = StepFights.Gunde();
        f.Cast<T>();
        f.Wait(0.3);
        return f.Damage(f.Ability<T>().Name).First().Value;
    }

    // ---- Row 1

    /// <summary>
    /// Player: Death's Arc: each time you use Blood Arc, it has a 30% chance to instantly reset its cooldown and make your next Blood Arc have +100%
    /// Critical Strike chance.
    /// Dev: the roll forced on; a Blood Arc: the roll has a 30% chance, Blood Arc is ready again at once, and the next Blood Arc has 100% more crit chance.
    /// </summary>
    [Fact]
    public void Deaths_Arc_Resets_Blood_Arc_And_Gives_The_Next_One_100_Percent_Crit()
    {
        var f = With(GundeTalents.DeathsArc.TalentId);
        var procs = f.ForceProcs(key => key == GundeTalents.DeathsArc.ProcKey ? true : null);

        f.Cast<BloodArc>();
        Assert.All(procs.Rolls.Where(r => r.Key == GundeTalents.DeathsArc.ProcKey), r => Assert.Equal(0.3, r.Chance, 1e-9));
        Assert.Equal(f.Now.TotalSeconds, ReadyAt(f, f.Ability<BloodArc>()), 0.002);

        f.Cast<BloodArc>();

        var hits = f.Damage("Blood Arc");
        Assert.Equal(1.0, hits[1].CritChance - hits[0].CritChance, 1e-9);
    }

    /// <summary>
    /// Player: Raven's Precision: each time you pick up a Blood Feather, an explosion of blood deals 37.4% of Strength to nearby enemies, reduced beyond 5
    /// targets.
    /// Dev: a Blood Feather forced from the first Rend tick of a Reaver's Edge (3s); one explosion of 37.4% of Strength after its pickup.
    /// </summary>
    [Fact]
    public void Ravens_Precision_Explodes_On_Every_Blood_Feather()
    {
        var f = With(GundeTalents.RavensPrecision.TalentId);
        f.ForceProcs(key => key == "blood_feather" ? true : null);
        f.Cast<ReaversEdge>();

        f.Wait(3.0);

        var hits = f.DamageOfEffect(3220);
        Assert.Single(hits);
        Assert.Equal(0.374, hits[0].Value / Strength, Ratio);
    }

    /// <summary>
    /// Player: Raven's Precision's explosions deal less beyond 5 enemies: multiplied by sqrt(5 / enemies).
    /// Dev: 5 and 10 enemies; a feather forced from a Rend tick; the explosion on each enemy at 10 over one at 5 is sqrt(5 / 10).
    /// </summary>
    [Fact]
    public void Ravens_Precision_Falls_Off_Beyond_5_Enemies()
    {
        double Explosion(int enemies)
        {
            var f = With(GundeTalents.RavensPrecision.TalentId, enemies);
            f.ForceProcs(key => key == "blood_feather" ? true : null);
            f.Cast<ReaversEdge>();
            f.Wait(3.0);
            return f.DamageOfEffect(3220)[0].Value;
        }

        Assert.Equal(Math.Sqrt(5.0 / 10.0), Explosion(10) / Explosion(5), Ratio);
    }

    /// <summary>
    /// Player: Grim Harvest: your Reaver's Edge, Blood Arc, Heart Splitter and Rupture have a 6% chance to reset the cooldown of Grim Carve and make your next
    /// Grim Carve have +100% Critical Strike chance.
    /// Dev: a Grim Carve, then a Heart Splitter with the roll forced on: the roll has a 6% chance, Grim Carve is ready at once, and the next Grim Carve has 100% more
    /// crit chance.
    /// </summary>
    [Fact]
    public void Grim_Harvest_Resets_Grim_Carve_And_Gives_The_Next_One_100_Percent_Crit()
    {
        var f = With(GundeTalents.GrimHarvest.TalentId);
        var procs = f.ForceProcs(key => key == GundeTalents.GrimHarvest.ProcKey ? false : null);
        f.Cast<GrimCarve>();
        f.Wait(1.5);
        procs.Rolls.Clear();
        f.ForceProcs(key => key == GundeTalents.GrimHarvest.ProcKey ? true : null);

        f.Cast<HeartSplitter>();

        Assert.Equal(f.Now.TotalSeconds, ReadyAt(f, f.Ability<GrimCarve>()), 0.002);
        f.Cast<GrimCarve>();
        f.Wait(1.5);
        var spins = f.Damage("Grim Carve");
        Assert.Equal(1.0, spins[3].CritChance - spins[0].CritChance, 1e-9);
    }

    /// <summary>
    /// Player: Grim Harvest has a 6% chance on each cast of Reaver's Edge, Blood Arc, Heart Splitter and Rupture.
    /// Dev: one cast of each; each rolls once with a 6% chance.
    /// </summary>
    [Theory]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Heart Splitter")]
    [InlineData("Rupture")]
    public void Grim_Harvest_Rolls_A_6_Percent_Chance_On_Each_Cast(string ability)
    {
        var f = With(GundeTalents.GrimHarvest.TalentId);
        var procs = f.ForceProcs(_ => null);

        f.Cast(f.Hero.Abilities.First(a => a.Name == ability));

        var rolls = procs.Rolls.Where(r => r.Key == GundeTalents.GrimHarvest.ProcKey).ToList();
        Assert.Single(rolls);
        Assert.Equal(0.06, rolls[0].Chance, 1e-9);
    }

    // ---- Row 2

    /// <summary>
    /// Player: Sundered Flesh: when your Rend contains more damage than an enemy has left in health, they instantly take that damage.
    /// Dev: an enemy with 1.4 times a Reaver's Edge's hit as health: after the hit, its health is below the Rend (50% of the hit), so Rend is dealt at once, in one
    /// tick. Without the talent nothing is dealt.
    /// </summary>
    [Fact]
    public void Sundered_Flesh_Deals_Rend_At_Once_When_It_Exceeds_Health()
    {
        double hit = Plain<ReaversEdge>();
        StepDriver Fight(bool talent)
        {
            var f = talent ? With(GundeTalents.SunderedFlesh.TalentId) : StepFights.Gunde();
            f.Enemy.MaxHealth = 1.4 * hit;
            f.Enemy.CurrentHealth = 1.4 * hit;
            f.Cast<ReaversEdge>();
            return f;
        }

        var with = Fight(true);
        var without = Fight(false);

        Assert.Single(with.Damage("Rend"));
        Assert.Equal(0.5 * hit, with.Damage("Rend")[0].Value, hit * Ratio);
        Assert.Equal(0, with.RendLeft());
        Assert.Empty(without.Damage("Rend"));
    }

    /// <summary>
    /// Player: Crimson Strikes: your Blood Arc grants Crimson Strikes, empowering your next ability that applies Rend with +15% damage, until used.
    /// Dev: a Blood Arc, then a Reaver's Edge 20 seconds later: its hit is 1.15 times a plain one; the next is plain again.
    /// </summary>
    [Fact]
    public void Crimson_Strikes_Empowers_The_Next_Rend_Ability_By_15_Percent_Until_Used()
    {
        double plain = Plain<ReaversEdge>();
        var f = With(GundeTalents.CrimsonStrikes.TalentId);
        f.Cast<BloodArc>();
        f.Wait(20);

        f.Cast<ReaversEdge>();
        f.Wait(5);
        f.Cast<ReaversEdge>();

        var hits = f.Damage("Reaver's Edge");
        Assert.Equal(1.15, hits[0].Value / plain, Ratio);
        Assert.Equal(1.0, hits[1].Value / plain, Ratio);
    }

    /// <summary>
    /// Player: Crimson Strikes empowers every hit of the next ability: all three spins of Grim Carve, and what it adds to Rend.
    /// Dev: a Blood Arc, then a Grim Carve; each spin is 1.15 times a plain spin.
    /// </summary>
    [Fact]
    public void Crimson_Strikes_Empowers_Every_Spin_Of_Grim_Carve()
    {
        double plain = Plain<GrimCarve>();
        var f = With(GundeTalents.CrimsonStrikes.TalentId);
        f.Cast<BloodArc>();

        f.Cast<GrimCarve>();
        f.Wait(1.5);

        Assert.All(f.Damage("Grim Carve"), h => Assert.Equal(1.15, h.Value / plain, Ratio));
    }

    /// <summary>
    /// Player: Crimson Strikes does not interact with Bloodbound Spirit: it neither strengthens it nor is used up.
    /// Dev: a Blood Arc, then Bloodbound Spirit: a plain hit; a Reaver's Edge after it has the 15%.
    /// </summary>
    [Fact]
    public void Crimson_Strikes_Does_Not_Interact_With_Bloodbound_Spirit()
    {
        var plain = StepFights.Gunde();
        plain.GiveGundeSpirit();
        plain.Cast<BloodboundSpirit>();
        plain.Wait(1);
        double plainReaver = Plain<ReaversEdge>();
        var f = With(GundeTalents.CrimsonStrikes.TalentId);
        f.Cast<BloodArc>();
        f.GiveGundeSpirit();

        f.Cast<BloodboundSpirit>();
        f.Wait(1);
        f.Cast<ReaversEdge>();

        Assert.Equal(plain.FirstHitOf("Bloodbound Spirit"), f.FirstHitOf("Bloodbound Spirit"), plain.FirstHitOf("Bloodbound Spirit") * Ratio);
        Assert.Equal(1.15, f.FirstHitOf("Reaver's Edge") / plainReaver, Ratio);
    }

    /// <summary>
    /// Player: Deep Rend: your Rend ticks 10% faster, dealing 10% more damage.
    /// Dev: a Rupture; 11 ticks every 2.733s (82 server frames) instead of 10 every 3s, each a tenth of the application: 110% of it in all.
    /// </summary>
    [Fact]
    public void Deep_Rend_Ticks_10_Percent_Faster_For_10_Percent_More()
    {
        var f = With(GundeTalents.DeepRend.TalentId);
        f.Cast<Rupture>();
        double application = 0.5 * f.FirstHitOf("Rupture");

        f.Wait(32);

        var ticks = f.Damage("Rend");
        Assert.Equal(11, ticks.Count);
        for (int i = 0; i < 11; i++) Assert.Equal(2.733 * (i + 1), ticks[i].Time.TotalSeconds, 0.05);
        Assert.Equal(1.1 * application, Sum(ticks), application * Ratio);
    }

    // ---- Row 3

    /// <summary>
    /// Player: Darkening Hearts: your Heart Splitter deals 10% more direct damage, and has +100% Critical Strike chance against targets with 30% health or less.
    /// Dev: a Heart Splitter at full health: 1.1 times a plain one; at 20% health: crit chance 100% higher; at 40%: no change.
    /// </summary>
    [Fact]
    public void Darkening_Hearts_Adds_10_Percent_Damage_And_100_Percent_Crit_Below_30_Percent_Health()
    {
        double plain = Plain<HeartSplitter>();
        double CritChance(double health)
        {
            var f = With(GundeTalents.DarkeningHearts.TalentId);
            f.Enemy.CurrentHealth = health * f.Enemy.MaxHealth;
            f.Cast<HeartSplitter>();
            return f.Damage("Heart Splitter")[0].CritChance;
        }

        var full = With(GundeTalents.DarkeningHearts.TalentId);
        full.Cast<HeartSplitter>();

        Assert.Equal(1.1, full.FirstHitOf("Heart Splitter") / plain, Ratio);
        Assert.Equal(1.0, CritChance(0.2) - CritChance(0.4), 1e-9);
    }

    /// <summary>
    /// Player: Murder of Crows: your Rupture grants 2 stacks of Murder of Crows for 12 seconds. Your next ability that applies Rend consumes a stack and always
    /// triggers a Spirit Refund.
    /// Dev: no Spirit; Spirit Refund rolls forced off; a Rupture, then three Double Strikes: refunds (counted as Rising Spirit gains, since Spirit Points also
    /// come with time) on the first two only.
    /// </summary>
    [Fact]
    public void Murder_Of_Crows_Makes_The_Next_Two_Rend_Abilities_Spirit_Refund()
    {
        var f = With(GundeTalents.MurderOfCrows.TalentId);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.Cast<Rupture>();
        Assert.Equal(0, SpiritRefunds(f));

        f.Cast<DoubleStrike>();
        f.Cast<DoubleStrike>();
        Assert.Equal(2, SpiritRefunds(f));

        f.Cast<DoubleStrike>();
        Assert.Equal(2, SpiritRefunds(f));
    }

    /// <summary>
    /// Player: Massacre: each Rend consumed by your Slaughter gives +0.25% Spirit for 8 seconds, stacking.
    /// Dev: three enemies with Rend; Slaughter; Spirit at 0.75%, and 0 nine seconds later.
    /// </summary>
    [Fact]
    public void Massacre_Gives_A_Quarter_Percent_Spirit_Per_Rend_Consumed_For_8_Seconds()
    {
        var f = With(GundeTalents.Massacre.TalentId, enemies: 3);
        f.Cast<ReaversEdge>();

        f.Cast<Slaughter>();

        Assert.Equal(0.0075, f.Hero.SpiritPercent(f.Now), 1e-9);
        f.Wait(9);
        Assert.Equal(0.0, f.Hero.SpiritPercent(f.Now), 1e-9);
    }

    // ---- Row 4

    /// <summary>
    /// Player: Slayer's Grin: your Rupture cooldown is reduced by 0.4 seconds each time you collect a Blood Feather.
    /// Dev: a Rupture (no refund), then a Reaver's Edge with a Spirit Refund forced (5 feathers); read as the cast returns (3s: the feathers were picked up at 2s, a Rend tick's feather would come later), Rupture's cooldown is 2 seconds shorter (ready at 58s).
    /// </summary>
    [Fact]
    public void Slayers_Grin_Takes_04_Seconds_Off_Rupture_Per_Feather()
    {
        var f = With(GundeTalents.SlayersGrin.TalentId, spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.Cast<Rupture>();
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);

        f.Cast<ReaversEdge>();

        Assert.Equal(60.0 - 2.0, ReadyAt(f, f.Ability<Rupture>()), 0.02);
    }

    /// <summary>
    /// Player: Frenzied Reign: your Reign in Blood makes the direct damage of your Rend abilities transfer 25% more to Rend.
    /// Dev: a Reign in Blood, then a Double Strike (50% Rend): 50% + 30% + 25%.
    /// </summary>
    [Fact]
    public void Frenzied_Reign_Adds_25_Percent_To_Rend_During_Reign_In_Blood()
    {
        var f = With(GundeTalents.FrenziedReign.TalentId);
        f.Cast<ReignInBlood>();

        f.Cast<DoubleStrike>();

        Assert.Equal(1.05, RendTotal(f) / Sum(f.Damage("Double Strike")), Ratio);
    }

    /// <summary>
    /// Player: Bloodbath: your Grim Carve gives Bloodbath for 3 seconds, which makes Reign in Blood's cooldown recover 200% faster (three times as fast).
    /// Dev: a Reign in Blood and a Grim Carve at the start; 3.1 seconds later Reign in Blood is ready at 90 - 2 x 3 = 84 seconds.
    /// TODO: fails: the engine's Reign in Blood is ready at about 90 seconds, as if Bloodbath did nothing, though the cooldown reports the faster rate while it
    /// holds (the time passed does not seem to be credited at it). Check in game that Bloodbath shortens a cooldown already running.
    /// </summary>
    [Fact]
    public void Bloodbath_Triples_Reign_In_Blood_Cooldown_Recovery_For_3_Seconds()
    {
        var f = With(GundeTalents.Bloodbath.TalentId);
        f.Cast<ReignInBlood>();
        f.Cast<GrimCarve>();

        f.Wait(3.1 - f.Now.TotalSeconds);

        Assert.Equal(84.0, ReadyAt(f, f.Ability<ReignInBlood>()), 0.05);
    }

    // ---- Row 5

    /// <summary>
    /// Player: Bloodcraze: your Owed in Blood makes you pulse damage of 25% of Strength every 3 seconds for 6 seconds, increased by 150% per stack of Owed in
    /// Blood consumed.
    /// Dev: 10 stacks; Owed in Blood; two pulses, 3 and 6 seconds after, each 25% of Strength x (1 + 1.5 x 10).
    /// </summary>
    [Fact]
    public void Bloodcraze_Pulses_Twice_For_150_Percent_More_Per_Stack()
    {
        var f = With(GundeTalents.Bloodcraze.TalentId);
        f.SetOwedInBlood(10);

        f.Cast<OwedInBlood>();
        f.Wait(7);

        var pulses = f.DamageOfEffect(3221);
        Assert.Equal(2, pulses.Count);
        Assert.Equal(3.0, pulses[0].Time.TotalSeconds, 0.05);
        Assert.Equal(6.0, pulses[1].Time.TotalSeconds, 0.05);
        Assert.All(pulses, p => Assert.Equal(0.25 * (1 + 1.5 * 10), p.Value / Strength, Ratio));
    }

    /// <summary>
    /// Player: Oathshatter: your Heart Splitter has +30% Critical Strike chance. When its direct damage crits, Exsanguinate explodes for 50% of its damage on all
    /// the other enemies near the target.
    /// Dev: 25% crit chance and 3 enemies: Heart Splitter's crit chance is 30% higher, and its crit makes two explosions of Exsanguinate at 50%. With 10% crit (40%
    /// in all, no crit) there is no explosion.
    /// </summary>
    [Fact]
    public void Oathshatter_Adds_30_Percent_Crit_And_Explodes_Exsanguinate_On_A_Crit()
    {
        var crit = With(GundeTalents.Oathshatter.TalentId, enemies: 3, crit: 0.25);
        crit.Cast<HeartSplitter>();
        var noCrit = With(GundeTalents.Oathshatter.TalentId, enemies: 3, crit: 0.10);
        noCrit.Cast<HeartSplitter>();

        Assert.Equal(0.55, crit.Damage("Heart Splitter")[0].CritChance, 1e-9);
        var hits = crit.Damage("Exsanguinate");
        Assert.Equal(3, hits.Count);
        var onTarget = hits.Single(h => h.Target == crit.Enemy.Name);
        var explosions = hits.Where(h => h.Target != crit.Enemy.Name).ToList();
        Assert.Equal(2, explosions.Count);
        Assert.All(explosions, e => Assert.Equal(0.5, e.Value / onTarget.Value, Ratio));
        Assert.Single(noCrit.Damage("Exsanguinate"));
    }

    /// <summary>
    /// Player: Oathshatter's explosion is reduced beyond a number of targets: multiplied by sqrt(8 / others) beyond 8 other enemies.
    /// Dev: 9 and 13 enemies (8 and 12 others); the explosions at 12 others over those at 8 are sqrt(8 / 12).
    /// TODO: the tooltip says the reduction starts beyond 10 targets; combat logs (1640 on 15 others of 4492, and 0.2773 on 26 others) fit 8, which the engine
    /// and this test use. Check in game.
    /// </summary>
    [Fact]
    public void Oathshatter_Explosion_Falls_Off_Beyond_8_Other_Enemies()
    {
        double Explosion(int enemies)
        {
            var f = With(GundeTalents.Oathshatter.TalentId, enemies, crit: 0.25);
            f.Cast<HeartSplitter>();
            return f.Damage("Exsanguinate").First(h => h.Target != f.Enemy.Name).Value;
        }

        Assert.Equal(Math.Sqrt(8.0 / 12.0), Explosion(13) / Explosion(9), Ratio);
    }

    /// <summary>
    /// Player: Carnage: each spin of Grim Carve reduces the cooldown of Reaver's Edge, Blood Arc, Heart Splitter and Rupture by 1 second, and Grim Carve deals
    /// 25% more damage.
    /// Dev: Rupture, Heart Splitter and Blood Arc cast first (at 0, 1.5 and 3s), then a Grim Carve: three spins; each cooldown is 3 seconds shorter, and its spins
    /// 1.25 times a plain one. (Reaver's Edge, with 5 seconds, would be ready already when read.)
    /// </summary>
    [Fact]
    public void Carnage_Takes_1_Second_Off_Four_Cooldowns_Per_Spin_And_Adds_25_Percent_Damage()
    {
        double plain = Plain<GrimCarve>();
        var f = With(GundeTalents.Carnage.TalentId);
        f.Cast<Rupture>();
        f.Cast<HeartSplitter>();
        f.Cast<BloodArc>();

        f.Cast<GrimCarve>();
        f.Wait(1.5);

        Assert.Equal(60.0 - 3, ReadyAt(f, f.Ability<Rupture>()), 0.02);
        Assert.Equal(1.5 + 12.0 - 3, ReadyAt(f, f.Ability<HeartSplitter>()), 0.02);
        Assert.Equal(3.0 + 9.0 - 3, ReadyAt(f, f.Ability<BloodArc>()), 0.02);
        Assert.All(f.Damage("Grim Carve"), h => Assert.Equal(1.25, h.Value / plain, Ratio));
    }

    // ---- Row 6

    /// <summary>
    /// Player: Superior Serration: Blood Arc's Serrated Edge adds 10% more damage to Rend (30% in all).
    /// Dev: a Blood Arc, then a Reaver's Edge (50%): 50% + 30%.
    /// </summary>
    [Fact]
    public void Superior_Serration_Makes_Serrated_Edge_Add_30_Percent()
    {
        var f = With(GundeTalents.SuperiorSerration.TalentId);
        f.Cast<BloodArc>();
        double before = RendTotal(f);

        f.Cast<ReaversEdge>();

        Assert.Equal(0.8, (RendTotal(f) - before) / f.FirstHitOf("Reaver's Edge"), Ratio);
    }

    /// <summary>
    /// Player: Ancestral Instinct: your Reaver's Edge and Grim Carve have a 40% chance to give 6% Strength for 6 seconds.
    /// Dev: the roll forced on; a Reaver's Edge: the roll has a 40% chance, and a Warbound after it deals 6% more than one without; 7 seconds later it is back
    /// to plain.
    /// </summary>
    [Fact]
    public void Ancestral_Instinct_Gives_6_Percent_Strength_For_6_Seconds()
    {
        double plain = Plain<Warbound>();
        var f = With(GundeTalents.AncestralInstinct.TalentId);
        var procs = f.ForceProcs(key => key == GundeTalents.AncestralInstinct.ProcKey ? true : null);

        f.Cast<ReaversEdge>();
        f.Cast<Warbound>();
        f.Wait(7);
        f.Cast<Warbound>();

        Assert.All(procs.Rolls.Where(r => r.Key == GundeTalents.AncestralInstinct.ProcKey), r => Assert.Equal(0.4, r.Chance, 1e-9));
        Assert.Equal(1.06, f.Damage("Warbound")[0].Value / plain, Ratio);
        Assert.Equal(1.0, f.Damage("Warbound")[1].Value / plain, Ratio);
    }

    /// <summary>
    /// Player: Harvester's Toll: your Rupture grants Harvester's Toll, increasing all damage you deal by 7.5% for 10 seconds.
    /// Dev: a Rupture: its own hit is 7.5% above a plain one (the buff comes first); a Reaver's Edge 5 seconds later too; one 11 seconds after the Rupture is plain.
    /// TODO: the effect's own text says 900% (value 10); the tooltip's 7.5% is used.
    /// </summary>
    [Fact]
    public void Harvesters_Toll_Adds_7_5_Percent_Damage_For_10_Seconds()
    {
        double plainRupture = Plain<Rupture>(), plainReaver = Plain<ReaversEdge>();
        var f = With(GundeTalents.HarvestersToll.TalentId);

        f.Cast<Rupture>();
        f.Wait(5 - f.Now.TotalSeconds);
        f.Cast<ReaversEdge>();
        f.Wait(11 - f.Now.TotalSeconds);
        f.Cast<ReaversEdge>();

        Assert.Equal(1.075, f.FirstHitOf("Rupture") / plainRupture, Ratio);
        Assert.Equal(1.075, f.Damage("Reaver's Edge")[0].Value / plainReaver, Ratio);
        Assert.Equal(1.0, f.Damage("Reaver's Edge")[1].Value / plainReaver, Ratio);
    }
}
