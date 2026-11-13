using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long Tariq's cooldowns last: the base durations, and what haste does to them (Face Breaker, Heavy Strike, Culling Strike, Chain
/// Lightning, Leap Smash, Wild Swing, Skull Crusher and Hammer Storm are shortened by it, at the cast; Thunder Call and Focused Wrath are
/// not). The talents that shorten them are in the talent files.
///
/// Every duration is checked by the ability being not ready 0.1s before its end and ready 0.1s after. Wild Swing, Skull Crusher and Hammer
/// Storm have a cooldown as long as their global cooldown, so for them a cast is repeated at once instead.
/// </summary>
public class TariqCooldowns
{
    // ---- Base durations and haste

    /// <summary>
    /// Face Breaker has a 6 second cooldown, Heavy Strike 8, Culling Strike 6, Chain Lightning 8 and Leap Smash 20, and haste shortens
    /// them: base / (1 + haste).
    ///
    /// Haste is a cooldown reduction here, applied once at the cast (the length is divided by 1 + haste): the cooldown then runs in real
    /// time, with no acceleration (recovery rate 1). Charge cooldowns are different: their haste is an acceleration.
    ///
    /// Thunder Call is cast first (for Chain Lightning) and the enemy is at 20% health (for Culling Strike).
    /// </summary>
    [Theory]
    [InlineData("Face Breaker", 6, 0.0)]
    [InlineData("Face Breaker", 6, 0.2)]
    [InlineData("Heavy Strike", 8, 0.0)]
    [InlineData("Heavy Strike", 8, 0.2)]
    [InlineData("Culling Strike", 6, 0.0)]
    [InlineData("Culling Strike", 6, 0.2)]
    [InlineData("Chain Lightning", 8, 0.0)]
    [InlineData("Chain Lightning", 8, 0.2)]
    [InlineData("Leap Smash", 20, 0.0)]
    [InlineData("Leap Smash", 20, 0.2)]
    public void Hasted_Cooldowns_Follow_Haste(string ability, double cooldown, double haste)
    {
        var f = StepFights.Tariq(haste: haste);
        f.SetHealth(0.2); // in Culling Strike's range

        f.Cast<ThunderCall>(); // enough to cast Chain Lightning

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown / (1 + haste);

        Assert.Equal(1.0, f.AccelerationRate(ability)); // a reduction at the cast, not an acceleration
        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }

    /// <summary>
    /// Wild Swing has a 1.5 second cooldown, Skull Crusher 1 and Hammer Storm 1.5, all shortened by haste: they are ready again when the
    /// global cooldown ends, however high your haste. Haste shortens them at the cast, with no acceleration (recovery rate 1).
    ///
    /// Two casts in a row with 100 Fury, the second right after the first (it throws if the ability is still on cooldown).
    /// </summary>
    [Theory]
    [InlineData("Wild Swing", 0.0)]
    [InlineData("Wild Swing", 0.5)]
    [InlineData("Skull Crusher", 0.0)]
    [InlineData("Skull Crusher", 0.5)]
    [InlineData("Hammer Storm", 0.0)]
    [InlineData("Hammer Storm", 0.5)]
    public void Wild_Swing_Skull_Crusher_And_Hammer_Storm_Are_Ready_When_The_Global_Cooldown_Ends(string ability, double haste)
    {
        var f = StepFights.Tariq(haste: haste);
        f.Set(Tariq.Fury, 100); // enough for both casts

        f.Cast(ability);
        Assert.Equal(1.0, f.AccelerationRate(ability)); // a reduction at the cast, not an acceleration
        f.Cast(ability);

        Assert.Equal(2, f.CastsCompleted(ability).Count);
    }

    /// <summary>The cooldowns of Thunder Call (45 seconds) and Focused Wrath (90) are not shortened by haste.</summary>
    [Theory]
    [InlineData("Thunder Call", 45, 0.0)]
    [InlineData("Thunder Call", 45, 0.5)]
    [InlineData("Focused Wrath", 90, 0.0)]
    [InlineData("Focused Wrath", 90, 0.5)]
    public void Fixed_Cooldowns_Ignore_Haste(string ability, double cooldown, double haste)
    {
        var f = StepFights.Tariq(haste: haste);

        f.Cast(ability);
        double end = f.CompletedAt(ability) + cooldown;

        f.WaitUntil(end - 0.1);
        Assert.False(f.IsReady(ability));
        f.WaitUntil(end + 0.1);
        Assert.True(f.IsReady(ability));
    }
}
