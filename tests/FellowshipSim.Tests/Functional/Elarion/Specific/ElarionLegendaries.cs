using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's legendary items: Starstriker's Ascent (his hauberk), Shimmer (his helm) and Astronomer's Hail (his leggings), each worn on a
/// bare Elarion. The Strand of Eternity every legendary item carries is in ElarionEventHorizonCooldown.cs, and Impending Heartseeker's
/// own rules (its damage, its 15 seconds) in ElarionTalents.cs.
/// </summary>
public class ElarionLegendaries
{
    // ---- Starstriker's Ascent

    /// <summary>
    /// With Starstriker's Ascent, a Spirit Refund has a 50% chance to reset Heartseeker Barrage's cooldown and give you Impending
    /// Heartseeker; when the chance fails, neither happens.
    ///
    /// The refund is the barrage's own, so the barrage is on cooldown when the legendary rolls.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Spirit_Refund_Has_A_50_Percent_Chance_To_Reset_The_Barrage_And_Give_Impending_Heartseeker(bool proc)
    {
        var f = StepFights.Elarion(legendaries: [StarstrikersAscent.ChestItemId]);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true // the barrage's Focus refunded
            : key == StarstrikersAscent.ProcKey ? proc
            : null);

        f.Cast<HeartseekerBarrage>();

