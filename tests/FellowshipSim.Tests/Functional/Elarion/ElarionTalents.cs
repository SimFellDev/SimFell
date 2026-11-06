using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's talents, on a bare Elarion: each talent's own rule first, then how it works with other talents and abilities. Their effects on
/// cooldowns are in ElarionCooldowns.cs, on Focus in ElarionResourceEconomy.cs, on cast times in ElarionCastTimes.cs, and on how many
/// hits fit in a channel in ElarionHasteBreakpoints.cs.
/// </summary>
public class ElarionTalents
{
    /// <summary>The first arrow of a plain Multishot, at one enemy.</summary>
    private static double PlainMultishotArrow()
    {
        var f = StepFights.Elarion().WithoutDamageSpread();

        f.Cast<Multishot>();
        return f.Damage<Multishot>().First().Value;
    }

    /// <summary>The arrows of a Multishot that Skystrider's Supremacy empowers, at one enemy, with the talents given.</summary>
    private static List<StepEvent> EmpoweredMultishotArrows(params int[] talents)
    {
        var f = StepFights.Elarion(talents: talents).WithoutDamageSpread();

        f.Cast<SkystridersSupremacy>();
        f.Cast<Multishot>();
        return f.Damage<Multishot>();
    }

    // ---- Impending Heartseeker

    /// <summary>
    /// With Impending Heartseeker, each arrow of the next Heartseeker Barrage deals 10% more than the one before, starting with the first:
    /// 110%, 120%, ... the tenth 200%, 15.5 arrows' worth in all.
    ///
    /// The buff is applied directly, without haste, so the barrage fires 10 arrows. The tooltip ("+10% increased damage with each arrow
    /// during its channel") does not say whether the first arrow already has +10%; the engine gives it
    /// (HeartseekerBarrage.ImpendingDamagePerArrow). TODO: check with datamine.
    /// </summary>
    [Fact]
    public void Impending_Heartseeker_Raises_Each_Barrage_Arrow_By_10_Percent_Starting_With_The_First()
    {
        var plainScenario = StepFights.Elarion().WithoutDamageSpread();

        plainScenario.Cast<HeartseekerBarrage>();
        double plainArrowDamage = plainScenario.Damage<HeartseekerBarrage>().First().Value;

        var impendingScenario = StepFights.Elarion().WithoutDamageSpread();
        impendingScenario.Apply(ImpendingHeartseeker.Buff); // as a Celestial Shot using Celestial Impetus would give it

        impendingScenario.Cast<HeartseekerBarrage>();
        var arrows = impendingScenario.Damage<HeartseekerBarrage>();

        using var check = new Checks(impendingScenario);
        check.Equal(10, arrows.Count);
        for (int k = 0; k < arrows.Count; k++) check.Ratio(1 + 0.1 * (k + 1), arrows[k].Value / plainArrowDamage, what: $"arrow {k + 1}");
        check.Ratio(15.5, arrows.Sum(arrow => arrow.Value) / plainArrowDamage);
    }

    /// <summary>Impending Heartseeker is used up by the barrage it empowers: the next barrage's arrows all deal the same damage.</summary>
    [Fact]
    public void Impending_Heartseeker_Is_Used_Up_By_One_Barrage()
    {
        var f = StepFights.Elarion().WithoutDamageSpread();
        f.Apply(ImpendingHeartseeker.Buff); // as a Celestial Shot using Celestial Impetus would give it

        f.Cast<HeartseekerBarrage>();

        using var check = new Checks(f);
        check.False(f.Has(ImpendingHeartseeker.Buff));

        f.ResetCooldown<HeartseekerBarrage>(); // ready again at once
        f.Cast<HeartseekerBarrage>();
        var secondArrows = f.Damage<HeartseekerBarrage>().Skip(10).ToList(); // the first barrage fires 10 arrows without haste
        check.Equal(10, secondArrows.Count);
        for (int i = 1; i < secondArrows.Count; i++) check.Ratio(1.0, secondArrows[i].Value / secondArrows[0].Value, what: $"arrow {i + 1}");
    }

    /// <summary>Impending Heartseeker fades after 15 seconds if no barrage uses it.</summary>
    [Fact]
    public void Impending_Heartseeker_Lasts_15_Seconds()
    {
        var f = StepFights.Elarion();
        f.Apply(ImpendingHeartseeker.Buff); // as a Celestial Shot using Celestial Impetus would give it, at 0s

        f.WaitUntil(14.9);
        Assert.True(f.Has(ImpendingHeartseeker.Buff));
        f.WaitUntil(15.1);
        Assert.False(f.Has(ImpendingHeartseeker.Buff));
    }

