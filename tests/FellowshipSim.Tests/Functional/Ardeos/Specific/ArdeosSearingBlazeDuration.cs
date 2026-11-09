using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long a Searing Blaze lasts: 30 seconds, and a recast on an enemy that has it keeps the time left on top of the new 30 seconds, up to
/// 30% more (39 seconds). That refreshed duration is also the most time an extension (Incinerate, Backdraft) can give it until it ends.
///
/// Per the user, as the reference sims have it: the Python sim's SearingBlazeDoT.fuse (pandemic fraction 0.30) and the C++ sim's
/// DOT_REFRESH_PANDEMIC. Every duration is checked by the effect being on the enemy 0.1s before its end and gone 0.1s after.
/// </summary>
public class ArdeosSearingBlazeDuration
{
    // ---- Recast

    /// <summary>
    /// Casting Searing Blaze on an enemy that has it sets it to 30 seconds plus the time it had left, up to 39 seconds (130% of 30):
    /// min(39, 30 + time left).
    /// Also known as pandemic duration extension.
    /// </summary>
    [Theory]
    [InlineData(20.0)]
    [InlineData(10.0)]
    [InlineData(9.0)]
    [InlineData(5.0)]
    [InlineData(1.0)]
    public void Recasting_Searing_Blaze_Adds_30_Seconds_To_Its_Time_Left_Up_To_39(double leftAtRecast)
    {
        var f = StepFights.Ardeos();

        f.Cast<SearingBlaze>();
        f.WaitUntil(f.CompletedAt("Searing Blaze") + 30.0 - leftAtRecast); // leftAtRecast seconds before it ends

        f.Cast<SearingBlaze>();
        double end = f.CompletedAt("Searing Blaze") + Math.Min(39.0, 30.0 + leftAtRecast);

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Searing Blaze"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Searing Blaze"));
    }

    // ---- Extensions after a recast (without a recast: Incinerate's in ArdeosAbilities.cs, Backdraft's in ArdeosTalents.cs)

    /// <summary>
    /// After a recast, the refreshed duration is Searing Blaze's new maximum: Incinerate's hits, cast right after a recast with 6 seconds
    /// left (36 seconds) or 10 (39 seconds), bring it back up to that duration, not to 30.
    ///
    /// Incinerate's 8 hits add 1.5 seconds each, more than the time they take, so the last one leaves the maximum.
    /// </summary>
    [Theory]
    [InlineData(6.0)]
    [InlineData(10.0)]
    public void Incinerate_After_A_Recast_Brings_Searing_Blaze_Up_To_Its_Refreshed_Duration(double leftAtRecast)
    {
        var f = StepFights.Ardeos();
        f.Set(Hero.SpiritPoints, 100); // enough to cast Incinerate

        f.Cast<SearingBlaze>();
        f.WaitUntil(f.CompletedAt("Searing Blaze") + 30.0 - leftAtRecast); // leftAtRecast seconds before it ends
        f.Cast<SearingBlaze>();
        double maximum = Math.Min(39.0, 30.0 + leftAtRecast); // the refreshed duration

        f.Cast<Incinerate>();
        double end = f.DirectHits<Incinerate>().Last().Time.TotalSeconds + maximum;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Searing Blaze"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Searing Blaze"));
    }

    /// <summary>
    /// After a recast, the refreshed duration is Searing Blaze's new maximum: with Backdraft, Detonates cast right after a recast with 6
    /// seconds left (36 seconds) or 10 (39 seconds) bring it back up to that duration, not to 30.
    ///
    /// Three Detonates add 1.5 seconds each about 1 second apart, more than the time they take, so the last one leaves the maximum.
    /// </summary>
    [Theory]
    [InlineData(6.0)]
    [InlineData(10.0)]
    public void Backdraft_After_A_Recast_Brings_Searing_Blaze_Up_To_Its_Refreshed_Duration(double leftAtRecast)
    {
        var f = StepFights.Ardeos(talents: [Backdraft.TalentId]);

        f.Cast<SearingBlaze>();
        f.WaitUntil(f.CompletedAt("Searing Blaze") + 30.0 - leftAtRecast); // leftAtRecast seconds before it ends
        f.Cast<SearingBlaze>();
        double maximum = Math.Min(39.0, 30.0 + leftAtRecast); // the refreshed duration

        for (int i = 0; i < 3; i++)
        {
            f.Set(Ardeos.Cinders, 100); // a Burning Ember for each
            f.Cast<Detonate>();
        }
        double end = f.CompletedAt("Detonate") + maximum;

        f.WaitUntil(end - 0.1);
        Assert.True(f.HasDebuff("Searing Blaze"));
        f.WaitUntil(end + 0.1);
        Assert.False(f.HasDebuff("Searing Blaze"));
    }
}
