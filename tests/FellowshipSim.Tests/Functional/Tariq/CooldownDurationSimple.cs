using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Tariq's base cooldowns and how haste affects them (Face Breaker, Heavy Strike, Culling Strike, Chain Lightning, Leap Smash, Wild Swing, Skull Crusher
/// and Hammer Storm are shortened by it, at the cast; Thunder Call and Focused Wrath are not). Talents that shorten them are in the talent files.
/// Dev: the time an ability is ready again is read from the hero's cooldown state right after the cast at the start of a fight (now + time remaining),
/// within 2ms. Wild Swing, Skull Crusher and Hammer Storm have a cooldown as long as their global cooldown, so for them a cast is repeated at once instead.
/// </summary>
public class TariqCooldownDurationSimple
{
    private const double Tolerance = 0.002;

    private static double ReadyAt(StepDriver f, Ability ability) =>
        f.Now.TotalSeconds + f.Hero.GetCooldownRemains(ability.Name, f.Now);

    private static Ability Named(StepDriver f, string name) => f.Hero.Abilities.First(a => a.Name == name);

    private static StepDriver Ready(double haste)
    {
        var f = StepFights.Tariq(haste);
        f.SetHealth(0.2);
        f.Cast<ThunderCall>();
        return f;
    }

    /// <summary>
    /// Player: Face Breaker has a 6 second cooldown, Heavy Strike 8, Culling Strike 6, Chain Lightning 8 and Leap Smash 20, and haste shortens them:
    /// base / (1 + haste).
    /// Dev: at 0% and 20% haste; Thunder Call (for Chain Lightning) and an enemy at 20% health (for Culling Strike); one cast; the ability is ready
    /// that long after the start of the fight.
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
    public void Hasted_Cooldowns_Follow_Haste(string name, double seconds, double haste)
    {
        var f = Ready(haste);
        var ability = Named(f, name);

        f.Cast(ability);

        Assert.Equal(seconds / (1 + haste), ReadyAt(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: Wild Swing has a 1.5 second cooldown, Skull Crusher 1 and Hammer Storm 1.5, all shortened by haste: they are ready again when the
    /// global cooldown ends, however high your haste.
    /// Dev: at 0% and 50% haste with 100 Fury; two casts in a row, the second right after the first (it throws if the ability is still on cooldown).
    /// </summary>
    [Theory]
    [InlineData("Wild Swing", 0.0)]
    [InlineData("Wild Swing", 0.5)]
    [InlineData("Skull Crusher", 0.0)]
    [InlineData("Skull Crusher", 0.5)]
    [InlineData("Hammer Storm", 0.0)]
    [InlineData("Hammer Storm", 0.5)]
    public void Wild_Swing_Skull_Crusher_And_Hammer_Storm_Are_Ready_When_The_Global_Cooldown_Ends(string name, double haste)
    {
        var f = StepFights.Tariq(haste);
        f.SetFury(100);
        var ability = Named(f, name);

        f.Cast(ability);
        f.Cast(ability);

        Assert.Equal(2, f.Events.Count(e => e.Kind == StepEventKind.CastCompleted && e.Name == name));
    }

    /// <summary>
    /// Player: the cooldowns of Thunder Call (45 seconds) and Focused Wrath (90) are not shortened by haste.
    /// Dev: at 0% and 50% haste; one cast at the start of the fight; the ability is ready that long after it.
    /// </summary>
    [Theory]
    [InlineData("Thunder Call", 45, 0.0)]
    [InlineData("Thunder Call", 45, 0.5)]
    [InlineData("Focused Wrath", 90, 0.0)]
    [InlineData("Focused Wrath", 90, 0.5)]
    public void Fixed_Cooldowns_Ignore_Haste(string name, double seconds, double haste)
    {
        var f = StepFights.Tariq(haste);
        var ability = Named(f, name);

        f.Cast(ability);

        Assert.Equal(seconds, ReadyAt(f, ability), Tolerance);
    }

    /// <summary>
    /// Player: an ability on cooldown cannot be cast: it is refused until the cooldown is over, then it works.
    /// Dev: Face Breaker; refused at 5.9s, cast at 6.0s.
    /// </summary>
    [Fact]
    public void An_Ability_On_Cooldown_Is_Refused_Until_It_Ends()
    {
        var f = StepFights.Tariq();
        f.Cast<FaceBreaker>();

        f.Wait(5.9 - f.Now.TotalSeconds);
        Assert.False(f.TryCast<FaceBreaker>().Succeeded);

        f.Wait(0.11);
        Assert.True(f.TryCast<FaceBreaker>().Succeeded);
    }

    /// <summary>
    /// Player: Raging Tempest has no cooldown of its own: it costs 100 Spirit Points, so you can cast it again as soon as you have them.
    /// Dev: two casts in a row with Spirit Points given before each.
    /// </summary>
    [Fact]
    public void Raging_Tempest_Has_No_Cooldown()
    {
        var f = StepFights.Tariq();
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();
        f.GiveTariqSpirit();

        Assert.True(f.TryCast<RagingTempest>().Succeeded);
    }
}