    /// <summary>
    /// With Impending Heartseeker, a Celestial Shot that uses Celestial Impetus resets Heartseeker Barrage's cooldown and gives you
    /// Impending Heartseeker. Without the talent, the same shot does neither.
    ///
    /// The engine's source is the tooltip: no log has the talent (ImpendingHeartseeker.cs). When it happens is the next test's.
    /// </summary>
    [Fact]
    public void A_Celestial_Shot_Using_Impetus_Resets_The_Barrage_And_Gives_Impending_Heartseeker()
    {
        var plainScenario = StepFights.Elarion();

        plainScenario.Cast<HeartseekerBarrage>(); // on cooldown
        plainScenario.Apply(CelestialShot.Impetus); // as a Focused Shot's hit would give it
        plainScenario.Cast<CelestialShot>();

        using var check = new Checks(plainScenario);
        check.False(plainScenario.IsReady<HeartseekerBarrage>());
        check.False(plainScenario.Has(ImpendingHeartseeker.Buff));

        var impendingScenario = StepFights.Elarion(talents: [ImpendingHeartseeker.TalentId]);

        impendingScenario.Cast<HeartseekerBarrage>(); // on cooldown
        impendingScenario.Apply(CelestialShot.Impetus); // as a Focused Shot's hit would give it
        impendingScenario.Cast<CelestialShot>();

        check.True(impendingScenario.IsReady<HeartseekerBarrage>());
        check.True(impendingScenario.Has(ImpendingHeartseeker.Buff));
    }

    /// <summary>
    /// The reset and Impending Heartseeker come when the Celestial Shot hits, not when its cast ends: the 15 second buff runs from the
    /// hit.
    ///
    /// The game's timing (per the user). The engine gives Celestial Shot no travel time, so its hit and its cast's end are the same
    /// moment and this passes either way today; it tells them apart once the shot travels more than 0.1 seconds.
    /// TODO: what happens when the target dies before the hit.
    /// </summary>
    [Fact]
    public void The_Barrage_Resets_And_Impending_Heartseeker_Starts_When_The_Celestial_Shot_Hits()
    {
        var f = StepFights.Elarion(talents: [ImpendingHeartseeker.TalentId]);
        f.Cast<HeartseekerBarrage>(); // on cooldown
        f.Apply(CelestialShot.Impetus); // as a Focused Shot's hit would give it

        f.Cast<CelestialShot>();
        f.Wait(1.0); // the shot hits
        double hit = f.Damage<CelestialShot>().Single().Time.TotalSeconds;

        using var check = new Checks(f);
        check.True(f.IsReady<HeartseekerBarrage>());
        f.WaitUntil(hit + 15.0 - 0.1);
        check.True(f.Has(ImpendingHeartseeker.Buff));
        f.WaitUntil(hit + 15.0 + 0.1);
        check.False(f.Has(ImpendingHeartseeker.Buff));
    }

    // ---- Fusillade (its 2.5 second channel is in ElarionCastTimes.cs, its arrows at each haste in ElarionHasteBreakpoints.cs)

    /// <summary>With Fusillade, Heartseeker Barrage's arrows have +20% critical strike chance; your other abilities do not.</summary>
    [Fact]
    public void Fusillade_Adds_20_Percent_Crit_Chance_To_Heartseeker_Barrage_Only()
    {
        var f = StepFights.Elarion(crit: 0.15, talents: [Fusillade.TalentId]);

        f.Cast<FocusedShot>();
        f.Cast<HeartseekerBarrage>();

        using var check = new Checks(f);
        check.Equal(0.15, f.Damage<FocusedShot>().First().CritChance);
        check.Equal(0.35, f.Damage<HeartseekerBarrage>().First().CritChance);
    }

    // ---- Lunar Fury

