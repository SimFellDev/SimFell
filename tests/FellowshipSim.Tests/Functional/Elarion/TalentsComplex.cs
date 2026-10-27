using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's talents working together, or with several abilities: the Empowered Multishot sources (Skystrider's Supremacy, Fervent
/// Supremacy, Focused Expanse), Resurgent Winds, and Final Crescendo.
/// </summary>
public class ElarionTalentsComplex
{
    private const double Ratio = 0.001;

    // ---- Empowered Multishot

    /// <summary>
    /// Player: with Fervent Supremacy, Skystrider's Supremacy gives 4 Empowered Multishots, one used up by each Multishot, and the
    /// buff is gone after the fourth.
    /// Dev: Fervent Supremacy taken; Skystrider's Supremacy cast; four Multishots; the buff has 3, 2, 1, 0 stacks after them.
    /// </summary>
    [Fact]
    public void Fervent_Supremacy_Charges_Are_Used_Up_One_Per_Multishot()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.FerventSupremacy.TalentId]);
        f.Cast<SkystridersSupremacy>();
        Assert.Equal(4, f.Hero.Auras.GetStacks(ElarionTalents.FerventSupremacy.Buff, f.Now));

        foreach (int left in new[] { 3, 2, 1, 0 })
        {
            f.Cast<Multishot>();
            Assert.Equal(left, f.Hero.Auras.GetStacks(ElarionTalents.FerventSupremacy.Buff, f.Now));
        }
    }

    /// <summary>
    /// Player: an Empowered Multishot fires at least 3 arrows, even at a single enemy.
    /// Dev: Skystrider's Supremacy cast; one Multishot at one enemy; 3 hits, equal; the next Multishot (no longer empowered after the
    /// 4 seconds) has 1 hit.
    /// </summary>
    [Fact]
    public void An_Empowered_Multishot_Fires_At_Least_3_Arrows()
    {
        var f = StepFights.Elarion();
        f.Cast<SkystridersSupremacy>();

        f.Cast<Multishot>();
        Assert.Equal(3, f.Damage("Multishot").Count);
        f.Wait(5);
        f.Cast<Multishot>();

        Assert.Equal(4, f.Damage("Multishot").Count);
    }

    /// <summary>
    /// Player: when you have both Skystrider's Supremacy and a Focused Expanse charge, the Supremacy is used first and the charge is
    /// kept; the same with Fervent Supremacy's charges.
    /// Dev: Focused Expanse taken, one charge applied, and Skystrider's Supremacy cast: after a Multishot the charge is still there.
    /// Then with Fervent Supremacy taken too, the Supremacy's stacks go down (4 to 3) and the Focused Expanse charge is kept.
    /// </summary>
    [Fact]
    public void Supremacy_Is_Used_Before_A_Focused_Expanse_Charge()
    {
        var plain = StepFights.Elarion(talents: [ElarionTalents.FocusedExpanse.TalentId]);
        plain.Hero.Auras.Apply(ElarionTalents.FocusedExpanse.Buff, plain.Now);
        plain.Cast<SkystridersSupremacy>();
        plain.Cast<Multishot>();
        Assert.Equal(1, plain.Hero.Auras.GetStacks(ElarionTalents.FocusedExpanse.Buff, plain.Now));

        var fervent = StepFights.Elarion(talents: [ElarionTalents.FocusedExpanse.TalentId, ElarionTalents.FerventSupremacy.TalentId]);
        fervent.Hero.Auras.Apply(ElarionTalents.FocusedExpanse.Buff, fervent.Now);
        fervent.Cast<SkystridersSupremacy>();
        fervent.Cast<Multishot>();
        Assert.Equal(3, fervent.Hero.Auras.GetStacks(ElarionTalents.FerventSupremacy.Buff, fervent.Now));
        Assert.Equal(1, fervent.Hero.Auras.GetStacks(ElarionTalents.FocusedExpanse.Buff, fervent.Now));
    }

    /// <summary>
    /// Player: Fervent Supremacy's Empowered Multishots deal 50% more damage.
    /// Dev: a Multishot arrow with Fervent Supremacy and Skystrider's Supremacy, against a plain Multishot's; the ratio is 1.5.
    /// (The game's talent text; the Python sim's earlier game version had 25%.)
    /// </summary>
    [Fact]
    public void Fervent_Supremacy_Raises_Multishot_Damage_By_50_Percent()
    {
        var plain = StepFights.Elarion();
        plain.Cast<Multishot>();
        var f = StepFights.Elarion(talents: [ElarionTalents.FerventSupremacy.TalentId]);
        f.Cast<SkystridersSupremacy>();
        f.Cast<Multishot>();

        Assert.All(f.Damage("Multishot"), h => Assert.Equal(1.5, h.Value / plain.Damage("Multishot")[0].Value, Ratio));
    }

    /// <summary>
    /// Player: Focused Expanse makes every Empowered Multishot deal 20% more, whichever Supremacy empowers it: with Fervent Supremacy
    /// 1.5 x that.
    /// Dev: an Empowered Multishot arrow against a plain one: 1.2 with Focused Expanse and a Skystrider's Supremacy, 1.8 with
    /// Fervent Supremacy too (the two multiply).
    /// </summary>
    [Theory]
    [InlineData(false, 1.2)]
    [InlineData(true, 1.8)]
    public void Focused_Expanse_Raises_Both_Supremacies_Multishots(bool fervent, double ratio)
    {
        var plain = StepFights.Elarion();
        plain.Cast<Multishot>();
        var f = StepFights.Elarion(talents: fervent
            ? [ElarionTalents.FocusedExpanse.TalentId, ElarionTalents.FerventSupremacy.TalentId]
            : [ElarionTalents.FocusedExpanse.TalentId]);
        f.Cast<SkystridersSupremacy>();
        f.Cast<Multishot>();

        Assert.All(f.Damage("Multishot"), h => Assert.Equal(ratio, h.Value / plain.Damage("Multishot")[0].Value, Ratio));
    }

    // ---- Resurgent Winds and Final Crescendo

    /// <summary>
    /// Player: a Highwind Arrow from Resurgent Winds deals 50% more damage to a marked enemy, for that cast only.
    /// Dev: Resurgent Winds taken, its buff applied (1 stack), the enemy marked; two Highwind Arrows (procs forced off); the first deals
    /// 1.5 times what a plain arrow does on a marked enemy, the second the same as a plain arrow.
    /// </summary>
    [Fact]
    public void Resurgent_Winds_Raises_The_Next_Highwind_Arrows_Damage_By_50_Percent()
    {
        var plain = StepFights.Elarion();
        plain.ForceProcs(_ => false);
        plain.Enemy.Auras.Apply(LunarlightMark.Debuff, plain.Now, stacks: 5);
        plain.Cast<HighwindArrow>();
        double reference = plain.Damage("Highwind Arrow")[0].Value;

        var f = StepFights.Elarion(talents: [ElarionTalents.ResurgentWinds.TalentId]);
        f.ForceProcs(_ => false);
        f.Enemy.Auras.Apply(LunarlightMark.Debuff, f.Now, stacks: 5);
        f.Hero.Auras.Apply(ElarionTalents.ResurgentWinds.Buff, f.Now);
        f.Cast<HighwindArrow>();
        f.Cast<HighwindArrow>();

        var hits = f.Damage("Highwind Arrow");
        Assert.Equal(1.5, hits[0].Value / reference, Ratio);
        Assert.Equal(1.0, hits[1].Value / reference, Ratio);
    }

    /// <summary>
    /// Player: Resurgent Winds' 50% and Final Crescendo's +100% multiply: 3 times a plain arrow.
    /// Dev: both talents; Final Crescendo's stacks and Resurgent Winds applied; one Highwind Arrow on a marked enemy; the first hit is 3
    /// times a plain arrow's.
    /// Final Crescendo comes on the 3rd cast (combat logs; the Python sim's earlier game version had the 4th).
    /// </summary>
    [Fact]
    public void Resurgent_Winds_And_Final_Crescendo_Multiply()
    {
        var plain = StepFights.Elarion();
        plain.ForceProcs(_ => false);
        plain.Enemy.Auras.Apply(LunarlightMark.Debuff, plain.Now, stacks: 5);
        plain.Cast<HighwindArrow>();

        var f = StepFights.Elarion(talents: [ElarionTalents.ResurgentWinds.TalentId, ElarionTalents.FinalCrescendo.TalentId]);
        f.ForceProcs(_ => false);
        f.Enemy.Auras.Apply(LunarlightMark.Debuff, f.Now, stacks: 5);
        for (int i = 0; i < 2; i++)
        {
            f.Cast<HighwindArrow>();
            f.Hero.CooldownOf(f.Ability<HighwindArrow>()).ResetAll();
        }
        f.Hero.Auras.Apply(ElarionTalents.ResurgentWinds.Buff, f.Now);
        f.Cast<HighwindArrow>();

        Assert.Equal(3.0, f.Damage("Highwind Arrow").Last().Value / plain.Damage("Highwind Arrow")[0].Value, Ratio);
    }

    /// <summary>
    /// Player: with Final Crescendo, every 3rd Highwind Arrow deals double damage and ricochets to 8 more enemies: 9 hits; the first
    /// two have 3 hits each (the target and 2 ricochets).
    /// Dev: Final Crescendo taken; 10 enemies; three Highwind Arrows (the charges reset between); casts 1 and 2 have 3 hits each, the
    /// 3rd has 9, the first of them double a normal first hit. (Combat logs: fXyzwGcQkTrLD34R fight 4; the Python sim's earlier game
    /// version had every 4th, with 7 ricochets.)
    /// </summary>
    [Fact]
    public void Final_Crescendo_Empowers_Every_3rd_Highwind_Arrow()
    {
        var f = StepFights.Elarion(enemies: 10, talents: [ElarionTalents.FinalCrescendo.TalentId]);
        var counts = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            int before = f.Damage("Highwind Arrow").Count;
            f.Cast<HighwindArrow>();
            f.Wait(1);
            counts.Add(f.Damage("Highwind Arrow").Count - before);
            f.Hero.CooldownOf(f.Ability<HighwindArrow>()).ResetAll();
        }

        Assert.Equal([3, 3, 9], counts);
        var hits = f.Damage("Highwind Arrow");
        Assert.Equal(2.0, hits[6].Value / hits[0].Value, Ratio);
    }

    /// <summary>
    /// Player: after the empowered Highwind Arrow, Final Crescendo starts again from nothing.
    /// Dev: three Highwind Arrows (the 3rd being the empowered one); the buff has 0, 1 and 2 stacks before them, and 0 after the 3rd.
    /// </summary>
    [Fact]
    public void Final_Crescendo_Stacks_Reset_After_The_Empowered_Arrow()
    {
        var f = StepFights.Elarion(enemies: 10, talents: [ElarionTalents.FinalCrescendo.TalentId]);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(i, f.Hero.Auras.GetStacks(ElarionTalents.FinalCrescendo.Buff, f.Now));
            f.Cast<HighwindArrow>();
            f.Hero.CooldownOf(f.Ability<HighwindArrow>()).ResetAll();
        }

        Assert.Equal(0, f.Hero.Auras.GetStacks(ElarionTalents.FinalCrescendo.Buff, f.Now));
    }
}
