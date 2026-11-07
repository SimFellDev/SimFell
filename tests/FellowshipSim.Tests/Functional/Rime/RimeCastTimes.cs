using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each Rime ability keeps you busy: from the start of the cast until you can act again (cast or channel over, and the
/// global cooldown). Every test casts once at the start of the fight, so the fight clock when the cast returns is that time.
/// </summary>
public class RimeCastTimes
{
    /// <summary>An ability with a cast time keeps you busy for that cast time, shortened by haste.</summary>
    [Theory]
    [InlineData("Frost Bolt", 1.5, 0.0)]
    [InlineData("Frost Bolt", 1.5, 0.2)]
    [InlineData("Frost Bolt", 1.5, 0.4)]
    [InlineData("Bursting Ice", 2.0, 0.0)]
    [InlineData("Bursting Ice", 2.0, 0.2)]
    [InlineData("Bursting Ice", 2.0, 0.4)]
    [InlineData("Glacial Blast", 1.5, 0.0)]
    [InlineData("Glacial Blast", 1.5, 0.2)]
    [InlineData("Glacial Blast", 1.5, 0.4)]
    [InlineData("Wrath of Winter", 1.5, 0.0)]
    [InlineData("Wrath of Winter", 1.5, 0.2)]
    [InlineData("Wrath of Winter", 1.5, 0.4)]
    public void A_Cast_Takes_Its_Cast_Time_Over_1_Plus_Haste(string ability, double castTime, double haste)
    {
        var f = StepFights.Rime(haste: haste);
        f.FillResources(); // Winter Orbs for Glacial Blast, Spirit for Wrath of Winter

        f.Cast(ability);

        AssertSeconds(castTime / (1 + haste), f.Seconds());
    }

    /// <summary>An instant ability keeps you busy for the 1.5 second global cooldown, shortened by haste.</summary>
    [Theory]
    [InlineData("Cold Snap", 0.0)]
    [InlineData("Cold Snap", 0.2)]
    [InlineData("Cold Snap", 0.4)]
    [InlineData("Ice Comet", 0.0)]
    [InlineData("Ice Comet", 0.2)]
    [InlineData("Ice Comet", 0.4)]
    public void An_Instant_Takes_The_Global_Cooldown_Of_1_5_Seconds_Over_1_Plus_Haste(string ability, double haste)
    {
        var f = StepFights.Rime(haste: haste);
        f.FillResources(); // Winter Orbs for Ice Comet

        f.Cast(ability);

        AssertSeconds(1.5 / (1 + haste), f.Seconds());
    }

    /// <summary>With Greater Glacial Blast, Glacial Blast takes 2 seconds to cast instead of 1.5, shortened by haste.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Greater_Glacial_Blast_Makes_Glacial_Blast_A_2_Second_Cast_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste: haste, talents: [GreaterGlacialBlast.TalentId]);
        f.RimeOf().WinterOrbs = 2; // enough for Glacial Blast

        f.Cast<GlacialBlast>();

        AssertSeconds(2.0 / (1 + haste), f.Seconds());
    }

    /// <summary>
    /// Freezing Torrent channels for 2 seconds, 2.8 with Supreme Torrent, and haste does not shorten it: it only makes it tick
    /// faster (RimeHasteBreakpoints.cs).
    /// </summary>
    [Theory]
    [InlineData(false, 2.0, 0.0)]
    [InlineData(false, 2.0, 0.2)]
    [InlineData(false, 2.0, 0.4)]
    [InlineData(true, 2.8, 0.0)]
    [InlineData(true, 2.8, 0.2)]
    [InlineData(true, 2.8, 0.4)]
    public void Haste_Does_Not_Shorten_Freezing_Torrent(bool supreme, double channel, double haste)
    {
        var f = StepFights.Rime(haste: haste, talents: supreme ? [SupremeTorrent.TalentId] : []);

        f.Cast<FreezingTorrent>();

        AssertSeconds(channel, f.Seconds());
    }
}
