using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's talents, one at a time, as their tooltips describe them. Efficient Killer and Venomous Delight are in MaraResourceEconomy.cs,
/// Sinner's Pride in MaraCooldowns.cs.
///
/// Damage has no spread, so two hits' ratio is exact. Random procs are forced, the chance of each read from its rolls. A talent's bonus
/// is compared with a plain cast of the same ability, or with the main stat measured from a Skittering Blades hit (80.6% of it). Talents
/// no combat log has shown are marked unverified in the engine; the tests follow the tooltips.
/// </summary>
public class MaraTalents
{
    /// <summary>The main stat, measured as a Skittering Blades hit on a single enemy with nothing else on, which deals 80.6% of it.</summary>
    private static readonly double MainStat = MainStatFromSkitteringBlades();

    private static double MainStatFromSkitteringBlades()
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Cast<SkitteringBlades>();
        return f.Damage<SkitteringBlades>().First().Value / 0.806;
    }

    /// <summary>A Queen's Fang hit at 3 Combo Points, with no talent: what a talent's bonus is compared with.</summary>
    private static double PlainQueensFang()
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>();
        return f.Damage<QueensFang>().First().Value;
    }

    /// <summary>An Arachnid Assault hit at 3 Combo Points, with no talent: what a talent's bonus is compared with.</summary>
    private static double PlainArachnidAssault()
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<ArachnidAssault>();
        return f.Damage<ArachnidAssault>().First().Value;
    }

    // ---- Assassin's Guile

    /// <summary>
    /// With Assassin's Guile, after attacking from Stealth your Queen's Fang and Arachnid Assault deal 40% more damage for 5 seconds: a
    /// Skittering Blades from Stealth, then at once a Queen's Fang at 3 Combo Points deals 1.4 times a plain one, and one 6 seconds later
    /// 1.0 times.
    /// </summary>
    [Fact]
    public void Assassins_Guile_Raises_Finisher_Damage_By_40_Percent_For_5_Seconds()
    {
        double plainDamage = PlainQueensFang();
        var f = StepFights.Mara(talents: [AssassinsGuile.TalentId]).WithoutDamageSpread();

        f.Cast<BroodingShadows>(); // Stealth
        f.Cast<StealthSkitteringBlades>();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>();
        f.Wait(6); // Assassin's Guile is over
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>();

        var hits = f.Damage<QueensFang>();
        using var check = new Checks(f);
        check.Ratio(1.4, hits[0].Value / plainDamage);
        check.Ratio(1.0, hits[1].Value / plainDamage);
    }

    // ---- Feed the Queen

    /// <summary>
    /// With Feed the Queen, each enemy your Skittering Blades hits gives a stack, and your next Queen's Fang deals 12% more damage per stack
    /// (6 stacks at most) and uses them up: with 3 enemies, a Skittering Blades (3 stacks) then a Queen's Fang at 3 Combo Points deals 1.36
    /// times a plain one, and a second one is plain.
    /// </summary>
    [Fact]
    public void Feed_The_Queen_Raises_The_Next_Queens_Fang_By_12_Percent_Per_Stack()
    {
        double plainDamage = PlainQueensFang();
        var f = StepFights.Mara(enemies: 3, talents: [FeedTheQueen.TalentId]).WithoutDamageSpread();

        f.Cast<SkitteringBlades>();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>();

        var hits = f.Damage<QueensFang>();
        using var check = new Checks(f);
        check.Ratio(1.36, hits[0].Value / plainDamage);
        check.Ratio(1.0, hits[1].Value / plainDamage);
    }

    // ---- Malevolence

    /// <summary>
    /// With Malevolence, Queen's Fang gives a stack of Malevolence for Arachnid Assault, which makes your next Arachnid Assault deal 100%
    /// more damage, and Arachnid Assault does the same for Queen's Fang: a Queen's Fang (plain), then an Arachnid Assault at 3 Combo Points
    /// deals 2 times a plain one, then a Queen's Fang at 3 Combo Points 2 times a plain one.
    /// </summary>
    [Fact]
    public void Malevolence_Doubles_The_Other_Finisher_After_Each_Finisher()
    {
        double plainFangDamage = PlainQueensFang();
        double plainAssaultDamage = PlainArachnidAssault();
        var f = StepFights.Mara(talents: [Malevolence.TalentId]).WithoutDamageSpread();

        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<ArachnidAssault>();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>(); // given a stack by that Arachnid Assault

        using var check = new Checks(f);
        check.Ratio(1.0, f.Damage<QueensFang>()[0].Value / plainFangDamage);
        check.Ratio(2.0, f.Damage<ArachnidAssault>()[0].Value / plainAssaultDamage);
        check.Ratio(2.0, f.Damage<QueensFang>()[1].Value / plainFangDamage);
    }

    // ---- Deadly Scheme

    /// <summary>
    /// With Deadly Scheme, every 5 Energy you spend gives a stack, and at 40 stacks (200 Energy) your next Queen's Fang or Arachnid Assault
    /// has +100% crit chance, for 12 seconds: after 10 Backstabs (20 Energy each, 200 in all), a Queen's Fang at 3 Combo Points has a crit
    /// chance of 1, and the next one 0.
    /// </summary>
    [Fact]
    public void Deadly_Scheme_Gives_The_Next_Finisher_100_Percent_Crit_After_200_Energy_Spent()
    {
        var f = StepFights.Mara(talents: [DeadlyScheme.TalentId]);

        for (int backstab = 0; backstab < 10; backstab++) f.Cast<Backstab>();
        f.Set(Mara.ComboPoints, 3);

        f.Cast<QueensFang>();
        f.Set(Mara.ComboPoints, 3);
        f.Cast<QueensFang>();

        var hits = f.Damage<QueensFang>();
        using var check = new Checks(f);
        check.Equal(1.0, hits[0].CritChance, 1e-9);
        check.Equal(0.0, hits[1].CritChance, 1e-9);
    }

    /// <summary>
    /// Deadly Scheme does not come early: with less than 200 Energy spent, the finisher has no extra crit chance. After 9 Backstabs (180
    /// Energy), a Queen's Fang at 3 Combo Points has a crit chance of 0.
    /// </summary>
    [Fact]
    public void Deadly_Scheme_Needs_40_Stacks()
    {
        var f = StepFights.Mara(talents: [DeadlyScheme.TalentId]);

        for (int backstab = 0; backstab < 9; backstab++) f.Cast<Backstab>();
        f.Set(Mara.ComboPoints, 3);

        f.Cast<QueensFang>();

        Assert.Equal(0.0, f.Damage<QueensFang>()[0].CritChance, 1e-9);
    }

    // ---- Arachnid Onslaught

    /// <summary>
    /// With Arachnid Onslaught, your Arachnid Assault deals 20% more damage to enemies that have your Hemorrhaging Strike bleed: with a
    /// bleed on the enemy, an Arachnid Assault at 3 Combo Points deals 1.2 times a plain one.
    /// </summary>
    [Fact]
    public void Arachnid_Onslaught_Raises_Arachnid_Assault_By_20_Percent_Against_A_Bleeding_Enemy()
    {
        double plainDamage = PlainArachnidAssault();
        var f = StepFights.Mara(talents: [ArachnidOnslaught.TalentId]).WithoutDamageSpread();
        f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

        f.Cast<HemorrhagingStrike>();
        f.Set(Mara.ComboPoints, 3);

        f.Cast<ArachnidAssault>();

        AssertRatio(1.2, f.Damage<ArachnidAssault>().First().Value / plainDamage);
    }

    // ---- Maiden's Doom

    /// <summary>
    /// With Maiden's Doom, Maiden of Death's damage bonus is 40%, not 20%, against enemies with 30% health or less: with Maiden of Death
    /// up, a Skittering Blades hit on an enemy at 20% health is 1.4 / 1.2 times its hit on an enemy at full health.
    /// </summary>
    [Fact]
    public void Maidens_Doom_Raises_Maiden_Of_Deaths_Bonus_To_40_Percent_On_Low_Enemies()
    {
        var f = StepFights.Mara(enemies: 2, talents: [MaidensDoom.TalentId]).WithoutDamageSpread();
        var weak = f.Enemies()[1];
        f.SetHealth(0.2, on: weak);

        f.Cast<MaidenOfDeath>();

        f.Cast<SkitteringBlades>();

        var hits = f.Damage<SkitteringBlades>();
        double healthyDamage = hits.First(hit => hit.Target == f.Enemy.Name).Value;
        double lowDamage = hits.First(hit => hit.Target == weak.Name).Value;
        AssertRatio(1.4 / 1.2, lowDamage / healthyDamage);
    }

    // ---- Puncture

    /// <summary>With Puncture, your Widow's Bite has +100% crit chance: with no crit chance of her own, its hits have a crit chance of 1.</summary>
    [Fact]
    public void Puncture_Gives_Widows_Bite_100_Percent_Crit_Chance()
    {
        var f = StepFights.Mara(talents: [Puncture.TalentId]);

        f.Cast<WidowsBite>();

        Assert.All(f.Damage<WidowsBite>(), hit => Assert.Equal(1.0, hit.CritChance, 1e-9));
    }

    // ---- Red Ledger

    /// <summary>
    /// With Red Ledger, while your Hemorrhaging Strike bleed is on an enemy you have +5% crit chance, and +1% more for each further enemy it
    /// is on, up to +10% in all: with bleeds on 1, 3 and 7 enemies, a Skittering Blades hit has 5%, 7% and 10% crit chance.
    /// </summary>
    [Theory]
    [InlineData(1, 0.05)]
    [InlineData(3, 0.07)]
    [InlineData(7, 0.10)]
    public void Red_Ledger_Gives_5_Percent_Crit_Plus_1_Per_Further_Bleeding_Enemy_Up_To_10(int bleeding, double crit)
    {
        var f = StepFights.Mara(enemies: bleeding, talents: [RedLedger.TalentId]);

        foreach (var enemy in f.Enemies())
        {
            f.Set(Mara.Energy, 200); // enough for every strike
            f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike
            f.Cast<HemorrhagingStrike>(enemy);
            f.Wait(1.1); // Hemorrhaging Strike's 2 second cooldown, with the global cooldown
        }

        f.Cast<SkitteringBlades>();

        Assert.Equal(crit, f.Damage<SkitteringBlades>().Last().CritChance, 1e-9);
    }

    // ---- Bloodrush

    /// <summary>With Bloodrush, your Hemorrhaging Strike bleed ticks 20% faster: at no haste, its ticks come every 3 / 1.2 = 2.5 seconds.</summary>
    [Fact]
    public void Bloodrush_Makes_The_Bleed_Tick_20_Percent_Faster()
    {
        var f = StepFights.Mara(talents: [Bloodrush.TalentId]);
        f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

        f.Cast<HemorrhagingStrike>();

        f.Wait(8);

        var ticks = f.Damage("Hemorrhage");
        using var check = new Checks(f);
        check.True(ticks.Count >= 3);
        check.Seconds(2.5, (ticks[1].Time - ticks[0].Time).TotalSeconds);
    }

    // ---- Gushing Blood

    /// <summary>
    /// With Gushing Blood, while Maiden of Death is active your Hemorrhaging Strike puts its bleed on up to 4 more enemies: with 7 enemies,
    /// a Hemorrhaging Strike during Maiden of Death makes 5 enemies bleed (the target and 4 more).
    ///
    /// The bleed's 50% more damage against Seething Poison, the tooltip's other clause, is tested with the talent's interactions.
    /// </summary>
    [Fact]
    public void Gushing_Blood_Spreads_The_Bleed_To_4_More_Enemies_During_Maiden_Of_Death()
    {
        var f = StepFights.Mara(enemies: 7, talents: [GushingBlood.TalentId]);

        f.Cast<MaidenOfDeath>();
        f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

        f.Cast<HemorrhagingStrike>();

        Assert.Equal(5, f.MaraOf().Hemorrhage.ActiveCount(f.Context));
    }

    /// <summary>
    /// With Gushing Blood, the bleed deals 50% more damage to an enemy with your Seething Poison, and Seething Poison itself adds 20% to
    /// the bleed: 1.8 times in all. The first tick of a bleed with Seething Poison on the enemy is 1.2 x 1.5 = 1.8 times the first tick of
    /// a plain bleed.
    ///
    /// TODO: whether the two bonuses multiply (1.8) or add (1.7) is not in the tooltips; the engine multiplies. Check in game.
    /// </summary>
    [Fact(Skip = "Gushing Blood with Seething Poison: 1.8 (multiplied) or 1.7 (added): to check in game")]
    public void Gushing_Blood_Raises_The_Bleed_By_50_Percent_On_Top_Of_Seething_Poison()
    {
        var plainScenario = StepFights.Mara().WithoutDamageSpread();
        plainScenario.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

        plainScenario.Cast<HemorrhagingStrike>();
        plainScenario.Wait(4); // the first tick
        double plainBleedDamage = plainScenario.Damage("Hemorrhage").First().Value;

        var gushingScenario = StepFights.Mara(talents: [GushingBlood.TalentId]).WithoutDamageSpread();

        gushingScenario.Cast<BroodingShadows>(); // into Stealth
        gushingScenario.Cast<WidowsBite>(); // Seething Poison
        gushingScenario.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike
        gushingScenario.Cast<HemorrhagingStrike>();
        gushingScenario.Wait(4); // the first tick
        double gushingBleedDamage = gushingScenario.Damage("Hemorrhage").First().Value;

        AssertRatio(1.8, gushingBleedDamage / plainBleedDamage);
    }

    // ---- Caustic Wounds

    /// <summary>
    /// With Caustic Wounds, your Backstab has a 30% chance to apply Caustic Poison outside Stealth if the enemy has your bleed: with no
    /// bleed there is no roll; after a bleed, a Backstab rolls once at 30%, and forced on, the roll gives a Caustic Poison hit.
    /// </summary>
    [Fact]
    public void Caustic_Wounds_Has_A_30_Percent_Chance_On_A_Bleeding_Enemy()
    {
        var f = StepFights.Mara(talents: [CausticWounds.TalentId]);
        var procs = f.ForceProcs(key => key == CausticWounds.ProcKey ? true : null);

        f.Cast<Backstab>();

        Assert.Empty(procs.ChancesOf(CausticWounds.ProcKey));

        f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike
        f.Cast<HemorrhagingStrike>();

        f.Cast<Backstab>();

        using var check = new Checks(f);
        check.Equal(0.3, procs.ChancesOf(CausticWounds.ProcKey).Single());
        check.Equal(1, f.Damage("Caustic Poison").Count);
    }

    // ---- Seething Burst

    /// <summary>
    /// With Seething Burst, each time Seething Poison deals damage it has a 20% chance to erupt for 150% of your main stat on nearby
    /// enemies: after a Widow's Bite from Stealth, each tick rolls at 20%, and forced on, the eruption's hit is 1.5 / 1.2276 times a
    /// Seething Poison tick.
    /// </summary>
    [Fact]
    public void Seething_Burst_Has_A_20_Percent_Chance_To_Erupt_For_150_Percent()
    {
        var f = StepFights.Mara(talents: [SeethingBurst.TalentId]).WithoutDamageSpread();
        var procs = f.ForceProcs(key => key == SeethingBurst.ProcKey ? true : null);

        f.Cast<BroodingShadows>(); // Stealth
        f.Cast<WidowsBite>();

        f.Wait(2.5); // the first ticks

        double tickDamage = f.Damage("Seething Poison").First().Value;
        double burstDamage = f.Damage("Seething Burst").First().Value;
        using var check = new Checks(f);
        foreach (double chance in procs.ChancesOf(SeethingBurst.ProcKey))
            check.Equal(0.2, chance, what: "a Seething Burst roll's chance");
        check.Ratio(1.5 / 1.2276, burstDamage / tickDamage);
    }

    // ---- Corrosive Spill

    /// <summary>
    /// With Corrosive Spill, each Combo Point you spend has a 3% chance to coat the ground with a spill for 3 seconds, which deals 158% of
    /// your main stat every 1.5 seconds: a finisher with 4 Combo Points rolls 4 times at 3%, and forced on, each spill tick deals 158% of
    /// the main stat.
    /// </summary>
    [Fact]
    public void Corrosive_Spill_Has_A_3_Percent_Chance_Per_Combo_Point_Spent()
    {
        var f = StepFights.Mara(talents: [CorrosiveSpill.TalentId]).WithoutDamageSpread();
        var procs = f.ForceProcs(key => key == CorrosiveSpill.ProcKey ? true : null);
        f.Set(Mara.ComboPoints, 4);

        f.Cast<QueensFang>();

        f.Wait(5); // the spills tick

        var chances = procs.ChancesOf(CorrosiveSpill.ProcKey);
        var ticks = f.Ticks(CorrosiveSpill.DamageEffectId);
        using var check = new Checks(f);
        check.Equal(4, chances.Count, what: "Corrosive Spill rolls");
        foreach (double chance in chances)
            check.Equal(0.03, chance, what: "a Corrosive Spill roll's chance");
        check.True(ticks.Count > 0, what: "spill ticks");
        foreach (var tick in ticks)
            check.Equal(1.58, tick.Value / MainStat, 1.58 * StepAsserts.RatioTolerance, what: "a spill tick over the main stat");
    }

    // ---- Hemotoxin

    /// <summary>
    /// With Hemotoxin, your Backstab, Widow's Bite and auto attacks have a 12% chance to apply a stack of Hemotoxin, which deals 20% of
    /// your main stat every 1.5 seconds over 9 seconds: a Backstab rolls at 12%, and forced on, the stack's tick deals 20% of the main
    /// stat, 1.5 seconds apart.
    /// </summary>
    [Fact]
    public void Hemotoxin_Has_A_12_Percent_Chance_To_Apply_A_Stack()
    {
        var f = StepFights.Mara(talents: [Hemotoxin.TalentId]).WithoutDamageSpread();
        var procs = f.ForceProcs(key => key == Hemotoxin.ProcKey ? true : null);

        f.Cast<Backstab>();
        f.Wait(4); // the first ticks

        var ticks = f.Ticks(Hemotoxin.PoisonEffectId);
        using var check = new Checks(f);
        check.Equal(0.12, procs.ChancesOf(Hemotoxin.ProcKey).First());
        check.True(ticks.Count >= 2);
        check.Equal(0.2, ticks[0].Value / MainStat, 0.2 * StepAsserts.RatioTolerance);
        check.Seconds(1.5, (ticks[1].Time - ticks[0].Time).TotalSeconds);
    }
}
