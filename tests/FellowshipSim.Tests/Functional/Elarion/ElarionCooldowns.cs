using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long Elarion's cooldowns last, and the talents that take time off them. Event Horizon's effects on cooldowns, and how they stack
/// with other acceleration, are in Specific/ElarionEventHorizonCooldown.cs. How any reduction works (on a ready cooldown, past zero, with
/// charges) is in CooldownStateTests.cs.
///
/// Every duration is checked by the ability being not ready 0.1s before its end and ready 0.1s after.
/// </summary>
public class ElarionCooldowns
{
    // ---- Base cooldowns

    /// <summary>
    /// Heartseeker Barrage has a 20 second cooldown, Starfall Volley 40 and Skystrider's Grace 120, counted from the cast; haste does not
    /// shorten them.
    /// </summary>
    [Theory]
    [InlineData("Heartseeker Barrage", 20.0, 0.0)]
    [InlineData("Heartseeker Barrage", 20.0, 0.3)]
    [InlineData("Starfall Volley", 40.0, 0.0)]
    [InlineData("Starfall Volley", 40.0, 0.3)]
    [InlineData("Skystrider's Grace", 120.0, 0.0)]
    [InlineData("Skystrider's Grace", 120.0, 0.3)]
    public void Each_Cooldown_Lasts_Its_Time_Whatever_Your_Haste(string ability, double cooldown, double haste)
    {
        var f = StepFights.Elarion(haste: haste);

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    // ---- Highwind Arrow's charges (with Event Horizon's acceleration, in Specific/ElarionEventHorizonCooldown.cs)

    /// <summary>
    /// A Highwind Arrow charge (of 3) recharges with your haste, at 1 + haste times the normal speed: it takes 15 / (1 + haste) seconds to
    /// come back.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.2)]
    [InlineData(0.3)]
    public void Highwind_Arrow_Charges_Recharge_In_15_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Elarion(haste: haste);

        f.Cast<HighwindArrow>();
        double end = f.CompletedAt("Highwind Arrow") + 15.0 / (1 + haste);

        using var check = new Checks(f);
        check.Equal(1 + haste, f.RecoveryRate<HighwindArrow>());
        f.WaitUntil(end - 0.1);
        check.Equal(2, f.Charges<HighwindArrow>());
        f.WaitUntil(end + 0.1);
        check.Equal(3, f.Charges<HighwindArrow>());
    }

    // ---- Skylit Grace: +120% Cooldown Acceleration to Skystrider's Grace while Starfall Volley is active

