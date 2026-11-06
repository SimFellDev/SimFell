using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's Focus: how it regenerates, what builds it, what each ability costs, and what makes a cast cheaper or free. What a Spirit
/// Refund gives back is in Generic/SpiritRefund.cs, and Event Horizon's 100 Spirit Points in Generic/Ultimate.cs.
///
/// Costs are read from the Focus spent, not from the bar, since Focus keeps regenerating while a cast goes on. The net Focus of a cast is
/// read from the bar once the hero is free again: its gain or cost plus the Focus regenerated while he was busy.
/// </summary>
public class ElarionResourceEconomy
{
    /// <summary>Times are whole milliseconds, which moves a hasted cast's regeneration by a few thousandths of Focus.</summary>
    private const double NetTolerance = 0.01;

    /// <summary>Focus now. The engine adds regenerated Focus every 0.1 seconds, so the bar alone can be up to 0.1 seconds behind.</summary>
    private static double Focus(StepDriver f) => f.ElarionOf().FocusAt(f.Now);

    /// <summary>Sets Focus now, once the Focus regenerated so far is counted, so it is not added on top later.</summary>
    private static void SetFocus(StepDriver f, double focus)
    {
        f.ElarionOf().RegenerateFocus(f.Now);
        f.Set(Elarion.Focus, focus);
    }

    /// <summary>Event Horizon cast, with Focus back to full (the cast's own regeneration does not count).</summary>
    private static StepDriver UnderEventHorizon(double haste)
    {
        var f = StepFights.Elarion(haste: haste);
        f.ForceProcs(key => key == CelestialShot.ImpetusProcKey ? false : null); // no free Celestial Shot
        f.Set(Hero.SpiritPoints, 100); // enough to cast it
        f.Cast<EventHorizon>();
        SetFocus(f, 100);
        return f;
    }

    // ---- Regeneration and cap

    /// <summary>Focus regenerates at 5 per second, raised by haste: 50% haste is 7.5 per second.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Focus_Regenerates_5_Per_Second_Raised_By_Haste(double haste)
    {
        var f = StepFights.Elarion(haste: haste);
        SetFocus(f, 0); // Elarion starts full

        f.Wait(4.0);

        Assert.Equal(4.0 * 5 * (1 + haste), Focus(f), 1e-9);
    }

    /// <summary>Focus regeneration stops at 100.</summary>
    [Fact]
    public void Focus_Regeneration_Stops_At_100()
    {
        var f = StepFights.Elarion();
        SetFocus(f, 95);

        f.Wait(10.0);

        Assert.Equal(100, Focus(f), 1e-9);
    }

    /// <summary>Focus never goes above 100: a Focused Shot near the cap only fills the bar, and the rest is lost.</summary>
    [Fact]
    public void Focus_Gains_Stop_At_100()
    {
        var f = StepFights.Elarion();
        SetFocus(f, 90);

        f.Cast<FocusedShot>(); // 1.5 seconds of regeneration, then 20 Focus

        using var check = new Checks(f);
        check.Equal(100, Focus(f));
        check.Equal(90 + 5 * 1.5 + 20 - 100, f.Overflows("Focus").Single().Value);
    }

    /// <summary>You cannot cast what you cannot pay: a Celestial Shot with 10 Focus is refused, and you keep your Focus.</summary>
    [Fact]
    public void An_Ability_Cannot_Be_Cast_Without_Enough_Focus()
    {
        var f = StepFights.Elarion();
        SetFocus(f, 10); // Celestial Shot costs 15

        var attempt = f.TryCast<CelestialShot>();

        using var check = new Checks(f);
        check.False(attempt.Succeeded);
        check.Equal(10, Focus(f));
        check.Equal(0, f.Seconds());
    }

    // ---- Focused Shot

    /// <summary>A Focused Shot gives 20 Focus when its cast completes, not when it starts.</summary>
    [Fact]
    public void Focused_Shot_Gives_20_Focus_When_The_Cast_Completes()
    {
        var f = StepFights.Elarion();
        SetFocus(f, 0); // room for the gain

        f.Cast<FocusedShot>();

        var gain = f.Gains("Focus").Single();

        using var check = new Checks(f);
        check.Equal(20, gain.Value);
        check.Seconds(f.CompletedAt("Focused Shot"), gain.Time.TotalSeconds);
    }

