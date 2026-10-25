using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each Rime ability keeps her busy: from the start of the cast until she can act again (cast or channel
/// finished and global cooldown over).
/// Dev: every test casts once at the start of a fight, so the fight clock after <c>Cast</c> returns is the down time.
/// The engine rounds times to the millisecond, hence the 1ms tolerance.
/// </summary>
public class CastTimes
{
    private const double Tolerance = 0.001;

    private static void AssertDownTime(StepDriver f, double expectedSeconds) =>
        Assert.InRange(f.Now.TotalSeconds, expectedSeconds - Tolerance, expectedSeconds + Tolerance);

    /// <summary>
    /// Player: Frost Bolt takes 1.5 seconds to cast, shortened by haste.
    /// Dev: one Frost Bolt at 0%, 20% and 40% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Frost_Bolt_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste);

        f.Cast<FrostBolt>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Cold Snap is instant, so you are busy for the global cooldown: 1.5 seconds, shortened by haste.
    /// Dev: one Cold Snap at 0%, 20% and 40% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Cold_Snap_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste);

        f.Cast<ColdSnap>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Ice Comet is instant, so you are busy for the global cooldown: 1.5 seconds, shortened by haste.
    /// Dev: orbs set to 2 so it can be cast; one Ice Comet at 0%, 20% and 40% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Ice_Comet_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<IceComet>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: Bursting Ice takes 2 seconds to cast, shortened by haste.
    /// Dev: one Bursting Ice at 0%, 20% and 40% haste; expects 2.0 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Bursting_Ice_Takes_2_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste);

        f.Cast<BurstingIce>();

        AssertDownTime(f, 2.0 / (1 + haste));
    }

    /// <summary>
    /// Player: Freezing Torrent is a 2 second channel, and haste does not shorten it (it only makes it tick faster).
    /// Dev: one Freezing Torrent at 0%, 20% and 40% haste; expects 2.0 whatever the haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Freezing_Torrent_Takes_2_Seconds_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Rime(haste);

        f.Cast<FreezingTorrent>();

        AssertDownTime(f, 2.0);
    }

    /// <summary>
    /// Player: with Supreme Torrent the channel lasts 2.8 seconds, and haste does not shorten it.
    /// Dev: the Supreme Torrent talent; one Freezing Torrent at 0%, 20% and 40% haste; expects 2.8 whatever the haste.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Freezing_Torrent_With_Supreme_Torrent_Takes_2_8_Seconds_Whatever_The_Haste(double haste)
    {
        var f = StepFights.Rime(haste, talents: [SupremeTorrent.TalentId]);

        f.Cast<FreezingTorrent>();

        AssertDownTime(f, 2.8);
    }

    /// <summary>
    /// Player: Glacial Blast takes 1.5 seconds to cast, shortened by haste.
    /// Dev: orbs set to 2 so it can be cast; one Glacial Blast at 0%, 20% and 40% haste; expects 1.5 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Glacial_Blast_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<GlacialBlast>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }

    /// <summary>
    /// Player: with Greater Glacial Blast, Glacial Blast takes 2 seconds to cast, shortened by haste.
    /// Dev: the Greater Glacial Blast talent; orbs set to 2; one Glacial Blast at 0%, 20% and 40% haste; expects
    /// 2.0 / (1 + haste).
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Glacial_Blast_With_Greater_Glacial_Blast_Takes_2_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste, talents: [GreaterGlacialBlast.TalentId]);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<GlacialBlast>();

        AssertDownTime(f, 2.0 / (1 + haste));
    }

    /// <summary>
    /// Player: Wrath of Winter takes 1.5 seconds to cast, shortened by haste. It does not use the global cooldown.
    /// Dev: 100 Spirit set so it can be cast; one Wrath of Winter at 0%, 20% and 40% haste; expects 1.5 / (1 + haste).
    /// The +30% haste it grants comes with the cast's completion, so it does not shorten the cast itself.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    public void Wrath_Of_Winter_Takes_1_5_Seconds_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Rime(haste);
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;

        f.Cast<WrathOfWinter>();

        AssertDownTime(f, 1.5 / (1 + haste));
    }
}