    /// <summary>
    /// With Skylit Grace, each Starfall Volley falling (8 seconds) during Skystrider's Grace's 120 second cooldown takes 8 x 1.2 = 9.6
    /// seconds off it, whether the volleys come one after another or fall together.
    ///
    /// The engine adds the volleys up, "confirmed in game" (SkylitGrace.cs). Overlapping volleys come from resetting Starfall Volley's
    /// cooldown between casts.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void Skylit_Grace_Takes_9_6_Seconds_Off_Skystriders_Grace_Per_Volley(int volleys, bool overlapping)
    {
        var f = StepFights.Elarion(talents: [SkylitGrace.TalentId]);

        f.Cast<SkystridersGrace>();
        for (int i = 0; i < volleys; i++)
        {
            if (i > 0 && overlapping) f.ResetCooldown<StarfallVolley>(); // ready again at once
            else if (i > 0) f.WaitUntil(f.CompletedAt("Starfall Volley") + 40.1); // the last volley's cooldown is over
            f.Cast<StarfallVolley>();
        }
        double end = f.CompletedAt("Skystrider's Grace") + 120.0 - volleys * 8.0 * 1.2;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<SkystridersGrace>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<SkystridersGrace>());
    }

    // ---- Skyward Munitions: 1 second off Highwind Arrow and Heartseeker Barrage per Celestial Shot or Multishot

    /// <summary>
    /// With Skyward Munitions, each Celestial Shot or Multishot you cast takes 1 second off the cooldowns of Highwind Arrow (15 seconds a
    /// charge, of 3) and Heartseeker Barrage (20 seconds).
    ///
    /// The tooltip's rule, which the game follows (per the user).
    /// </summary>
    [Theory]
    [InlineData("Celestial Shot")]
    [InlineData("Multishot")]
    public void Skyward_Munitions_Takes_1_Second_Off_Highwind_Arrow_And_Heartseeker_Barrage(string spender)
    {
        var f = StepFights.Elarion(talents: [SkywardMunitions.TalentId]);
        f.ForceProcs(key => key == CelestialShot.ImpetusProcKey ? false : null); // Celestial Impetus would reset the barrage

        f.Cast<HighwindArrow>();
        f.Cast<HeartseekerBarrage>();

        f.Cast(spender);
        double arrowEnd = f.CompletedAt("Highwind Arrow") + 15.0 - 1.0;
        double barrageEnd = f.CompletedAt("Heartseeker Barrage") + 20.0 - 1.0;

        using var check = new Checks(f);
        f.WaitUntil(arrowEnd - 0.1);
        check.Equal(2, f.Charges<HighwindArrow>());
        f.WaitUntil(arrowEnd + 0.1);
        check.Equal(3, f.Charges<HighwindArrow>());
        f.WaitUntil(barrageEnd - 0.1);
        check.False(f.IsReady<HeartseekerBarrage>());
        f.WaitUntil(barrageEnd + 0.1);
        check.True(f.IsReady<HeartseekerBarrage>());
    }

    // ---- Fervent Supremacy: 10 seconds off Skystrider's Supremacy (its charges and damage are in ElarionTalents.cs)

    /// <summary>Skystrider's Supremacy has a 45 second cooldown, counted from its cast; with Fervent Supremacy, 10 seconds less: 35.</summary>
    [Theory]
    [InlineData(false, 45.0)]
    [InlineData(true, 35.0)]
    public void Skystriders_Supremacy_Has_A_45_Second_Cooldown_35_With_Fervent_Supremacy(bool fervent, double cooldown)
    {
        var f = StepFights.Elarion(talents: fervent ? [FerventSupremacy.TalentId] : []);

        f.Cast<SkystridersSupremacy>();
        double end = f.CompletedAt("Skystrider's Supremacy") + cooldown;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<SkystridersSupremacy>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<SkystridersSupremacy>());
    }

    // ---- Repeating Stars: time off Starfall Volley per Multishot

    /// <summary>
    /// With Repeating Stars, each Multishot takes 1.5 seconds off Starfall Volley's 40 second cooldown, however many enemies it hits.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Repeating_Stars_Takes_1_5_Seconds_Off_Starfall_Volley_Per_Multishot(int enemies)
    {
        var f = StepFights.Elarion(enemies: enemies, talents: [RepeatingStars.TalentId]);

        f.Cast<StarfallVolley>();

        f.Cast<Multishot>();
        double end = f.CompletedAt("Starfall Volley") + 40.0 - 1.5;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<StarfallVolley>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<StarfallVolley>());
    }

    /// <summary>With Repeating Stars, an Empowered Multishot takes 3 seconds off Starfall Volley's cooldown instead of 1.5.</summary>
    [Fact]
    public void Repeating_Stars_Takes_3_Seconds_Off_Starfall_Volley_Per_Empowered_Multishot()
    {
        var f = StepFights.Elarion(talents: [RepeatingStars.TalentId]);

        f.Cast<StarfallVolley>();
        f.Cast<SkystridersSupremacy>(); // empowers the next Multishot

        f.Cast<Multishot>();
        double end = f.CompletedAt("Starfall Volley") + 40.0 - 3.0;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady<StarfallVolley>());
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady<StarfallVolley>());
    }
}
