using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each of Tariq's abilities keeps him busy. His abilities are instant: he is busy for the global cooldown, 1.5 seconds shortened by haste,
/// except Skull Crusher, whose global cooldown is shorter. Some abilities can be used during the global cooldown and take no time.
/// Dev: every test casts at the start of a fight, so the fight clock after <c>Cast</c> returns is the down time. Times are whole milliseconds.
/// </summary>
public class TariqCastTimes
{
    private const double Tolerance = 0.001;

    /// <summary>The global-cooldown abilities: one nested class each, giving the set-up it needs.</summary>
    public abstract class GlobalCooldownAbility<T> where T : Ability
    {
        protected virtual void Prepare(StepDriver f) { }

        /// <summary>
        /// Player: the ability takes the 1.5 second global cooldown, shortened by haste.
        /// Dev: one cast at 0%, 20% and 50% haste; the fight clock reads 1.5 / (1 + haste) afterwards.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.2)]
        [InlineData(0.5)]
        public void Takes_The_1_5_Second_Global_Cooldown_Over_1_Plus_Haste(double haste)
        {
            var f = StepFights.Tariq(haste);
            Prepare(f);
            var start = f.Now;

            f.Cast<T>();

            Assert.Equal(1.5 / (1 + haste), (f.Now - start).TotalSeconds, Tolerance);
        }
    }

    public class WildSwingTime : GlobalCooldownAbility<WildSwing> { }
    public class FaceBreakerTime : GlobalCooldownAbility<FaceBreaker> { }
    public class HeavyStrikeTime : GlobalCooldownAbility<HeavyStrike> { }

    public class ChainLightningTime : GlobalCooldownAbility<ChainLightning>
    {
        protected override void Prepare(StepDriver f) => f.Cast<ThunderCall>();
    }

    public class HammerStormTime : GlobalCooldownAbility<HammerStorm>
    {
        protected override void Prepare(StepDriver f) => f.SetFury(100);
    }

    /// <summary>
    /// Player: Skull Crusher triggers a shortened global cooldown: 1 second, shortened by haste.
    /// Dev: one cast at 0%, 20% and 50% haste with 100 Fury; the fight clock reads 1 / (1 + haste) afterwards.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    public void Skull_Crusher_Takes_The_1_Second_Shortened_Global_Cooldown_Over_1_Plus_Haste(double haste)
    {
        var f = StepFights.Tariq(haste);
        f.SetFury(100);

        f.Cast<SkullCrusher>();

        Assert.Equal(1.0 / (1 + haste), f.Now.TotalSeconds, Tolerance);
    }

    /// <summary>
    /// Player: Leap Smash, Culling Strike, Thunder Call, Focused Wrath and Raging Tempest can be used during the global cooldown: they take no time.
    /// Dev: each cast alone at the start of a fight at 20% haste (Fury, an enemy in execute range and Spirit given where they are needed); the fight
    /// clock does not move.
    /// </summary>
    [Theory]
    [InlineData("Leap Smash")]
    [InlineData("Culling Strike")]
    [InlineData("Thunder Call")]
    [InlineData("Focused Wrath")]
    [InlineData("Raging Tempest")]
    public void Off_Global_Cooldown_Abilities_Take_No_Time(string ability)
    {
        var f = StepFights.Tariq(0.2);
        f.SetHealth(0.2);
        f.GiveTariqSpirit();

        f.Cast(f.Hero.Abilities.First(a => a.Name == ability));

        Assert.Equal(SimTime.Zero, f.Now);
    }

    /// <summary>
    /// Player: abilities used during the global cooldown do not start one: casting one after a global-cooldown ability leaves the clock where it was.
    /// Dev: Wild Swing, then Leap Smash, Thunder Call and Focused Wrath, with the clock read after each.
    /// </summary>
    [Fact]
    public void Off_Global_Cooldown_Abilities_Do_Not_Start_A_Global_Cooldown()
    {
        var f = StepFights.Tariq();
        f.Cast<WildSwing>();
        var after = f.Now;

        f.Cast<LeapSmash>();
        f.Cast<ThunderCall>();
        f.Cast<FocusedWrath>();

        Assert.Equal(after, f.Now);
    }

    /// <summary>
    /// Player: down times add up over a rotation, and a Skull Crusher in it is shorter than the others: a Wild Swing, a Skull Crusher and a Face Breaker
    /// in a row take 1.5 + 1 + 1.5 seconds.
    /// Dev: at 0% haste with 50 Fury; the three cast one after the other; the fight clock reads 4.0s.
    /// </summary>
    [Fact]
    public void Down_Times_Add_Up_Over_A_Rotation()
    {
        var f = StepFights.Tariq();
        f.SetFury(50);

        f.Cast<WildSwing>();
        f.Cast<SkullCrusher>();
        f.Cast<FaceBreaker>();

        Assert.Equal(4.0, f.Now.TotalSeconds, 3 * Tolerance);
    }

    /// <summary>
    /// Player: your haste changes as you go: Raging Tempest gives 30% haste (as Spirit of Heroism) for 20 seconds, so what you cast in it is shorter,
    /// and longer again once it ends.
    /// Dev: Raging Tempest at 0% haste; a Wild Swing takes 1.5 / 1.3; 20 seconds later it takes 1.5 again.
    /// </summary>
    [Fact]
    public void Down_Times_Follow_Haste_Changes()
    {
        var f = StepFights.Tariq();
        f.GiveTariqSpirit();
        f.Cast<RagingTempest>();
        var start = f.Now;
        f.Cast<WildSwing>();
        Assert.Equal(1.5 / 1.3, (f.Now - start).TotalSeconds, Tolerance);

        f.Wait(21);
        start = f.Now;
        f.Cast<WildSwing>();
        Assert.Equal(1.5, (f.Now - start).TotalSeconds, Tolerance);
    }
}
