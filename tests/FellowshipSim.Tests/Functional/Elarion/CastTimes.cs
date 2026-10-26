using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each Elarion ability keeps him busy: from the start of the cast until he can act again (cast or channel
/// finished and global cooldown over).
/// Dev: every test casts at the start of a fight, so the fight clock after <c>Cast</c> returns is the down time. The engine
/// rounds times to the millisecond, hence the 1ms tolerance for one cast and 1ms per cast for a sequence.
/// </summary>
public class ElarionCastTimes
{
    private const double Tolerance = 0.001;

    private static void AssertDownTime(StepDriver f, double expectedSeconds, double tolerance = Tolerance) =>
        Assert.InRange(f.Now.TotalSeconds, expectedSeconds - tolerance, expectedSeconds + tolerance);

    /// <summary>
    /// Player: Focused Shot takes 1.5 seconds to cast, shortened by haste.
    /// Dev: one Focused Shot at 0%, 20% and 50% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Focused_Shot_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<FocusedShot>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Celestial Shot is instant, so you are busy for the global cooldown: 1.5 seconds, shortened by haste.
    /// Dev: one Celestial Shot at 0%, 20% and 50% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Celestial_Shot_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<CelestialShot>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Multishot is instant, so you are busy for the global cooldown: 1.5 seconds, shortened by haste.
    /// Dev: one Multishot at 0%, 20% and 50% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Multishot_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<Multishot>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Highwind Arrow takes 2 seconds to cast, shortened by haste.
    /// Dev: one Highwind Arrow at 0%, 20% and 50% haste; expects 2.0 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Highwind_Arrow_Takes_2_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<HighwindArrow>();

        AssertDownTime(f, 2.0 / (1 + haste));
    }

    /// <summary>
    /// Player: Starfall Volley is instant, so you are busy for the global cooldown: 1.5 seconds, shortened by haste.
    /// Dev: one Starfall Volley at 0%, 20% and 50% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Starfall_Volley_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<StarfallVolley>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Heartseeker Barrage channels for 2 seconds whatever your haste: haste fits more arrows in, it does not shorten the
    /// channel.
    /// Dev: one Heartseeker Barrage at 0%, 20% and 50% haste; expects 2.0.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Heartseeker_Barrage_Takes_2_Seconds_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);

        f.Cast<HeartseekerBarrage>();

        AssertDownTime(f, 2.0);
    }

    /// <summary>
    /// Player: Event Horizon takes 0.7 seconds to cast, shortened by haste.
    /// Dev: 100 Spirit Points set; one Event Horizon at 0%, 20% and 50% haste; expects 0.7 / (1 + haste).
    /// TODO: the engine keeps Elarion busy for the 1.5s global cooldown after Event Horizon, longer than its cast. The Python sim has
    /// no global cooldown for it. Check in game.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Event_Horizon_Takes_0_7_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Elarion(haste);
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;

        f.Cast<EventHorizon>();

        AssertDownTime(f, 0.7 / (1 + haste));
    }

    /// <summary>
    /// Player: Lunarlight Mark, Skystrider's Grace and Skystrider's Supremacy are instant and can be used during the global
    /// cooldown: they take no time.
    /// Dev: each cast once at 20% haste; the fight clock does not move.
    /// </summary>
    [Fact]
    public void Lunarlight_Mark_Skystriders_Grace_And_Supremacy_Take_No_Time()
    {
        var f = StepFights.Elarion(0.2);

        f.Cast<LunarlightMark>();
        f.Cast<SkystridersGrace>();
        f.Cast<SkystridersSupremacy>();

        Assert.Equal(SimTime.Zero, f.Now);
    }

    /// <summary>
    /// Player: a Highwind Arrow made instant by Resurgent Winds takes the global cooldown, 1.5 seconds shortened by haste; the
    /// next one is back to 2 seconds.
    /// Dev: Resurgent Winds taken and its buff applied (at 1 charge); two Highwind Arrows at 0%, 20% and 50% haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Highwind_Arrow_From_Resurgent_Winds_Takes_The_Global_Cooldown(double haste)
    {
        var f = StepFights.Elarion(haste, talents: [ElarionTalents.ResurgentWinds.TalentId]);
        f.Hero.Auras.Apply(ElarionTalents.ResurgentWinds.Buff, f.Now);

        f.Cast<HighwindArrow>();
        AssertDownTime(f, 1.5 / (1 + haste));

        f.Cast<HighwindArrow>();
        AssertDownTime(f, 1.5 / (1 + haste) + 2.0 / (1 + haste), 2 * Tolerance);
    }

    /// <summary>
    /// Player: down times add up over a rotation, and they follow your haste as it changes: Event Horizon gives 30% haste (as
    /// Spirit of Heroism) for 20 seconds, so what you cast in it is shorter, and longer again once it ends.
    /// Dev: at 20% haste: Lunarlight Mark (0s), a barrage (2s), a Highwind Arrow, a Multishot, then Event Horizon; then a Highwind
    /// Arrow and a Multishot at 50% haste; then 20 seconds later (the buff gone) a Highwind Arrow and a Multishot at 20%. The
    /// haste before and after is read from the hero. Each leg is compared within 1ms per cast.
    /// TODO: fails with Event Horizon's cast time (see Event_Horizon_Takes_0_7_Seconds_Over_1_Plus_Haste: the engine has a global cooldown).
    /// </summary>
    [Fact]
    public void Down_Times_Add_Up_Over_A_Rotation_And_Follow_Haste_Changes()
    {
        const double haste = 0.2;
        var f = StepFights.Elarion(haste);
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;

        f.Cast<LunarlightMark>();
        AssertDownTime(f, 0.0);

        f.Cast<HeartseekerBarrage>();
        AssertDownTime(f, 2.0);

        f.Cast<HighwindArrow>();
        AssertDownTime(f, 2.0 + 2.0 / (1 + haste), 2 * Tolerance);

        f.Cast<Multishot>();
        AssertDownTime(f, 2.0 + (2.0 + 1.5) / (1 + haste), 3 * Tolerance);

        f.Cast<EventHorizon>();
        AssertDownTime(f, 2.0 + (2.0 + 1.5 + 0.7) / (1 + haste), 4 * Tolerance);
        double hasteInHeroism = f.Hero.GetHaste(f.Now);
        Assert.Equal(haste + 0.3, hasteInHeroism, 1e-9);

        double reference = f.Now.TotalSeconds;
        f.Cast<HighwindArrow>();
        Assert.InRange(f.Now.TotalSeconds, reference + 2.0 / (1 + hasteInHeroism) - Tolerance, reference + 2.0 / (1 + hasteInHeroism) + Tolerance);
        f.Cast<Multishot>();
        Assert.InRange(f.Now.TotalSeconds, reference + 3.5 / (1 + hasteInHeroism) - 2 * Tolerance, reference + 3.5 / (1 + hasteInHeroism) + 2 * Tolerance);

        f.Wait(20);
        Assert.Equal(haste, f.Hero.GetHaste(f.Now), 1e-9);
        reference = f.Now.TotalSeconds;
        f.Cast<HighwindArrow>();
        Assert.InRange(f.Now.TotalSeconds, reference + 2.0 / (1 + haste) - Tolerance, reference + 2.0 / (1 + haste) + Tolerance);
        f.Cast<Multishot>();
        Assert.InRange(f.Now.TotalSeconds, reference + 3.5 / (1 + haste) - 2 * Tolerance, reference + 3.5 / (1 + haste) + 2 * Tolerance);
    }
}
