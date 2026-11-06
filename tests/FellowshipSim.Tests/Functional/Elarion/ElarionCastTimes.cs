using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each Elarion ability keeps you busy: from the start of the cast until you can act again (cast or channel over, and the
/// global cooldown). Every test casts once at the start of the fight, so the fight clock when the cast returns is that time.
/// </summary>
public class ElarionCastTimes
{
    /// <summary>An ability with a cast time keeps you busy for that cast time, shortened by haste.</summary>
    [Theory]
    [InlineData("Focused Shot", 1.5, 0.0)]
    [InlineData("Focused Shot", 1.5, 0.2)]
    [InlineData("Focused Shot", 1.5, 0.5)]
    [InlineData("Highwind Arrow", 2.0, 0.0)]
    [InlineData("Highwind Arrow", 2.0, 0.2)]
    [InlineData("Highwind Arrow", 2.0, 0.5)]
    public void A_Cast_Takes_Its_Cast_Time_Over_1_Plus_Haste(string ability, double castTime, double haste)
    {
        var f = StepFights.Elarion(haste: haste);

        f.Cast(ability);

        AssertSeconds(castTime / (1 + haste), f.Seconds());
    }

    /// <summary>An instant ability keeps you busy for the 1.5 second global cooldown, shortened by haste.</summary>
    [Theory]
    [InlineData("Celestial Shot", 0.0)]
    [InlineData("Celestial Shot", 0.2)]
    [InlineData("Celestial Shot", 0.5)]
    [InlineData("Multishot", 0.0)]
    [InlineData("Multishot", 0.2)]
    [InlineData("Multishot", 0.5)]
    [InlineData("Starfall Volley", 0.0)]
    [InlineData("Starfall Volley", 0.2)]
    [InlineData("Starfall Volley", 0.5)]
    public void An_Instant_Takes_The_Global_Cooldown_Of_1_5_Seconds_Over_1_Plus_Haste(string ability, double haste)
    {
        var f = StepFights.Elarion(haste: haste);

        f.Cast(ability);

        AssertSeconds(1.5 / (1 + haste), f.Seconds());
    }

    /// <summary>Lunarlight Mark, Skystrider's Grace and Skystrider's Supremacy are off the global cooldown: they take no time.</summary>
    [Theory]
    [InlineData("Lunarlight Mark")]
    [InlineData("Skystrider's Grace")]
    [InlineData("Skystrider's Supremacy")]
    public void Off_Global_Cooldown_Abilities_Take_No_Time(string ability)
    {
        var f = StepFights.Elarion(haste: 0.2);

        f.Cast(ability);

        AssertSeconds(0.0, f.Seconds());
    }

    /// <summary>
    /// Heartseeker Barrage channels for 2 seconds, 2.5 with Fusillade, and haste does not shorten it: it only fits more arrows in
    /// (ElarionHasteBreakpoints.cs).
    /// </summary>
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(false, 0.2)]
    [InlineData(false, 0.5)]
    [InlineData(true, 0.0)]
    [InlineData(true, 0.2)]
    [InlineData(true, 0.5)]
    public void Heartseeker_Barrage_Channels_For_2_Seconds_2_5_With_Fusillade_Whatever_Your_Haste(bool fusillade, double haste)
    {
        var f = StepFights.Elarion(haste: haste, talents: fusillade ? [Fusillade.TalentId] : []);

        f.Cast<HeartseekerBarrage>();

        AssertSeconds(fusillade ? 2.5 : 2.0, f.Seconds());
    }

    /// <summary>
    /// Event Horizon takes 0.7 seconds to cast, shortened by haste, and its global cooldown is no longer than its cast: you can act
    /// again as soon as the cast ends.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Event_Horizon_Takes_0_7_Seconds_Over_1_Plus_Haste_With_No_Longer_Global_Cooldown(double haste)
    {
        var f = StepFights.Elarion(haste: haste);
        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.Cast<EventHorizon>();

        AssertSeconds(0.7 / (1 + haste), f.Seconds());
    }

    // ---- Resurgent Winds (its damage is in ElarionTalents.cs)

    /// <summary>
    /// With Resurgent Winds up, your next Highwind Arrow is instant, so it takes the 1.5 second global cooldown, shortened by haste; the
    /// one after it is back to its 2 second cast.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Resurgent_Winds_Makes_The_Next_Highwind_Arrow_Instant(double haste)
    {
        var f = StepFights.Elarion(haste: haste, talents: [ResurgentWinds.TalentId]);
        f.ForceProcs(key => key == ResurgentWinds.ProcKey ? false : null); // no new Resurgent Winds for the second arrow
        f.Apply(ResurgentWinds.Buff); // as Lunarlight Mark would give it

        f.Cast<HighwindArrow>();
        double instantArrowEnd = f.Seconds();
        f.Cast<HighwindArrow>();
        double castArrowEnd = f.Seconds();

        using var check = new Checks(f);
        check.Seconds(1.5 / (1 + haste), instantArrowEnd);
        check.Seconds(2.0 / (1 + haste), castArrowEnd - instantArrowEnd);
    }
}