        using var check = new Checks(f);
        check.True(procs.ChancesOf(StarstrikersAscent.ProcKey).SequenceEqual([0.5]), "one roll at 50%");
        check.True(f.IsReady<HeartseekerBarrage>() == proc, proc ? "the barrage is ready" : "the barrage is on cooldown");
        check.True(f.Has(ImpendingHeartseeker.Buff) == proc, proc ? "Impending Heartseeker is up" : "no Impending Heartseeker");
    }

    /// <summary>
    /// The Impending Heartseeker a barrage's own Spirit Refund gives is left for the next barrage, and chained refunds keep it going: of
    /// three barrages, the first deals the same damage with every arrow, and the next two, each ready at once, ramp their arrows up.
    ///
    /// From the combat logs (pWmcV42K39GHTLQX fight 23 @ 15.192, HeartseekerBarrage.cs). Arrow k (from 0) of an empowered barrage deals
    /// 1 + 0.1 x (k + 1) times a plain arrow (Impending Heartseeker's rule, ElarionTalents.cs), so 1 + 0.1 x (k + 1) over 1.1 times its
    /// barrage's first arrow; barrages are not compared with each other, since Rising Spirit raises the main stat after each refund.
    /// Without haste a barrage fires 10 arrows.
    /// </summary>
    [Fact]
    public void Chained_Refunds_Keep_Impending_Heartseeker_Going_On_Barrages()
    {
        var f = StepFights.Elarion(spirit: 0.25, legendaries: [StarstrikersAscent.ChestItemId]).WithoutDamageSpread();
        f.ForceProcs(key => key == Hero.SpiritRefundProc || key == StarstrikersAscent.ProcKey); // every other proc off

        for (int i = 0; i < 3; i++) f.Cast<HeartseekerBarrage>(); // each ready again: the refund reset it
        f.Wait(0.5); // the last arrows land

        var arrows = f.Damage<HeartseekerBarrage>();

        using var check = new Checks(f);
        check.Equal(30, arrows.Count);
        for (int k = 1; k < 10; k++) check.Ratio(1.0, arrows[k].Value / arrows[0].Value, what: $"first barrage, arrow {k + 1}");
        for (int barrage = 1; barrage < 3; barrage++)
        for (int k = 1; k < 10; k++)
            check.Ratio((1 + 0.1 * (k + 1)) / 1.1, arrows[barrage * 10 + k].Value / arrows[barrage * 10].Value,
                what: $"barrage {barrage + 1}, arrow {k + 1}");
    }

    // ---- Shimmer

    /// <summary>
    /// With Shimmer, an enemy your Highwind Arrow hits is afflicted by Shimmer for 9 seconds.
    /// </summary>
    [Fact]
    public void Highwind_Arrow_Afflicts_Its_Target_With_Shimmer_For_9_Seconds()
    {
        var f = StepFights.Elarion(legendaries: [Shimmer.HeadItemId]);

        f.Cast<HighwindArrow>();
        double end = f.Damage<HighwindArrow>().Single().Time.TotalSeconds + 9.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(Shimmer.Debuff, on: f.Enemy));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(Shimmer.Debuff, on: f.Enemy));
    }

    /// <summary>
    /// Highwind Arrow's ricochets afflict the enemies they hit with Shimmer too: an arrow at a group of 4 leaves Shimmer on its target and
    /// its 2 ricochets' enemies, and none on the fourth.
    /// </summary>
    [Fact]
    public void Highwind_Arrow_Ricochets_Afflict_Their_Enemies_With_Shimmer()
    {
        var f = StepFights.Elarion(enemies: 4, legendaries: [Shimmer.HeadItemId]);

        f.Cast<HighwindArrow>();
        f.Wait(0.5); // the ricochets land

        var shimmered = f.Enemies().Where(enemy => f.Has(Shimmer.Debuff, on: enemy)).ToList();

        using var check = new Checks(f);
        check.Equal(3, f.Damage<HighwindArrow>().Count, what: "hits");
        check.Equal(3, shimmered.Count, what: "enemies with Shimmer");
        check.True(shimmered.Contains(f.Enemy), "the target has Shimmer");
    }

    /// <summary>
    /// A new Shimmer on an enemy that has it adds a stack and restarts the 9 seconds: a Highwind Arrow 5 seconds after the first leaves 2
    /// stacks, which last 9 seconds from the second arrow.
    /// </summary>
    [Fact]
    public void A_New_Shimmer_Adds_A_Stack_And_Restarts_The_9_Seconds()
    {
        var f = StepFights.Elarion(legendaries: [Shimmer.HeadItemId]);

        f.Cast<HighwindArrow>();
        f.WaitUntil(5.0); // the first Shimmer still up
        f.Cast<HighwindArrow>(); // its second charge
        double end = f.Damage<HighwindArrow>().Last().Time.TotalSeconds + 9.0;

        f.WaitUntil(end - 0.1);
        Assert.Equal(2, f.Stacks(Shimmer.Debuff, on: f.Enemy));
        f.WaitUntil(end + 0.1);
        Assert.Equal(0, f.Stacks(Shimmer.Debuff, on: f.Enemy));
    }

    /// <summary>
    /// Each Shimmer stack makes you deal 8% more damage to the enemy, up to 3 stacks: 5 Highwind Arrows in a row deal 1, 1.08, 1.16, 1.24
    /// and 1.24 times the first, which hits an enemy without Shimmer.
    /// </summary>
    [Fact]
    public void Each_Shimmer_Stack_Raises_Damage_By_8_Percent_Up_To_3_Stacks()
    {
        var f = StepFights.Elarion(legendaries: [Shimmer.HeadItemId]).WithoutDamageSpread();

        for (int i = 0; i < 5; i++)
        {
            f.ResetCooldown<HighwindArrow>(); // a charge for every arrow
            f.Set(Elarion.Focus, 100); // and the Focus
            f.Cast<HighwindArrow>();
        }

        var arrows = f.Damage<HighwindArrow>();
        double[] expected = [1.0, 1.08, 1.16, 1.24, 1.24];

        using var check = new Checks(f);
        check.Equal(5, arrows.Count);
        for (int k = 1; k < arrows.Count; k++) check.Ratio(expected[k], arrows[k].Value / arrows[0].Value, what: $"arrow {k + 1}");
        check.Equal(3, f.Stacks(Shimmer.Debuff, on: f.Enemy));
    }

    // ---- Astronomer's Hail

    /// <summary>Whether a Starfall Volley is falling now.</summary>
    private static bool VolleyFalling(StepDriver f) => f.Ability<StarfallVolley>().ActiveCount > 0;

    /// <summary>
    /// Astronomer's Hail makes Starfall Volley last 3 seconds longer: 11 seconds from its cast instead of 8.
    /// </summary>
    [Theory]
    [InlineData(false, 8.0)]
    [InlineData(true, 11.0)]
    public void Astronomers_Hail_Makes_Starfall_Volley_Last_3_Seconds_Longer(bool legendary, double duration)
    {
        var f = StepFights.Elarion(legendaries: legendary ? [AstronomersHail.LegsItemId] : []);

        f.Cast<StarfallVolley>();
        double end = f.CompletedAt("Starfall Volley") + duration;

        f.WaitUntil(end - 0.1);
        Assert.True(VolleyFalling(f));
        f.WaitUntil(end + 0.1);
        Assert.False(VolleyFalling(f));
    }

    /// <summary>
    /// With Astronomer's Hail, Starfall Volley deals 100% more damage to its main target: each of its strikes deals the target twice what
    /// it deals another enemy.
    /// </summary>
    [Fact]
    public void Astronomers_Hail_Doubles_Starfall_Volley_Damage_On_Its_Main_Target()
    {
        var f = StepFights.Elarion(enemies: 2, legendaries: [AstronomersHail.LegsItemId]).WithoutDamageSpread();

        f.Cast<StarfallVolley>();
        f.Wait(0.5); // the first strike lands

        var strikes = f.Damage<StarfallVolley>();
        double targetDamage = strikes.First(hit => hit.Target == f.Enemy.Name).Value;
        double otherDamage = strikes.First(hit => hit.Target != f.Enemy.Name).Value;

        AssertRatio(2.0, targetDamage / otherDamage);
    }

    /// <summary>
    /// With Astronomer's Hail, each Multishot you cast while Starfall Volley falls extends it by 0.25 seconds: with n Multishots it ends
    /// 11 + 0.25 n seconds after its cast.
    ///
    /// The Multishots are cast in the first seconds of the volley, with Focus topped up before each.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Each_Multishot_Extends_Starfall_Volley_By_A_Quarter_Of_A_Second(int multishots)
    {
        var f = StepFights.Elarion(legendaries: [AstronomersHail.LegsItemId]);

        f.Cast<StarfallVolley>();
        double end = f.CompletedAt("Starfall Volley") + 11.0 + 0.25 * multishots;
        for (int i = 0; i < multishots; i++)
        {
            f.Set(Elarion.Focus, 100); // enough to cast it
            f.Cast<Multishot>();
        }

        f.WaitUntil(end - 0.1);
        Assert.True(VolleyFalling(f));
        f.WaitUntil(end + 0.1);
        Assert.False(VolleyFalling(f));
    }
}