    /// <summary>
    /// A Focused Shot gives 20 Focus, and Focus keeps regenerating while you cast it: from an empty bar you have 27.5 once it is done,
    /// whatever your haste.
    ///
    /// The regeneration during the cast is 5 x (1 + haste) per second for 1.5 / (1 + haste) seconds: 7.5 at every haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Focused_Shot_Nets_27_5_Focus(double haste)
    {
        var f = StepFights.Elarion(haste: haste);
        f.ForceProcs(key => key == CelestialShot.ImpetusProcKey ? false : null); // no free Celestial Shot
        SetFocus(f, 0); // room for the gain

        f.Cast<FocusedShot>();

        Assert.Equal(20 + 5 * 1.5, Focus(f), NetTolerance);
    }

    // ---- Costs

    /// <summary>Celestial Shot costs 15 Focus, Multishot 20, and Highwind Arrow, Heartseeker Barrage and Starfall Volley 30.</summary>
    [Theory]
    [InlineData("Celestial Shot", 15.0)]
    [InlineData("Multishot", 20.0)]
    [InlineData("Highwind Arrow", 30.0)]
    [InlineData("Heartseeker Barrage", 30.0)]
    [InlineData("Starfall Volley", 30.0)]
    public void Each_Spender_Costs_Its_Focus(string ability, double cost)
    {
        var f = StepFights.Elarion();

        f.Cast(ability);

        Assert.Equal(cost, f.Spends("Focus").Single().Value);
    }

    /// <summary>
    /// Highwind Arrow's 30 Focus is paid when its 2 second cast completes, not when it starts.
    ///
    /// The engine pays every cost as the cast completes (ElarionAbility.Pay, called from Execute), as the game does (per the user).
    /// </summary>
    [Fact]
    public void Highwind_Arrow_Costs_Its_Focus_When_The_Cast_Completes()
    {
        var f = StepFights.Elarion();

        f.Cast<HighwindArrow>();

        AssertSeconds(f.CompletedAt("Highwind Arrow"), f.Spends("Focus").Single().Time.TotalSeconds);
    }

    /// <summary>
    /// From a full bar, a spender costs its Focus less the 7.5 Focus you regenerate during its global cooldown, whatever your haste:
    /// Celestial Shot 7.5 net, Multishot 12.5, Starfall Volley 22.5.
    ///
    /// The global cooldown is 1.5 / (1 + haste) seconds, and Focus regenerates at 5 x (1 + haste) per second.
    /// </summary>
    [Theory]
    [InlineData("Celestial Shot", 15.0, 0.0)]
    [InlineData("Celestial Shot", 15.0, 0.2)]
    [InlineData("Celestial Shot", 15.0, 0.5)]
    [InlineData("Multishot", 20.0, 0.0)]
    [InlineData("Multishot", 20.0, 0.2)]
    [InlineData("Multishot", 20.0, 0.5)]
    [InlineData("Starfall Volley", 30.0, 0.0)]
    [InlineData("Starfall Volley", 30.0, 0.2)]
    [InlineData("Starfall Volley", 30.0, 0.5)]
    public void An_Instant_Spender_Costs_Its_Focus_Less_7_5_Net(string ability, double cost, double haste)
    {
        var f = StepFights.Elarion(haste: haste);

        f.Cast(ability);

        Assert.Equal(100 - cost + 5 * 1.5, Focus(f), NetTolerance);
    }

    /// <summary>
    /// Highwind Arrow's 30 Focus is paid when its 2 second cast completes, so the Focus you regenerate meanwhile (10) is lost if you were
    /// already full: from 100 Focus you are at 70 once it is done, from 80 at 60.
    ///
    /// The regeneration during the hasted cast is 5 x (1 + haste) per second for 2 / (1 + haste) seconds: 10 at every haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Highwind_Arrow_From_Full_Focus_Loses_The_Focus_Regenerated_During_Its_Cast(double haste)
    {
        var fullScenario = StepFights.Elarion(haste: haste);

        fullScenario.Cast<HighwindArrow>();

        var lowScenario = StepFights.Elarion(haste: haste);
        SetFocus(lowScenario, 80);

        lowScenario.Cast<HighwindArrow>();

        using var check = new Checks();
        check.Equal(100 - 30, Focus(fullScenario), NetTolerance);
        check.Equal(80 + 5 * 2.0 - 30, Focus(lowScenario), NetTolerance);
    }

