using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each of Gunde's abilities keeps him busy. His abilities are instant (Bloodbound Spirit lost its cast time in patch 0.4.5.0), so he is
/// busy for the global cooldown: 1.5 seconds, shortened by haste. Some abilities can be used during the global cooldown and take no time.
/// Dev: every test casts at the start of a fight, so the fight clock after <c>Cast</c> returns is the down time. Times are whole milliseconds.
/// </summary>
public class GundeCastTimes
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
            var f = StepFights.Gunde(haste);
            Prepare(f);
            var start = f.Now;

            f.Cast<T>();

            Assert.Equal(1.5 / (1 + haste), (f.Now - start).TotalSeconds, Tolerance);
        }
    }

    public class DoubleStrikeTime : GlobalCooldownAbility<DoubleStrike> { }
    public class ReaversEdgeTime : GlobalCooldownAbility<ReaversEdge> { }
    public class BloodArcTime : GlobalCooldownAbility<BloodArc> { }
    public class HeartSplitterTime : GlobalCooldownAbility<HeartSplitter> { }
    public class GrimCarveTime : GlobalCooldownAbility<GrimCarve> { }
    public class RuptureTime : GlobalCooldownAbility<Rupture> { }

    /// <summary>
    /// Player: Slaughter, Reign in Blood, Owed in Blood, Warbound, Butcher's Hook, Jawbreaker, Reckless Abandon and the auto attack can be used
    /// during the global cooldown: they take no time.
    /// Dev: each cast alone at the start of a fight at 20% haste (a stack of Owed in Blood given for it, Rend on the enemy for Slaughter); the
    /// fight clock does not move.
    /// </summary>
    [Theory]
    [InlineData("Slaughter")]
    [InlineData("Reign in Blood")]
    [InlineData("Owed in Blood")]
    [InlineData("Warbound")]
    [InlineData("Butcher's Hook")]
    [InlineData("Jawbreaker")]
    [InlineData("Reckless Abandon")]
    [InlineData("Attack")]
    public void Off_Global_Cooldown_Abilities_Take_No_Time(string ability)
    {
        var f = StepFights.Gunde(0.2);
        f.SetOwedInBlood(1);

        f.Cast(f.Hero.Abilities.First(a => a.Name == ability));

        Assert.Equal(SimTime.Zero, f.Now);
    }

    /// <summary>
    /// Player: abilities used during the global cooldown do not start one: casting one after a global-cooldown ability leaves the clock where it was.
    /// Dev: Heart Splitter, then Reign in Blood, Owed in Blood and Warbound, with the clock read after each.
    /// </summary>
    [Fact]
    public void Off_Global_Cooldown_Abilities_Do_Not_Start_A_Global_Cooldown()
    {
        var f = StepFights.Gunde();
        f.SetOwedInBlood(1);
        f.Cast<HeartSplitter>();
        var after = f.Now;

        f.Cast<ReignInBlood>();
        f.Cast<OwedInBlood>();
        f.Cast<Warbound>();

        Assert.Equal(after, f.Now);
    }

    /// <summary>
    /// Player: down times add up over a rotation: Double Strike, Reaver's Edge, Blood Arc, Heart Splitter and Grim Carve in a row take 5
    /// global cooldowns.
    /// Dev: at 50% haste; the five cast one after the other; the fight clock reads 5 x 1.5 / 1.5 = 5.0s.
    /// </summary>
    [Fact]
    public void Down_Times_Add_Up_Over_A_Rotation()
    {
        var f = StepFights.Gunde(0.5);

        f.Cast<DoubleStrike>();
        f.Cast<ReaversEdge>();
        f.Cast<BloodArc>();
        f.Cast<HeartSplitter>();
        f.Cast<GrimCarve>();

        Assert.Equal(5.0, f.Now.TotalSeconds, 5 * Tolerance);
    }
}
