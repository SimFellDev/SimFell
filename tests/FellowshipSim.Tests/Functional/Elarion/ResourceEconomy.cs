using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's Focus and Spirit Point economy: what builds it, what spends it, how it regenerates, and what Event Horizon
/// and a few talents change. Each test has a "Player" line (what it checks, in game terms) and a "Dev" line (how it is done).
/// Dev: the net Focus of a cast is read once <c>Cast</c> returns, i.e. when Elarion is free to act again, so it includes the
/// Focus regenerated while he was busy. Costs are paid when the cast completes (or when an instant cast starts), and
/// regeneration runs at 5 per second raised by haste, so for a cast of 1.5s / (1 + haste) the regeneration is 7.5 whatever
/// the haste.
/// Celestial Impetus (a random free Celestial Shot) is forced off wherever Focused Shots are cast.
/// </summary>
public class ElarionResourceEconomy
{
    /// <summary>Times are whole milliseconds, which moves a hasted cast's regeneration by a few thousandths.</summary>
    private const double Tolerance = 0.01;

    private static double Focus(StepDriver f) => f.ElarionOf().FocusAt(f.Now);

    private static void SetFocus(StepDriver f, double focus)
    {
        var elarion = f.ElarionOf();
        elarion.RegenerateFocus(f.Now);
        elarion.Resources.Get(Elarion.Focus).Current = focus;
    }

    private static void NoImpetus(StepDriver f) =>
        f.ForceProcs(key => key == CelestialShot.ImpetusProcKey ? false : null);

    // ---- Builders and spenders

