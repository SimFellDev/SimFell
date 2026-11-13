using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;
using FellowshipSim.Core.Heroes.Mara.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Mara's abilities, on a bare Mara: each ability's damage as a share of her main stat, how finishers grow with Combo Points, area
/// falloff, Hemorrhaging Strike's bleed, Stealth and the poisons it brings, Maiden of Death, Final Stratagem's reset, Macabre Stratagem,
/// and Matriarch Macabre with its shadowy clones.
///
/// Damage has no spread, so two hits' ratio is exact. The tooltips give each ability's damage as a share of the main stat, so a hit is
/// compared with a Skittering Blades hit on one enemy (80.6% of it) rather than with an absolute number. No haste, so Creeping Death (0.5%
/// more poison and bleed damage per 1% haste) is out of the way.
/// </summary>
public class MaraAbilities
{
    /// <summary>The main stat, measured as a Skittering Blades hit on a single enemy with nothing else on, which deals 80.6% of it.</summary>
    private static readonly double MainStat = MainStatFromSkitteringBlades();

    private static double MainStatFromSkitteringBlades()
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Cast<SkitteringBlades>();
        return f.Damage<SkitteringBlades>().First().Value / 0.806;
    }

    // ---- Damage of each ability

    /// <summary>
    /// Each ability deals the share of your main stat its tooltip gives: Queen's Fang 357.2% (with 1 Combo Point, which adds 20%),
    /// Arachnid Assault 117.22% (the same), Hemorrhaging Strike's hit 286.65%, Widow's Bite 145.2% with the right hand and 108% with the
    /// left, Skittering Blades 80.6%.
    ///
    /// One cast of each, with 1 Combo Point for the finishers; the first hit over the main stat, within 0.1% of the coefficient.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang", 3.572 * 1.2)]
    [InlineData("Arachnid Assault", 1.1722 * 1.2)]
    [InlineData("Hemorrhaging Strike", 2.8665)]
    [InlineData("Widow's Bite", 1.452)]
    public void Each_Ability_Deals_Its_Share_Of_The_Main_Stat(string ability, double coefficient)
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Set(Mara.ComboPoints, 1);

        f.Cast(ability);

        AssertRatio(coefficient, f.Damage(ability).First().Value / MainStat, coefficient * RatioTolerance);
    }

    // ---- Widow's Bite

    /// <summary>Widow's Bite strikes twice: 145.2% of your main stat with the right hand, then 108% with the left.</summary>
    [Fact]
    public void Widows_Bite_Strikes_Twice_Right_Hand_Then_Left()
    {
        var f = StepFights.Mara().WithoutDamageSpread();

        f.Cast<WidowsBite>();
        var hits = f.Damage<WidowsBite>();

        Assert.Equal(2, hits.Count);
        AssertRatio(1.08 / 1.452, hits[1].Value / hits[0].Value);
    }

    // ---- Backstab

    /// <summary>
    /// Backstab deals 99.6% of your main stat, and 40% more when you attack from behind.
    ///
    /// TODO: the engine always attacks from behind (the 40% is always applied, marked unverified there). Check in game whether the sim
    /// should.
    /// </summary>
    [Fact(Skip = "Backstab from behind: the engine always applies the 40%, unverified: to check in game")]
    public void Backstab_Deals_99_6_Percent_Of_The_Main_Stat_Plus_40_Percent_From_Behind()
    {
        var f = StepFights.Mara();

        f.Cast<Backstab>();

        AssertRatio(0.996 * 1.4, f.Damage<Backstab>().First().Value / MainStat);
    }

    // ---- Finishers

    /// <summary>
    /// Queen's Fang and Arachnid Assault grow with the Combo Points spent: +20% for each, so +120% with 6. A finisher's hit over the
    /// same finisher's hit at 1 Combo Point is (1 + 0.2 x points) / 1.2.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang", 2)]
    [InlineData("Queen's Fang", 6)]
    [InlineData("Arachnid Assault", 3)]
    [InlineData("Arachnid Assault", 6)]
    public void Finishers_Deal_20_Percent_More_Per_Combo_Point(string ability, int points)
    {
        double Hit(int comboPoints)
        {
            var f = StepFights.Mara().WithoutDamageSpread();
            f.Set(Mara.ComboPoints, comboPoints);
            f.Cast(ability);
            return f.Damage(ability).First().Value;
        }

        AssertRatio((1 + 0.2 * points) / 1.2, Hit(points) / Hit(1));
    }

    // ---- Hemorrhaging Strike

    /// <summary>
    /// Only the bleed of Hemorrhaging Strike grows with the Combo Points spent (in duration); its hit does not: the hit at 1 and at 6
    /// Combo Points is the same.
    /// </summary>
    [Fact]
    public void Hemorrhaging_Strikes_Hit_Does_Not_Grow_With_Combo_Points()
    {
        double Hit(int comboPoints)
        {
            var f = StepFights.Mara().WithoutDamageSpread();
            f.Set(Mara.ComboPoints, comboPoints);
            f.Cast<HemorrhagingStrike>();
            return f.Damage<HemorrhagingStrike>().First().Value;
        }

        Assert.Equal(Hit(1), Hit(6), 1e-6);
    }

    /// <summary>
    /// Hemorrhaging Strike's bleed lasts 12 seconds, and 3 seconds more for each Combo Point spent (30 seconds at 6).
    ///
    /// The time left on the bleed after the cast, plus the time the cast took, within 0.01s.
    /// </summary>
    [Theory]
    [InlineData(1, 15)]
    [InlineData(3, 21)]
    [InlineData(6, 30)]
    public void The_Bleed_Lasts_12_Seconds_Plus_3_Per_Combo_Point(int points, double seconds)
    {
        var f = StepFights.Mara();
        f.Set(Mara.ComboPoints, points);
        var start = f.Now;

        f.Cast<HemorrhagingStrike>();

        Assert.Equal(seconds, f.MaraOf().Hemorrhage.Remaining(f.Enemy, f.Context) + (f.Now - start).TotalSeconds, 0.01);
    }

    /// <summary>
    /// Hemorrhaging Strike's bleed deals 98.41% of your main stat every 3 seconds.
    ///
    /// The first tick over the main stat, within 0.1% of the coefficient.
    /// </summary>
    [Fact]
    public void The_Bleed_Deals_98_41_Percent_Of_The_Main_Stat_Per_Tick()
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Set(Mara.ComboPoints, 1);

        f.Cast<HemorrhagingStrike>();
        f.Wait(4); // the first tick

        AssertRatio(0.9841, f.Damage("Hemorrhage").First().Value / MainStat, 0.9841 * RatioTolerance);
    }

    /// <summary>
    /// A Hemorrhaging Strike on a bleeding enemy gives the bleed its new duration plus the time it had left, up to 30% of the new duration
    /// (130% at most): 12 + 3 seconds per Combo Point, plus min(time left, 0.3 x that). A short strike on a long bleed shortens it.
    ///
    /// Checked in game by the user. The rows: the cap reached, the time left under the cap, and a 1 Combo Point strike on a 30 second bleed.
    /// </summary>
    [Theory]
    [InlineData(6, 5.0, 6)]
    [InlineData(1, 12.0, 6)]
    [InlineData(6, 1.0, 1)]
    public void Refreshing_The_Bleed_Carries_Over_Up_To_30_Percent(int firstPoints, double wait, int points)
    {
        var f = StepFights.Mara();

        f.Set(Mara.ComboPoints, firstPoints);
        f.Cast<HemorrhagingStrike>();
        double firstEnd = f.CompletedAt("Hemorrhaging Strike") + 12 + 3 * firstPoints;
        f.Wait(wait);
        f.Set(Mara.ComboPoints, points);
        f.Cast<HemorrhagingStrike>();
        double refresh = f.CompletedAt("Hemorrhaging Strike");
        double duration = 12 + 3 * points;
        double end = refresh + duration + Math.Min(firstEnd - refresh, 0.3 * duration);

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Hemorrhage"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Hemorrhage"));
    }

    /// <summary>
    /// A Hemorrhaging Strike on a bleeding enemy refreshes the bleed and keeps its ticks on time: recast on every global cooldown for 12
    /// seconds, the bleed still ticks every 3 seconds from the first strike.
    ///
    /// From combat logs (Mara's NOTES.md: "A refresh keeps the tick timing").
    /// </summary>
    [Fact]
    public void Recasting_Hemorrhaging_Strike_Keeps_The_Bleeds_Ticks_On_Time()
    {
        var f = StepFights.Mara();

        f.FillResources(); // Energy and Combo Points for the strike
        f.Cast<HemorrhagingStrike>();
        double first = f.CompletedAt("Hemorrhaging Strike");
        while (f.Seconds() < first + 12) // a recast on every global cooldown
        {
            f.FillResources();
            f.Cast<HemorrhagingStrike>();
        }

        var ticks = f.Damage("Hemorrhage").Where(tick => tick.Time.TotalSeconds < first + 12.5).ToList();
        using var check = new Checks(f);
        check.Equal(4, ticks.Count);
        for (int i = 0; i < ticks.Count; i++) check.Seconds(first + 3 * (i + 1), ticks[i].Time.TotalSeconds, what: $"tick {i + 1}");
    }

    // ---- Area damage

    /// <summary>
    /// Skittering Blades and Arachnid Assault hit every enemy around you, and beyond 8 of them each hit is weaker: it is multiplied by
    /// sqrt(8 / enemies).
    ///
    /// One cast at 8 and at 12 enemies, with 3 Combo Points; a hit on each enemy, and a hit at 12 over a hit at 8 is sqrt(8 / 12).
    /// </summary>
    [Theory]
    [InlineData("Skittering Blades")]
    [InlineData("Arachnid Assault")]
    public void Area_Abilities_Hit_Every_Enemy_And_Fall_Off_Beyond_8(string ability)
    {
        List<StepEvent> Hits(int enemies)
        {
            var f = StepFights.Mara(enemies: enemies).WithoutDamageSpread();
            f.Set(Mara.ComboPoints, 3); // enough to cast Arachnid Assault
            f.Cast(ability);
            return f.Damage(ability);
        }

        var eightEnemyHits = Hits(8);
        var twelveEnemyHits = Hits(12);

        using var check = new Checks();
        check.Equal(8, eightEnemyHits.Count);
        check.Equal(12, twelveEnemyHits.Count);
        check.Ratio(Math.Sqrt(8.0 / 12.0), twelveEnemyHits[0].Value / eightEnemyHits[0].Value);
    }

    // ---- Stealth

    /// <summary>Brooding Shadows puts you in Stealth, and your next Backstab, Widow's Bite or Skittering Blades ends it.</summary>
    [Theory]
    [InlineData("Stealth Backstab")]
    [InlineData("Widow's Bite")]
    [InlineData("Stealth Skittering Blades")]
    public void An_Attack_Ends_Stealth(string ability)
    {
        var f = StepFights.Mara();

        f.Cast<BroodingShadows>();
        Assert.True(f.Has(BroodingShadows.Stealth));

        f.Cast(ability);
        Assert.False(f.Has(BroodingShadows.Stealth));
    }

    /// <summary>
    /// In Stealth, Backstab and Skittering Blades are replaced by their Stealth versions, which only work in Stealth, and your auto attack
    /// does not swing: the Stealth versions are refused outside Stealth, and Backstab, Skittering Blades and the auto attack in it.
    /// </summary>
    [Fact]
    public void Stealth_Replaces_Backstab_And_Skittering_Blades_And_Stops_The_Auto_Attack()
    {
        var f = StepFights.Mara();

        using var check = new Checks(f);
        check.False(f.TryCast<StealthBackstab>().Succeeded);
        check.False(f.TryCast<StealthSkitteringBlades>().Succeeded);

        f.Cast<BroodingShadows>();

        check.False(f.TryCast<Backstab>().Succeeded);
        check.False(f.TryCast<SkitteringBlades>().Succeeded);
        check.False(f.TryCast<Attack>().Succeeded);
    }

    /// <summary>
    /// Backstab from Stealth applies Caustic Poison: 478.93% of your main stat at once, always a critical strike (+100% crit chance), and it
    /// generates 6 Combo Points.
    ///
    /// The hero has no crit chance of her own. The crit factor is read as a Skittering Blades hit with 100% crit over one without; the
    /// poison's damage is checked within 0.1% of 4.7893 times it.
    /// </summary>
    [Fact]
    public void Stealth_Backstab_Applies_Caustic_Poison()
    {
        var critScenario = StepFights.Mara(crit: 1.0).WithoutDamageSpread();
        critScenario.Cast<SkitteringBlades>();
        double critFactor = critScenario.Damage<SkitteringBlades>().First().Value / 0.806 / MainStat;

        var f = StepFights.Mara().WithoutDamageSpread();

        f.Cast<BroodingShadows>();
        f.Cast<StealthBackstab>();
        var poison = f.Damage("Caustic Poison").First();

        using var check = new Checks(f);
        check.True(poison.CritChance >= 1.0);
        check.Equal(4.7893 * critFactor, poison.Value / MainStat, 4.7893 * critFactor * RatioTolerance);
        check.Equal(6, f.Amount(Mara.ComboPoints), 0.01);
    }

    /// <summary>
    /// Widow's Bite from Stealth applies Seething Poison, 122.76% of your main stat every 2 seconds for 60 seconds.
    ///
    /// The first tick over the main stat, within 0.1% of the coefficient; the first two ticks 2 seconds apart.
    /// </summary>
    [Fact]
    public void Widows_Bite_From_Stealth_Applies_Seething_Poison()
    {
        var f = StepFights.Mara().WithoutDamageSpread();

        f.Cast<BroodingShadows>(); // into Stealth
        f.Cast<WidowsBite>();
        f.Wait(5); // two ticks
        var ticks = f.Damage("Seething Poison");

        Assert.True(ticks.Count >= 2);
        using var check = new Checks(f);
        check.Equal(1.2276, ticks[0].Value / MainStat, 1.2276 * RatioTolerance);
        check.Seconds(2.0, (ticks[1].Time - ticks[0].Time).TotalSeconds);
    }

    /// <summary>
    /// Seething Poison can only be on one enemy at a time: putting it on another takes it off the first. With 2 enemies, a Widow's Bite
    /// from Stealth on each; afterwards the poison ticks only on the second.
    /// </summary>
    [Fact]
    public void Seething_Poison_Is_On_One_Enemy_At_A_Time()
    {
        var f = StepFights.Mara(enemies: 2);
        var first = f.Enemies()[0];
        var second = f.Enemies()[1];

        f.Cast<BroodingShadows>(); // into Stealth
        f.Cast<WidowsBite>(first);
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>(second);
        f.Wait(10);
        var ticks = f.Damage("Seething Poison");

        using var check = new Checks(f);
        check.Equal(1, ticks.Select(tick => tick.Target).Where(target => target != null).Distinct().Count(target => target == second.Name));
        check.False(ticks.Any(tick => tick.Time > f.Now - SimTime.FromSeconds(8) && tick.Target == first.Name));
    }

    /// <summary>
    /// Putting Seething Poison back on an enemy that has it restarts its ticks: Brooding Shadows and Widow's Bite chained on one enemy, a
    /// Widow's Bite on every global cooldown for 10 seconds, deal no tick, and the first tick comes 2 seconds after the last Widow's Bite.
    ///
    /// Checked in game by the user. The cooldowns are reset between casts, so the chain goes on past the charges.
    /// </summary>
    [Fact]
    public void Renewing_Seething_Poison_Restarts_Its_Ticks()
    {
        var f = StepFights.Mara();

        f.Cast<BroodingShadows>(); // into Stealth
        f.Cast<WidowsBite>();
        double first = f.CompletedAt("Widow's Bite");
        while (f.Seconds() < first + 10) // a Widow's Bite from Stealth on every global cooldown
        {
            f.ResetCooldown<BroodingShadows>(); // past its 2 charges
            f.ResetCooldown<WidowsBite>(); // past its 3 charges
            f.Cast<BroodingShadows>();
            f.Cast<WidowsBite>();
        }
        double last = f.CompletedAt("Widow's Bite");
        f.Wait(3); // the first tick

        var ticks = f.Damage("Seething Poison");
        using var check = new Checks(f);
        check.Equal(1, ticks.Count);
        check.Seconds(last + 2, ticks[0].Time.TotalSeconds);
    }

    /// <summary>
    /// An enemy with Seething Poison on it takes 20% more damage from Hemorrhaging Strike's bleed: the bleed's first tick is 1.2 times
    /// as strong as on an enemy without the poison.
    /// </summary>
    [Fact]
    public void Seething_Poison_Raises_The_Bleeds_Damage_By_20_Percent()
    {
        var plainScenario = StepFights.Mara().WithoutDamageSpread();
        plainScenario.Set(Mara.ComboPoints, 1);

        plainScenario.Cast<HemorrhagingStrike>();
        plainScenario.Wait(4); // the first tick
        double plainBleedDamage = plainScenario.Damage("Hemorrhage").First().Value;

        var poisonedScenario = StepFights.Mara().WithoutDamageSpread();

        poisonedScenario.Cast<BroodingShadows>(); // into Stealth
        poisonedScenario.Cast<WidowsBite>(); // Seething Poison
        poisonedScenario.Set(Mara.ComboPoints, 1);
        poisonedScenario.Cast<HemorrhagingStrike>();
        poisonedScenario.Wait(4); // the first tick
        double poisonedBleedDamage = poisonedScenario.Damage("Hemorrhage").First().Value;

        AssertRatio(1.2, poisonedBleedDamage / plainBleedDamage);
    }

    /// <summary>
    /// Skittering Blades from Stealth applies Volatile Poison to every enemy hit: 31.2% of your main stat on each tick, and at the end it
    /// erupts for 124.8% (4 times a tick).
    ///
    /// 2 enemies; both take ticks and an eruption, each within 0.1% of its coefficient over the main stat.
    /// </summary>
    [Fact]
    public void Skittering_Blades_From_Stealth_Applies_Volatile_Poison_To_Every_Enemy_Hit()
    {
        var f = StepFights.Mara(enemies: 2).WithoutDamageSpread();

        f.Cast<BroodingShadows>(); // into Stealth
        f.Cast<StealthSkitteringBlades>();
        f.Wait(10); // the poison is over and has erupted
        var ticks = f.Ticks(VolatilePoison.EffectId);
        var eruptions = f.Ticks(VolatilePoison.EruptionEffectId);

        using var check = new Checks(f);
        check.Equal(2, eruptions.Count);
        check.Equal(2, ticks.Select(tick => tick.Target).Distinct().Count());
        foreach (var tick in ticks) check.Equal(0.312, tick.Value / MainStat, 0.312 * RatioTolerance, what: "a tick over the main stat");
        foreach (var eruption in eruptions) check.Equal(1.248, eruption.Value / MainStat, 1.248 * RatioTolerance, what: "an eruption over the main stat");
    }

    // ---- Maiden of Death

    /// <summary>
    /// Maiden of Death makes you deal 20% more damage for 10 seconds: a Skittering Blades hit just after it is 1.2 times one before it,
    /// and one 10 seconds later is back to 1.0 times.
    /// </summary>
    [Fact]
    public void Maiden_Of_Death_Deals_20_Percent_More_Damage_For_10_Seconds()
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Set(Mara.Energy, 200);

        f.Cast<SkitteringBlades>();
        double beforeDamage = f.Damage<SkitteringBlades>()[0].Value;
        f.Cast<MaidenOfDeath>();
        f.Cast<SkitteringBlades>();
        double duringDamage = f.Damage<SkitteringBlades>()[1].Value;
        f.Wait(10); // Maiden of Death is over
        f.Cast<SkitteringBlades>();
        double afterDamage = f.Damage<SkitteringBlades>()[2].Value;

        using var check = new Checks(f);
        check.Ratio(1.2, duringDamage / beforeDamage);
        check.Ratio(1.0, afterDamage / beforeDamage);
    }

    /// <summary>While Maiden of Death is active every Combo Point generator generates 6 Combo Points: Backstab, Skittering Blades and
    /// Widow's Bite.</summary>
    [Theory]
    [InlineData("Backstab")]
    [InlineData("Skittering Blades")]
    [InlineData("Widow's Bite")]
    public void Maiden_Of_Death_Makes_Generators_Generate_6_Combo_Points(string ability)
    {
        var f = StepFights.Mara();

        f.Cast<MaidenOfDeath>();

        f.Cast(ability);

        Assert.Equal(6, f.Amount(Mara.ComboPoints), 0.01);
    }

    // ---- Final Stratagem

    /// <summary>
    /// Final Stratagem refills your Energy (200) and Combo Points (6), and resets the cooldowns of your abilities, except its own: Widow's
    /// Bite and Brooding Shadows have all their charges back, Maiden of Death is ready, and Final Stratagem is still on cooldown 100
    /// seconds later.
    ///
    /// TODO: the engine's reset covers every ability but Final Stratagem, the Spirit ability and the auto attack, from the tooltip alone (no
    /// combat log has it).
    /// </summary>
    [Fact]
    public void Final_Stratagem_Refills_Resources_And_Resets_Cooldowns()
    {
        var f = StepFights.Mara();

        f.Cast<WidowsBite>();
        f.Cast<WidowsBite>();
        f.Cast<BroodingShadows>();
        f.Cast<MaidenOfDeath>();
        f.Set(Mara.Energy, 50);

        f.Cast<FinalStratagem>();

        using var check = new Checks(f);
        check.Equal(200, f.MaraOf().EnergyAt(f.Now), 0.01);
        check.Equal(6, f.Amount(Mara.ComboPoints), 0.01);
        check.Equal(3, f.Charges<WidowsBite>());
        check.Equal(2, f.Charges<BroodingShadows>());
        check.True(f.IsReady<MaidenOfDeath>());
        f.WaitUntil(f.Seconds() + 100);
        check.False(f.IsReady<FinalStratagem>());
    }

    // ---- Macabre Stratagem (its cooldown is in MaraCooldowns.cs)

    /// <summary>
    /// Macabre Stratagem replaces Final Stratagem when you have the talent, and cannot be used without it: without the talent Macabre
    /// Stratagem is refused; with it Final Stratagem is.
    /// </summary>
    [Fact]
    public void Macabre_Stratagem_Replaces_Final_Stratagem()
    {
        var plainScenario = StepFights.Mara();
        var talentScenario = StepFights.Mara(talents: [MacabreStratagemTalent.TalentId]);

        using var check = new Checks();
        check.False(plainScenario.TryCast<MacabreStratagem>().Succeeded);
        check.False(talentScenario.TryCast<FinalStratagem>().Succeeded);
        check.True(talentScenario.TryCast<MacabreStratagem>().Succeeded);
    }

    /// <summary>
    /// Macabre Stratagem gives 10 seconds of Matriarch Macabre, and no extra haste; used again while it is active it extends it by 10
    /// seconds. The buff is up after the cast and 9 seconds later, and gone 11 seconds later.
    ///
    /// TODO: the extension (a second use while active, +10 seconds) is not tested: the 180 second cooldown stops a second use. Final
    /// Stratagem's reset does not apply to it either. Needs a cooldown reset to test.
    /// </summary>
    [Fact]
    public void Macabre_Stratagem_Gives_10_Seconds_Of_Matriarch_Macabre()
    {
        var f = StepFights.Mara(talents: [MacabreStratagemTalent.TalentId]);

        f.Cast<MacabreStratagem>();
        double start = f.Seconds();

        using var check = new Checks(f);
        check.True(f.Has(MatriarchMacabre.Buff));
        check.Equal(0.0, f.Haste(), 0.001);
        f.WaitUntil(start + 9);
        check.True(f.Has(MatriarchMacabre.Buff));
        f.WaitUntil(start + 11);
        check.False(f.Has(MatriarchMacabre.Buff));
    }

    // ---- Matriarch Macabre (what every ultimate shares is in Generic/Ultimate.cs)

    /// <summary>
    /// Matriarch Macabre, your Spirit ability, needs Spirit Points. It gives Spirit of Heroism (+30% haste) and makes you deal 20% more
    /// damage for 20 seconds: refused without Spirit Points; with them, the haste is 30% higher and a Skittering Blades hit is 1.2 times
    /// as strong, and after 20 seconds both are gone.
    /// </summary>
    [Fact]
    public void Matriarch_Macabre_Gives_30_Percent_Haste_And_20_Percent_Damage_For_20_Seconds()
    {
        var f = StepFights.Mara().WithoutDamageSpread();

        using var check = new Checks(f);
        check.False(f.TryCast<MatriarchMacabre>().Succeeded);

        f.Cast<SkitteringBlades>();
        double beforeDamage = f.Damage<SkitteringBlades>()[0].Value;
        f.Set(Hero.SpiritPoints, 100); // enough to cast it
        f.Cast<MatriarchMacabre>();
        f.Cast<SkitteringBlades>();
        double duringDamage = f.Damage<SkitteringBlades>()[1].Value;

        check.Equal(0.3, f.Haste(), 0.001);
        check.Ratio(1.2, duringDamage / beforeDamage);

        f.Wait(20); // Matriarch Macabre is over
        check.Equal(0.0, f.Haste(), 0.001);
        f.Cast<SkitteringBlades>();
        double afterDamage = f.Damage<SkitteringBlades>()[2].Value;
        check.Ratio(1.0, afterDamage / beforeDamage);
    }

    /// <summary>
    /// During Matriarch Macabre your Queen's Fang summons two shadowy clones that each repeat it for 50% of its damage: 2 clone hits, each
    /// half of the cast's own hit.
    /// </summary>
    [Fact]
    public void Queens_Fang_Summons_Two_Clones_At_50_Percent()
    {
        var f = StepFights.Mara().WithoutDamageSpread();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Matriarch Macabre

        f.Cast<MatriarchMacabre>();
        f.Set(Mara.ComboPoints, 3);

        f.Cast<QueensFang>();
        f.Wait(2); // the clones strike
        var ownHits = f.Damage<QueensFang>();
        var cloneHits = f.Damage("Queen's Fang (shadowy clone)");

        using var check = new Checks(f);
        check.Equal(1, ownHits.Count);
        check.Equal(2, cloneHits.Count);
        foreach (var clone in cloneHits) check.Ratio(0.5, clone.Value / ownHits[0].Value, what: "a clone hit over the cast's own");
    }

    /// <summary>
    /// The clones of an Arachnid Assault each hit every enemy the cast hit, at 50% of the damage and with the cast's area falloff, sqrt(8 /
    /// enemies) beyond 8: at 10 enemies, 20 clone hits, each 0.5 x sqrt(8 / 10) times a cast's hit on 8 enemies.
    ///
    /// Game data: GE_Mara_AoeAttackSpender_Damage applies the falloff to the cast and both clones; combat logs: a clone's hit over the
    /// cast's is 0.49 to 0.51 at every target count (NOTES.md, Shadowy clones).
    /// </summary>
    [Fact]
    public void Arachnid_Assaults_Clones_Hit_Every_Enemy_At_50_Percent_With_The_Falloff()
    {
        StepDriver Assault(int enemies)
        {
            var f = StepFights.Mara(enemies: enemies).WithoutDamageSpread();
            f.Set(Hero.SpiritPoints, 100); // enough to cast Matriarch Macabre

            f.Cast<MatriarchMacabre>();
            f.Set(Mara.ComboPoints, 3);

            f.Cast<ArachnidAssault>();
            f.Wait(2); // the clones strike
            return f;
        }

        double eightEnemyHit = Assault(8).Damage<ArachnidAssault>()[0].Value;
        var tenEnemyScenario = Assault(10);
        var cloneHits = tenEnemyScenario.Damage("Arachnid Assault (shadowy clone)");

        using var check = new Checks(tenEnemyScenario);
        check.Equal(10, tenEnemyScenario.Damage<ArachnidAssault>().Count);
        check.Equal(20, cloneHits.Count);
        foreach (var clone in cloneHits)
            check.Ratio(0.5 * Math.Sqrt(8.0 / 10.0), clone.Value / eightEnemyHit, what: "a clone hit over a cast's hit on 8 enemies");
    }

    // ---- Creeping Death

    /// <summary>
    /// Creeping Death makes your poisons and bleeds deal 0.5% more damage for every 1% haste: the first tick of the bleed (at 1 Combo
    /// Point) at 20% and 40% haste is 1 + 0.5 x haste times the one at no haste.
    /// </summary>
    [Theory]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Creeping_Death_Raises_Poison_And_Bleed_Damage_By_Half_The_Haste(double haste)
    {
        double FirstBleedTick(double atHaste)
        {
            var f = StepFights.Mara(haste: atHaste).WithoutDamageSpread();
            f.Set(Mara.ComboPoints, 1); // enough to cast Hemorrhaging Strike

            f.Cast<HemorrhagingStrike>();
            f.Wait(4); // the first tick
            return f.Damage("Hemorrhage").First().Value;
        }

        AssertRatio(1 + 0.5 * haste, FirstBleedTick(haste) / FirstBleedTick(0));
    }
}