    /// <summary>
    /// Heartseeker Barrage costs 30 Focus and channels for 2 seconds whatever your haste, so unlike the other casts the Focus you
    /// regenerate meanwhile grows with haste: from a full bar you are at 100 - 30 + 10 x (1 + haste) once it is done.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Heartseeker_Barrage_Costs_30_Focus_And_Regenerates_With_Haste(double haste)
    {
        var f = StepFights.Elarion(haste: haste);

        f.Cast<HeartseekerBarrage>();

        Assert.Equal(100 - 30 + 5 * (1 + haste) * 2.0, Focus(f), NetTolerance);
    }

    // ---- Free casts

    /// <summary>
    /// Celestial Impetus makes your next Celestial Shot free: a Celestial Shot with one charge costs no Focus, and the one after it
    /// costs 15 again.
    /// </summary>
    [Fact]
    public void Celestial_Impetus_Makes_The_Next_Celestial_Shot_Free()
    {
        var f = StepFights.Elarion();
        f.Apply(CelestialShot.Impetus); // as a Focused Shot's hit would give it

        f.Cast<CelestialShot>();
        f.Cast<CelestialShot>();

        using var check = new Checks(f);
        check.Equal(2, f.Damage<CelestialShot>().Count, what: "Celestial Shots");
        check.Equal(1, f.Spends("Focus").Count, what: "Focus spends");
        check.Equal(15, f.Spends("Focus").Single().Value);
    }

    /// <summary>
    /// With Resurgent Winds up, your next Highwind Arrow costs no Focus; the one after it costs 30 again.
    ///
    /// Its instant cast is in ElarionCastTimes.cs, its damage in ElarionTalents.cs.
    /// </summary>
    [Fact]
    public void Resurgent_Winds_Makes_The_Next_Highwind_Arrow_Free()
    {
        var f = StepFights.Elarion(talents: [ResurgentWinds.TalentId]);
        f.ForceProcs(key => key == ResurgentWinds.ProcKey ? false : null); // no new Resurgent Winds for the second arrow
        f.Apply(ResurgentWinds.Buff); // as Lunarlight Mark would give it

        f.Cast<HighwindArrow>();
        f.Cast<HighwindArrow>();

        using var check = new Checks(f);
        check.Equal(2, f.CastsCompleted("Highwind Arrow").Count, what: "Highwind Arrows");
        check.Equal(1, f.Spends("Focus").Count, what: "Focus spends");
        check.Equal(30, f.Spends("Focus").Single().Value);
    }

    // ---- Empowered Multishot

    /// <summary>
    /// An Empowered Multishot costs 50% less Focus, 10 instead of 20, whatever empowers it, and the cheaper cost ends with the
    /// empowerment: Skystrider's Supremacy empowers the Multishots of the next 4 seconds (3 at 0% haste), Fervent Supremacy 4 Multishots,
    /// and Focused Expanse's 2 charges 2.
    ///
    /// Combat logs show an Empowered Multishot spending 10 Focus (NOTES.md, fight 23 @ 39.10).
    /// </summary>
    [Theory]
    [InlineData("Skystrider's Supremacy", 3)]
    [InlineData("Fervent Supremacy", 4)]
    [InlineData("Focused Expanse", 2)]
    public void An_Empowered_Multishot_Costs_10_Focus_Instead_Of_20(string empowerment, int empoweredMultishots)
    {
        var f = StepFights.Elarion(talents: empowerment switch
        {
            "Fervent Supremacy" => [FerventSupremacy.TalentId],
            "Focused Expanse" => [FocusedExpanse.TalentId],
            _ => [],
        });

        if (empowerment == "Focused Expanse") f.Apply(FocusedExpanse.Buff, stacks: 2); // its 2 charges, as Focused Shots would give them
        else f.Cast<SkystridersSupremacy>();
        for (int i = 0; i < empoweredMultishots + 1; i++) f.Cast<Multishot>(); // the last one is plain

        double[] costs = [.. Enumerable.Repeat(10.0, empoweredMultishots), 20.0];
        Assert.Equal(costs, f.Spends("Focus").Select(spend => spend.Value));
    }