    /// <summary>
    /// With Lunar Fury, Heartseeker Barrage's hits on a marked enemy have twice the chance to call down a Lunarlight Salvo: 50% instead of
    /// 25%.
    ///
    /// The chance of the barrage's first hit, which does not crit.
    /// </summary>
    [Theory]
    [InlineData(false, 0.25)]
    [InlineData(true, 0.5)]
    public void Lunar_Fury_Doubles_Heartseeker_Barrages_Salvo_Chance(bool lunarFury, double chance)
    {
        var f = StepFights.Elarion(talents: lunarFury ? [LunarFury.TalentId] : []);
        var procs = f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? false : null); // only to read the chance
        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy); // a salvo needs a marked enemy

        f.Cast<HeartseekerBarrage>();

        Assert.Equal(chance, procs.ChancesOf(LunarlightSalvo.ProcKey)[0], 1e-9);
    }

    // ---- Lethal Shots

    /// <summary>With Lethal Shots, each Highwind Arrow has a 40% chance to have +100% critical strike chance.</summary>
    [Fact]
    public void Lethal_Shots_Gives_Highwind_Arrow_A_40_Percent_Chance_Of_100_Percent_More_Crit_Chance()
    {
        var missScenario = StepFights.Elarion(talents: [LethalShots.TalentId]);
        var missProcs = missScenario.ForceProcs(key => key == LethalShots.ProcKey ? false : null);

        missScenario.Cast<HighwindArrow>();

        var procScenario = StepFights.Elarion(talents: [LethalShots.TalentId]);
        procScenario.ForceProcs(key => key == LethalShots.ProcKey ? true : null);

        procScenario.Cast<HighwindArrow>();

        using var check = new Checks(procScenario);
        check.Equal(0.4, missProcs.ChancesOf(LethalShots.ProcKey).Single());
        check.Equal(0.0, missScenario.Damage<HighwindArrow>().First().CritChance);
        check.Equal(1.0, procScenario.Damage<HighwindArrow>().First().CritChance);
    }

    // ---- Resurgent Winds (its instant cast is in ElarionCastTimes.cs, its free cast in ElarionResourceEconomy.cs)

    /// <summary>With Resurgent Winds, your next Highwind Arrow deals 50% more damage; the one after it deals normal damage.</summary>
    [Fact]
    public void Resurgent_Winds_Makes_The_Next_Highwind_Arrow_Deal_50_Percent_More()
    {
        var plainScenario = StepFights.Elarion().WithoutDamageSpread();

        plainScenario.Cast<HighwindArrow>();
        plainScenario.Wait(1.0); // the arrow lands
        double plainArrowDamage = plainScenario.Damage<HighwindArrow>().Single().Value;

        var windsScenario = StepFights.Elarion(talents: [ResurgentWinds.TalentId]).WithoutDamageSpread();
        windsScenario.ForceProcs(key => key == ResurgentWinds.ProcKey ? false : null); // no new Resurgent Winds for the second arrow
        windsScenario.Apply(ResurgentWinds.Buff); // as Lunarlight Mark would give it

        windsScenario.Cast<HighwindArrow>();
        windsScenario.Cast<HighwindArrow>();
        windsScenario.Wait(1.0); // the arrows land
        var windsArrows = windsScenario.Damage<HighwindArrow>();

        using var check = new Checks(windsScenario);
        check.Equal(2, windsArrows.Count);
        check.Ratio(1.5, windsArrows[0].Value / plainArrowDamage);
        check.Ratio(1.0, windsArrows[1].Value / plainArrowDamage);
    }

    // ---- Final Crescendo

    /// <summary>
    /// With Final Crescendo, every third Highwind Arrow deals double damage and strikes up to 9 enemies, its target and 8 ricochets; the
    /// first two strike 3, their target and 2 ricochets.
    ///
    /// The engine's 9 enemies at most, the target included, are from the combat logs (FinalCrescendo.cs).
    /// </summary>
    [Fact]
    public void Final_Crescendo_Makes_Every_3rd_Highwind_Arrow_Deal_Double_And_Strike_9_Enemies()
    {
        var f = StepFights.Elarion(enemies: 10, talents: [FinalCrescendo.TalentId]).WithoutDamageSpread();
        var hitsPerCast = new List<int>();

        for (int i = 0; i < 3; i++)
        {
            int before = f.Damage<HighwindArrow>().Count;
            f.Cast<HighwindArrow>();
            f.Wait(1.0); // the arrow and its ricochets land
            hitsPerCast.Add(f.Damage<HighwindArrow>().Count - before);
        }
        var hits = f.Damage<HighwindArrow>();

        using var check = new Checks(f);
        check.Equal(3, hitsPerCast[0]);
        check.Equal(3, hitsPerCast[1]);
        check.Equal(9, hitsPerCast[2]);
        check.Ratio(2.0, hits[6].Value / hits[0].Value); // the third cast's first hit against the first cast's
    }

    /// <summary>Final Crescendo counts again from nothing after the empowered Highwind Arrow: 0, 1 and 2 stacks before the casts, 0 after.</summary>
    [Fact]
    public void Final_Crescendo_Starts_Again_After_The_Empowered_Arrow()
    {
        var f = StepFights.Elarion(talents: [FinalCrescendo.TalentId]);

        using var check = new Checks(f);
        for (int i = 0; i < 3; i++)
        {
            check.Equal(i, f.Stacks(FinalCrescendo.Buff), what: $"stacks before cast {i + 1}");
            f.Cast<HighwindArrow>();
        }
        check.Equal(0, f.Stacks(FinalCrescendo.Buff));
    }

    /// <summary>Resurgent Winds' 50% and Final Crescendo's 100% multiply: a Highwind Arrow with both deals 3 times a plain one.</summary>
    [Fact]
    public void Resurgent_Winds_And_Final_Crescendo_Multiply()
    {
        var plainScenario = StepFights.Elarion().WithoutDamageSpread();

        plainScenario.Cast<HighwindArrow>();
        plainScenario.Wait(1.0); // the arrow lands
        double plainArrowDamage = plainScenario.Damage<HighwindArrow>().Single().Value;

        var bothScenario = StepFights.Elarion(talents: [ResurgentWinds.TalentId, FinalCrescendo.TalentId]).WithoutDamageSpread();
        bothScenario.ForceProcs(key => key == ResurgentWinds.ProcKey ? false : null); // no Resurgent Winds before the third arrow

        bothScenario.Cast<HighwindArrow>();
        bothScenario.Cast<HighwindArrow>();
        bothScenario.Apply(ResurgentWinds.Buff); // as Lunarlight Mark would give it
        bothScenario.Cast<HighwindArrow>(); // the third: Final Crescendo's
        bothScenario.Wait(1.0); // the arrow lands
        double bothArrowDamage = bothScenario.Damage<HighwindArrow>().Last().Value;

        AssertRatio(3.0, bothArrowDamage / plainArrowDamage);
    }

    // ---- Fervent Supremacy (its shorter cooldown is in ElarionCooldowns.cs, its cheaper Multishots in ElarionResourceEconomy.cs)

    /// <summary>
    /// With Fervent Supremacy, Skystrider's Supremacy gives 4 charges of Empowered Multishot: each Multishot uses one, and the fourth
    /// leaves none.
    /// </summary>
    [Fact]
    public void Fervent_Supremacy_Gives_4_Empowered_Multishots_One_Used_By_Each()
    {
        var f = StepFights.Elarion(talents: [FerventSupremacy.TalentId]);

        f.Cast<SkystridersSupremacy>();

        using var check = new Checks(f);
        check.Equal(4, f.Stacks(FerventSupremacy.Buff));
        foreach (int left in new[] { 3, 2, 1, 0 })
        {
            f.Cast<Multishot>();
            check.Equal(left, f.Stacks(FerventSupremacy.Buff), what: $"charges after a Multishot, {left} expected");
        }
    }

    /// <summary>
    /// While Fervent Supremacy is active, your Multishot deals 50% more damage: each of its 3 arrows deals 1.5 times a plain Multishot's.
    /// </summary>
    [Fact]
    public void Fervent_Supremacy_Makes_Multishot_Deal_50_Percent_More()
    {
        double plainArrowDamage = PlainMultishotArrow();
        var ferventArrows = EmpoweredMultishotArrows(FerventSupremacy.TalentId);

        using var check = new Checks();
        check.Equal(3, ferventArrows.Count);
        for (int k = 0; k < ferventArrows.Count; k++) check.Ratio(1.5, ferventArrows[k].Value / plainArrowDamage, what: $"arrow {k + 1}");
    }

    // ---- Focused Expanse

    /// <summary>
    /// With Focused Expanse, every Empowered Multishot deals 20% more damage, whichever Supremacy empowers it: each of its 3 arrows deals
    /// 1.2 times a plain Multishot's, and with Fervent Supremacy's 50% as well, the two multiply to 1.8 times.
    /// </summary>
    [Theory]
    [InlineData(false, 1.2)]
    [InlineData(true, 1.8)]
    public void Focused_Expanse_Makes_Empowered_Multishots_Deal_20_Percent_More(bool fervent, double ratio)
    {
        int[] talents = fervent ? [FocusedExpanse.TalentId, FerventSupremacy.TalentId] : [FocusedExpanse.TalentId];

        double plainArrowDamage = PlainMultishotArrow();
        var empoweredArrows = EmpoweredMultishotArrows(talents);

        using var check = new Checks();
        check.Equal(3, empoweredArrows.Count);
        for (int k = 0; k < empoweredArrows.Count; k++) check.Ratio(ratio, empoweredArrows[k].Value / plainArrowDamage, what: $"arrow {k + 1}");
    }

    /// <summary>
    /// When both Skystrider's Supremacy and a Focused Expanse charge could empower a Multishot, the Focused Expanse charge is used and the
    /// Supremacy kept; with Fervent Supremacy, its 4 charges are all kept.
    ///
    /// The order is the game's (per the user). The older suite had the Supremacy first.
    /// </summary>
    [Theory(Skip = "The engine uses Skystrider's Supremacy first and keeps the Focused Expanse charge (SkystridersSupremacy.Connect)")]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Focused_Expanse_Charge_Empowers_A_Multishot_Before_Skystriders_Supremacy(bool fervent)
    {
        int[] talents = fervent ? [FocusedExpanse.TalentId, FerventSupremacy.TalentId] : [FocusedExpanse.TalentId];
        var f = StepFights.Elarion(talents: talents);
        f.Apply(FocusedExpanse.Buff); // as a Focused Shot would give it

        f.Cast<SkystridersSupremacy>();
        f.Cast<Multishot>();

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(FocusedExpanse.Buff));
        check.True(f.Has(SkystridersSupremacy.Buff) || f.Has(FerventSupremacy.Buff), "the Supremacy is kept");
        if (fervent) check.Equal(4, f.Stacks(FerventSupremacy.Buff));
    }

    // ---- Lunarlight Affinity

    /// <summary>With Lunarlight Affinity, Lunarlight Salvo has +20% critical strike chance.</summary>
    [Fact]
    public void Lunarlight_Affinity_Adds_20_Percent_Crit_Chance_To_Lunarlight_Salvo()
    {
        var f = StepFights.Elarion(crit: 0.15, talents: [LunarlightAffinity.TalentId]);
        f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : null); // the shot fires a salvo
        f.Apply(LunarlightMark.Debuff, stacks: 3, on: f.Enemy); // a salvo needs a marked enemy

        f.Cast<FocusedShot>();

        Assert.Equal(0.35, f.Damage("Lunarlight Salvo").First().CritChance, 1e-9);
    }

    /// <summary>
    /// With Lunarlight Affinity, the salvos Starfall Volley calls down do not take a stack of Lunarlight Mark: one stack lasts through
    /// several salvos.
    /// </summary>
    [Fact]
    public void Lunarlight_Affinity_Starfall_Volley_Salvos_Do_Not_Take_A_Stack()
    {
        var f = StepFights.Elarion(talents: [LunarlightAffinity.TalentId]);
        f.ForceProcs(key => key == LunarlightSalvo.KeptMarkProcKey ? true : null); // every arrow calls down a salvo
        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy); // a salvo needs a marked enemy

        f.Cast<StarfallVolley>();
        f.Wait(10.0); // the volley falls

        using var check = new Checks(f);
        check.True(f.Damage("Lunarlight Salvo").Count > 1);
        check.Equal(1, f.Stacks(LunarlightMark.Debuff, on: f.Enemy));
    }

    // ---- Last Lights

    /// <summary>With Last Lights, your hits have +20% critical strike chance against enemies below 30% health.</summary>
    [Theory]
    [InlineData(0.20, 0.35)]
    [InlineData(0.29, 0.35)]
    [InlineData(0.31, 0.15)]
    [InlineData(1.00, 0.15)]
    public void Last_Lights_Adds_20_Percent_Crit_Chance_Below_30_Percent_Health(double health, double critChance)
    {
        var f = StepFights.Elarion(crit: 0.15, talents: [LastLights.TalentId]);
        f.Enemy.CurrentHealth = f.Enemy.MaxHealth * health; // the enemy already wounded

        f.Cast<FocusedShot>();

        Assert.Equal(critChance, f.Damage<FocusedShot>().First().CritChance, 1e-9);
    }
}