    /// <summary>
    /// Player: Focused Shot gives 20 Focus, whatever your haste, and Focus keeps regenerating while you cast it: from an empty
    /// bar you are at 27.5 Focus once it is done.
    /// Dev: Focus emptied first (so the cap does not hide the gain); one Focused Shot at 0%, 20% and 50% haste; expects 20 + 5 x 1.5.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Focused_Shot_Nets_27_5_Focus(double haste)
    {
        var f = StepFights.Elarion(haste);
        NoImpetus(f);
        SetFocus(f, 0);

        f.Cast<FocusedShot>();

        Assert.Equal(27.5, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: Focused Shot's 20 Focus comes when the cast finishes, not when you start it.
    /// Dev: Focus emptied first; one Focused Shot at 0% haste; the single Focus gain event is at 1.5s.
    /// </summary>
    [Fact]
    public void Focused_Shot_Gives_Its_Focus_When_The_Cast_Completes()
    {
        var f = StepFights.Elarion();
        NoImpetus(f);
        SetFocus(f, 0);

        f.Cast<FocusedShot>();

        var gained = Assert.Single(f.Events, e => e.Kind == StepEventKind.ResourceGained && e.Name == "Focus");
        Assert.Equal(20, gained.Value);
        Assert.Equal(SimTime.FromSeconds(1.5), gained.Time);
    }

    /// <summary>
    /// Player: Celestial Shot costs 15 Focus; while you are busy for its global cooldown you regenerate 7.5, so it costs 7.5
    /// net, whatever your haste.
    /// Dev: starts from full Focus; one Celestial Shot at 0%, 20% and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Celestial_Shot_Costs_7_5_Focus_Net(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<CelestialShot>();

        Assert.Equal(100 - 7.5, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: Multishot costs 20 Focus, 12.5 net after the regeneration during its global cooldown.
    /// Dev: starts from full Focus; one Multishot at 0%, 20% and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Multishot_Costs_12_5_Focus_Net(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<Multishot>();

        Assert.Equal(100 - 12.5, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: Starfall Volley costs 30 Focus, 22.5 net after the regeneration during its global cooldown.
    /// Dev: starts from full Focus; one Starfall Volley at 0%, 20% and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Starfall_Volley_Costs_22_5_Focus_Net(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<StarfallVolley>();

        Assert.Equal(100 - 22.5, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: Highwind Arrow costs 30 Focus, paid when the 2 second cast finishes. The Focus you regenerate meanwhile (10) is
    /// lost if you were already full, so from a full bar it costs the whole 30; from 80 Focus it costs 20 net.
    /// Dev: one Highwind Arrow from 100 Focus, then one from 80, at 0%, 20% and 50% haste (the regeneration during a hasted cast
    /// is 5 x (1 + haste) x 2 / (1 + haste) = 10 whatever the haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Highwind_Arrow_Costs_30_Focus_At_The_End_Of_The_Cast(double haste)
    {
        var full = StepFights.Elarion(haste);
        full.Cast<HighwindArrow>();
        Assert.Equal(100 - 30, Focus(full), Tolerance);

        var low = StepFights.Elarion(haste);
        SetFocus(low, 80);
        low.Cast<HighwindArrow>();
        Assert.Equal(80 - 30 + 10, Focus(low), Tolerance);
    }

    /// <summary>
    /// Player: a Highwind Arrow made free and instant by Resurgent Winds costs no Focus, but you still regenerate during its
    /// global cooldown: from an empty bar you are at 7.5.
    /// Dev: Resurgent Winds taken and its buff applied; Focus emptied; one Highwind Arrow at 0%, 20% and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Highwind_Arrow_From_Resurgent_Winds_Costs_No_Focus(double haste)
    {
        var f = StepFights.Elarion(haste, talents: [ElarionTalents.ResurgentWinds.TalentId]);
        f.Hero.Auras.Apply(ElarionTalents.ResurgentWinds.Buff, f.Now);
        SetFocus(f, 0);

        f.Cast<HighwindArrow>();

        Assert.Equal(7.5, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: Heartseeker Barrage costs 30 Focus and channels for 2 seconds whatever your haste, so unlike the other casts the
    /// Focus you regenerate meanwhile depends on haste: 10 x (1 + haste).
    /// Dev: starts from full Focus; one barrage at 0%, 20% and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Heartseeker_Barrage_Costs_30_Focus_And_Regenerates_With_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<HeartseekerBarrage>();

        Assert.Equal(100 - 30 + 10 * (1 + haste), Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: five Celestial Shots and two Focused Shots in a row, the usual filler loop, are a net -7.5 Focus each and +27.5
    /// each: 70 Focus becomes 32.5, then 87.5.
    /// Dev: Focus set to 70; five Celestial Shots then two Focused Shots at 0% haste.
    /// </summary>
    [Fact]
    public void Five_Celestial_Shots_And_Two_Focused_Shots_Loop_Nets_17_5_Focus()
    {
        var f = StepFights.Elarion();
        NoImpetus(f);
        SetFocus(f, 70);

        for (int i = 0; i < 5; i++) f.Cast<CelestialShot>();
        Assert.Equal(32.5, Focus(f), Tolerance);

        f.Cast<FocusedShot>();
        f.Cast<FocusedShot>();
        Assert.Equal(87.5, Focus(f), Tolerance);
    }

    // ---- Empowered Multishot

    /// <summary>
    /// Player: an Empowered Multishot costs 10 Focus (half), 2.5 net. The reduced cost goes with the empowerment: once it is
    /// gone Multishot costs 12.5 net again. Skystrider's Supremacy empowers the Multishots cast in the next 4 seconds.
    /// Dev: Skystrider's Supremacy, then three Multishots at 0% haste (they start at 0, 1.5 and 3s, inside the 4 seconds):
    /// the Focus is 2.5 lower after each. After 20s (full Focus again) a fourth costs 12.5.
    /// </summary>
    [Fact]
    public void Multishot_Empowered_By_Skystriders_Supremacy_Costs_2_5_Focus_Net()
    {
        var f = StepFights.Elarion();
        f.Cast<SkystridersSupremacy>();

        for (int i = 1; i <= 3; i++)
        {
            f.Cast<Multishot>();
            Assert.Equal(100 - 2.5 * i, Focus(f), Tolerance);
        }

        f.Wait(20);
        double before = Focus(f);
        f.Cast<Multishot>();
        Assert.Equal(before - 12.5, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: with Fervent Supremacy, Skystrider's Supremacy gives 4 Empowered Multishots, each 2.5 Focus net, and the fifth
    /// Multishot is back to 12.5.
    /// Dev: Fervent Supremacy taken; Skystrider's Supremacy; four Multishots then a fifth after 20s.
    /// </summary>
    [Fact]
    public void Multishot_Empowered_By_Fervent_Supremacy_Costs_2_5_Focus_Net()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.FerventSupremacy.TalentId]);
        f.Cast<SkystridersSupremacy>();

        for (int i = 1; i <= 4; i++)
        {
            f.Cast<Multishot>();
            Assert.Equal(100 - 2.5 * i, Focus(f), Tolerance);
        }

        f.Wait(20);
        double before = Focus(f);
        f.Cast<Multishot>();
        Assert.Equal(before - 12.5, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: the Empowered Multishots Focused Expanse gives cost 2.5 Focus net each, and the next one is back to 12.5.
    /// Dev: Focused Expanse taken, its buff applied at 2 charges (instead of waiting for the 20% chance); two Multishots, then a
    /// third after 20s.
    /// </summary>
    [Fact]
    public void Multishot_Empowered_By_Focused_Expanse_Costs_2_5_Focus_Net()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.FocusedExpanse.TalentId]);
        f.Hero.Auras.Apply(ElarionTalents.FocusedExpanse.Buff, f.Now, setStacks: ElarionTalents.FocusedExpanse.MaxCharges);

        for (int i = 1; i <= 2; i++)
        {
            f.Cast<Multishot>();
            Assert.Equal(100 - 2.5 * i, Focus(f), Tolerance);
        }

        f.Wait(20);
        double before = Focus(f);
        f.Cast<Multishot>();
        Assert.Equal(before - 12.5, Focus(f), Tolerance);
    }

    // ---- Caps and floors

    /// <summary>
    /// Player: Focus never goes above 100: a Focused Shot near the cap only fills the bar.
    /// Dev: Focus set to 90; one Focused Shot; the Focus is 100 and an overflow of the extra 10 is reported, no more is gained.
    /// </summary>
    [Fact]
    public void Focus_Gain_Stops_At_100()
    {
        var f = StepFights.Elarion();
        NoImpetus(f);
        SetFocus(f, 90);

        f.Cast<FocusedShot>();

        Assert.Equal(100, Focus(f), Tolerance);
        Assert.Contains(f.Events, e => e.Kind == StepEventKind.ResourceOverflow && e.Name == "Focus");
    }

    /// <summary>
    /// Player: Focus regeneration stops at 100.
    /// Dev: Focus set to 95; 10 seconds pass.
    /// </summary>
    [Fact]
    public void Focus_Regeneration_Stops_At_100()
    {
        var f = StepFights.Elarion();
        SetFocus(f, 95);

        f.Wait(10);

        Assert.Equal(100, Focus(f), Tolerance);
    }

    /// <summary>
    /// Player: you cannot cast what you cannot pay: Celestial Shot with 10 Focus is refused and nothing is spent.
    /// Dev: Focus set to 10; a Celestial Shot attempt is refused; the Focus is still 10 and no time passed.
    /// </summary>
    [Fact]
    public void An_Ability_Cannot_Be_Cast_Without_Enough_Focus()
    {
        var f = StepFights.Elarion();
        SetFocus(f, 10);

        var attempt = f.TryCast<CelestialShot>();

        Assert.False(attempt.Succeeded);
        Assert.Equal(10, Focus(f), Tolerance);
        Assert.Equal(SimTime.Zero, f.Now);
    }

    // ---- Regeneration

    /// <summary>
    /// Player: Focus regenerates at 5 per second, and haste raises that: 50% haste is 7.5 per second.
    /// Dev: Focus emptied; 4 seconds pass at 0%, 20% and 50% haste; expects 20 x (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Focus_Regenerates_5_Per_Second_Raised_By_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);
        SetFocus(f, 0);

        f.Wait(4);

        Assert.Equal(20 * (1 + haste), Focus(f), Tolerance);
    }

    // ---- Event Horizon

    /// <summary>
    /// Player: while Event Horizon is up, every Focus cost is halved: Highwind Arrow costs 15 instead of 30, Multishot 10
    /// instead of 20.
    /// Dev: Event Horizon cast with 100 Spirit Points, then one Highwind Arrow and one Multishot at 0% haste; the cost is the
    /// Focus spent event of each cast.
    /// </summary>
    [Fact]
    public void Event_Horizon_Halves_The_Focus_Cost_Of_Abilities()
    {
        var f = StepFights.Elarion();
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
        f.Cast<EventHorizon>();

        f.Cast<HighwindArrow>();
        f.Cast<Multishot>();

        var spends = f.Events.Where(e => e.Kind == StepEventKind.ResourceSpent && e.Name == "Focus").Select(e => e.Value).ToList();
        Assert.Equal([15.0, 10.0], spends);
    }

    /// <summary>
    /// Player: Event Horizon halves Celestial Shot's 15 Focus: it is not stated whether that is 7, 8 or 7.5.
    /// Dev: asserts the Python sim's value (8: the half rounded up).
    /// TODO: open question from mechanics_to_test.md ("Focus cost of celestial shot under event horizon: 7 or 8? Floor or
    /// ceiling?"); the engine does not round (7.5). Check in game.
    /// </summary>
    [Fact(Skip = "Open question: the rounding of a halved odd cost (7, 8 or 7.5): to check in game")]
    public void Event_Horizon_Rounds_Celestial_Shots_Cost_Up_To_8()
    {
        var f = StepFights.Elarion();
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
        f.Cast<EventHorizon>();

        f.Cast<CelestialShot>();

        var spend = Assert.Single(f.Events, e => e.Kind == StepEventKind.ResourceSpent && e.Name == "Focus");
        Assert.Equal(8, spend.Value);
    }

    // ---- Spirit Points

    /// <summary>
    /// Player: you regenerate 1 Spirit Point every 3 seconds, raised by your Spirit: 10% Spirit makes it 1.1 per 3 seconds.
    /// Dev: starts from 50 Spirit Points; at 0%, 10%, 25% and 35% Spirit, 12 seconds pass; expects 50 + 12 x (1 + spirit) / 3.
    /// TODO: the engine regenerates a flat point per 3 seconds (Hero.SpiritPointRegenSeconds); the Python sim scales it with
    /// Spirit. Check in game.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.35)]
    public void Spirit_Points_Regenerate_One_Per_Three_Seconds_Raised_By_Spirit(double spirit)
    {
        var f = StepFights.Elarion(spirit: spirit);
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 50;

        f.Wait(12);

        Assert.Equal(50 + 12 * (1 + spirit) / 3, f.Hero.ResourceAmount(Hero.SpiritPoints), 1e-3);
    }
}