    // ---- Event Horizon (its 100 Spirit Points are in Generic/Ultimate.cs, its effect on cooldowns in Specific/ElarionEventHorizonCooldown.cs)

    /// <summary>
    /// While Event Horizon is up, every Focus cost is halved, without rounding: Celestial Shot costs 7.5.
    ///
    /// The engine does not round (ElarionAbility.CostAt), as the game does not (per the user); the log keeps Focus in hundredths
    /// (NOTES.md).
    /// </summary>
    [Theory]
    [InlineData("Celestial Shot", 7.5)]
    [InlineData("Multishot", 10.0)]
    [InlineData("Highwind Arrow", 15.0)]
    [InlineData("Heartseeker Barrage", 15.0)]
    [InlineData("Starfall Volley", 15.0)]
    public void Event_Horizon_Halves_Every_Focus_Cost(string ability, double cost)
    {
        var f = StepFights.Elarion();
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();

        f.Cast(ability);

        Assert.Equal(cost, f.Spends("Focus").Single().Value);
    }

    /// <summary>The halved costs end with Event Horizon's 20 seconds: a Celestial Shot costs 7.5 Focus just before, 15 after.</summary>
    [Fact]
    public void Event_Horizon_Halved_Costs_End_After_20_Seconds()
    {
        var f = StepFights.Elarion();
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();
        double end = f.CompletedAt("Event Horizon") + 20.0;

        f.WaitUntil(end - 0.1);
        f.Cast<CelestialShot>();
        f.Cast<CelestialShot>(); // after the first one's global cooldown, past the end

        Assert.Equal([7.5, 15.0], f.Spends("Focus").Select(spend => spend.Value));
    }

    /// <summary>
    /// Event Horizon's 50% and an Empowered Multishot's 50% multiply: an Empowered Multishot under Event Horizon costs 5 Focus, a quarter
    /// of 20, and you regenerate 7.5 during its global cooldown: from 50 Focus you are at 52.5, whatever your haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void An_Empowered_Multishot_Costs_5_Focus_Under_Event_Horizon(double haste)
    {
        var f = UnderEventHorizon(haste);

        f.Cast<SkystridersSupremacy>(); // empowers the next Multishot
        SetFocus(f, 50); // not full, so the cap does not hide the regeneration

        f.Cast<Multishot>();

        using var check = new Checks(f);
        check.Equal(5, f.Spends("Focus").Single().Value);
        check.Equal(50 - 5 + 7.5, Focus(f), NetTolerance);
    }

    /// <summary>
    /// Under Event Horizon a spender costs half its Focus, less the 7.5 Focus you regenerate during its global cooldown, whatever your
    /// haste: Celestial Shot 0 net (from 50 Focus, so the cap does not hide the regeneration), Multishot 2.5 net, Starfall Volley 7.5.
    /// </summary>
    [Theory]
    [InlineData("Celestial Shot", 50.0, 7.5, 0.0)]
    [InlineData("Celestial Shot", 50.0, 7.5, 0.2)]
    [InlineData("Celestial Shot", 50.0, 7.5, 0.5)]
    [InlineData("Multishot", 100.0, 10.0, 0.0)]
    [InlineData("Multishot", 100.0, 10.0, 0.2)]
    [InlineData("Multishot", 100.0, 10.0, 0.5)]
    [InlineData("Starfall Volley", 100.0, 15.0, 0.0)]
    [InlineData("Starfall Volley", 100.0, 15.0, 0.2)]
    [InlineData("Starfall Volley", 100.0, 15.0, 0.5)]
    public void A_Spender_Under_Event_Horizon_Costs_Half_Its_Focus_Less_7_5_Net(string ability, double focus, double halvedCost, double haste)
    {
        var f = UnderEventHorizon(haste);
        SetFocus(f, focus);

        f.Cast(ability);

        Assert.Equal(focus - halvedCost + 5 * 1.5, Focus(f), NetTolerance);
    }
}
