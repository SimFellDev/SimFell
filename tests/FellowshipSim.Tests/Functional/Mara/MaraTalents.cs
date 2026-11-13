using FellowshipSim.Core.Customization.Legendaries;
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
            f.Wait(1.1); // the global cooldown
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

    /// <summary>
    /// With Bloodrush and From the Shadows, the two speed-ups multiply the bleed's period: every 3 x 0.87 / 1.2 seconds shortened by
    /// haste, floor(15 / tick) full ticks over the 15 seconds of a 1 Combo Point bleed, plus a partial one for the time left (at least 0.1
    /// seconds).
    ///
    /// From the game data and a combat log, as PR #61 reads them: each scales the bleed's tick period, From the Shadows by 0.87 and
    /// Bloodrush by 1 / 1.2, and fjrCax7VzGmMZw6h fight 26 has its ticks 0.722 to 0.739 of 3 seconds / haste apart (0.87 / 1.2 = 0.725).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    public void Bloodrush_And_From_The_Shadows_Multiply_The_Bleeds_Tick_Period(double haste)
    {
        double tick = 3 * 0.87 / 1.2 / (1 + haste);
        var f = StepFights.Mara(haste: haste, talents: [Bloodrush.TalentId], legendaries: [FromTheShadows.CapeItemId]);
        f.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // no clone
        f.Set(Mara.ComboPoints, 1); // a 15 second bleed

        f.Cast<HemorrhagingStrike>();
        f.Wait(20); // the bleed is over

        var ticks = f.Damage("Hemorrhage");
        using var check = new Checks(f);
        check.Equal(FullTicks(15, tick) + PartialTick(15, tick, 0.1), ticks.Count);
        check.Seconds(tick, ticks[1].Time.TotalSeconds - ticks[0].Time.TotalSeconds);
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
    /// With Gushing Blood, the bleeds a Hemorrhaging Strike puts on the other enemies during Maiden of Death last as long as the struck
    /// enemy's: a strike at 1 Combo Point on a bleeding enemy gives it 15 seconds plus the time it had left, up to 30% of 15, and the
    /// copies run out with it.
    ///
    /// From the game data and a combat log, as PR #61 reads them: the talent copies the struck enemy's bleed as the strike left it, and
    /// fjrCax7VzGmMZw6h fight 26 @ 24.677 has a 39 second bleed on the struck enemy and on 4 others.
    /// </summary>
    [Fact]
    public void Gushing_Blood_Copies_Last_As_Long_As_The_Struck_Enemys_Bleed()
    {
        var f = StepFights.Mara(enemies: 3, talents: [GushingBlood.TalentId]);
        var others = f.Enemies().Where(enemy => enemy != f.Enemy).ToList();

        f.Set(Mara.ComboPoints, 1); // a 15 second bleed
        f.Cast<HemorrhagingStrike>(); // before Maiden of Death: on the target alone
        double firstEnd = f.CompletedAt("Hemorrhaging Strike") + 15;
        f.Cast<MaidenOfDeath>();
        f.Set(Mara.ComboPoints, 1);
        f.Cast<HemorrhagingStrike>();
        double struck = f.CompletedAt("Hemorrhaging Strike");
        double end = struck + 15 + Math.Min(firstEnd - struck, 0.3 * 15);

        f.WaitUntil(end - 0.1);

        using var check = new Checks(f);
        check.True(f.HasDebuff("Hemorrhage", on: f.Enemy));
        foreach (var enemy in others) check.True(f.HasDebuff("Hemorrhage", on: enemy), what: "a copy before the struck enemy's bleed ends");

        f.WaitUntil(end + 0.1);

        check.False(f.HasDebuff("Hemorrhage", on: f.Enemy));
        foreach (var enemy in others) check.False(f.HasDebuff("Hemorrhage", on: enemy), what: "a copy after the struck enemy's bleed ends");
    }

    /// <summary>
    /// With Gushing Blood, a Hemorrhaging Strike during Maiden of Death still carries over up to 30% of the time left, on the struck enemy
    /// and on another enemy that was bleeding: two enemies with a 30 second bleed, then Maiden of Death and a strike at 6 Combo Points on
    /// the first, and both bleeds end 30 + 9 = 39 seconds after it.
    ///
    /// From a combat log: in fjrCax7VzGmMZw6h fight 26 (Mara player 23, 6 Combo Point strikes), all 218 struck enemies that were bleeding
    /// and 164 of the 167 other enemies that were bleeding get a 39 second bleed during Maiden of Death (@ 28.816: the struck enemy and 3
    /// others with 34.9 seconds left). No other enemy in that log had under 9 seconds left, so whether such an enemy gets the struck enemy's
    /// duration or its own carry-over is not shown.
    /// </summary>
    [Fact]
    public void Gushing_Blood_Keeps_The_30_Percent_Carry_Over_On_Every_Bleeding_Enemy()
    {
        var f = StepFights.Mara(enemies: 2, talents: [GushingBlood.TalentId]);
        var struck = f.Enemies()[0];
        var other = f.Enemies()[1];

        f.Set(Mara.ComboPoints, 6); // a 30 second bleed
        f.Cast<HemorrhagingStrike>(struck);
        f.Set(Mara.ComboPoints, 6);
        f.Cast<HemorrhagingStrike>(other);
        f.Cast<MaidenOfDeath>();
        f.Set(Mara.ComboPoints, 6);
        f.Cast<HemorrhagingStrike>(struck);
        double end = f.CompletedAt("Hemorrhaging Strike") + 39;

        f.WaitUntil(end - 0.1);

        using var check = new Checks(f);
        check.True(f.HasDebuff("Hemorrhage", on: struck));
        check.True(f.HasDebuff("Hemorrhage", on: other));

        f.WaitUntil(end + 0.1);

        check.False(f.HasDebuff("Hemorrhage", on: struck));
        check.False(f.HasDebuff("Hemorrhage", on: other));
    }

    /// <summary>
    /// With Gushing Blood, From the Shadows and Bloodrush, a Hemorrhaging Strike during Maiden of Death gives the struck enemy and the
    /// other enemy a bleed whose period has From the Shadows' 0.87 twice: 3 x 0.87 x 0.87 / 1.2 seconds shortened by haste, first tick
    /// included, against 3 x 0.87 / 1.2 outside Maiden of Death.
    ///
    /// From a combat log: in fjrCax7VzGmMZw6h fight 26 (Mara player 23, the one Mara player with all three), the 2,227 pairs of a copy's
    /// ticks with nothing between them are 0.635 of 3 seconds / haste apart at the median (0.87 x 0.87 / 1.2 = 0.631), and 0.722 to 0.739
    /// after a refresh outside Maiden of Death. In the game, Gushing Blood copies the struck enemy's bleed after From the Shadows has
    /// shortened its period, and From the Shadows shortens the copy's again.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    public void Gushing_Blood_Copies_Take_From_The_Shadows_Twice(double haste)
    {
        double tick = 3 * 0.87 * 0.87 / 1.2 / (1 + haste);
        var f = StepFights.Mara(haste: haste, enemies: 2,
            talents: [GushingBlood.TalentId, Bloodrush.TalentId], legendaries: [FromTheShadows.CapeItemId]);
        f.ForceProcs(key => key == FromTheShadows.ProcKey ? false : null); // no clone
        var struck = f.Enemies()[0];
        var other = f.Enemies()[1];

        f.Cast<MaidenOfDeath>();
        f.Set(Mara.ComboPoints, 1); // a 15 second bleed
        f.Cast<HemorrhagingStrike>(struck);
        double strike = f.CompletedAt("Hemorrhaging Strike");
        f.Wait(5); // two ticks

        using var check = new Checks(f);
        foreach (var enemy in new[] { struck, other })
        {
            var ticks = f.Damage("Hemorrhage").Where(hit => hit.Target == enemy.Name).ToList();
            check.True(ticks.Count >= 2, what: $"two ticks on {enemy.Name}");
            check.Seconds(strike + tick, ticks[0].Time.TotalSeconds, what: $"first tick on {enemy.Name}");
            check.Seconds(tick, ticks[1].Time.TotalSeconds - ticks[0].Time.TotalSeconds, what: $"tick period on {enemy.Name}");
        }
    }

    /// <summary>
    /// With Gushing Blood, each Hemorrhaging Strike during Maiden of Death puts a new bleed on the struck enemy, which restarts its ticks,
    /// even with no other enemy around: recast on every global cooldown while Maiden of Death lasts, the bleed deals no tick, and the first
    /// tick comes 3 seconds after the last strike.
    ///
    /// From a combat log: fjrCax7VzGmMZw6h fight 26 (Mara player 23) logs every strike in Maiden of Death as a refresh, a removal and an
    /// application of the bleed on the struck enemy, 27 of them with no other enemy, and the next tick comes about one period after the
    /// strike; outside Maiden of Death a strike is a refresh alone and the ticks keep their schedule.
    /// </summary>
    [Fact]
    public void Gushing_Blood_Restarts_The_Bleeds_Ticks_On_Each_Strike_During_Maiden_Of_Death()
    {
        var f = StepFights.Mara(talents: [GushingBlood.TalentId]);

        f.Cast<MaidenOfDeath>();
        while (f.Has(MaidenOfDeath.Buff)) // a strike on every global cooldown
        {
            f.FillResources(); // Energy and Combo Points for the strike
            f.Cast<HemorrhagingStrike>();
        }
        double last = f.CompletedAt("Hemorrhaging Strike");
        f.Wait(3.5); // the first tick

        var ticks = f.Damage("Hemorrhage");
        using var check = new Checks(f);
        check.Equal(1, ticks.Count);
        check.Seconds(last + 3, ticks[0].Time.TotalSeconds);
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
