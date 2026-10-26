using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each of Mara's abilities keeps her busy. All of them are instant, so she is busy for the global cooldown: 1 second, which haste
/// does not shorten. Some abilities can be used during the global cooldown and take no time.
/// Dev: every test casts at the start of a fight, so the fight clock after <c>Cast</c> returns is the down time. Times are whole milliseconds.
/// </summary>
public class MaraCastTimes
{
    private const double Tolerance = 0.001;

    /// <summary>The global-cooldown abilities: one nested class each, giving the set-up it needs.</summary>
    public abstract class GlobalCooldownAbility<T> where T : Ability
    {
        protected virtual void Prepare(StepDriver f) { }

        /// <summary>
        /// Player: the ability takes the 1 second global cooldown, whatever your haste.
        /// Dev: one cast at 0%, 20% and 50% haste; the fight clock reads 1.0s afterwards.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.2)]
        [InlineData(0.5)]
        public void Takes_The_1_Second_Global_Cooldown_Whatever_The_Haste(double haste)
        {
            var f = StepFights.Mara(haste);
            Prepare(f);
            var start = f.Now;

            f.Cast<T>();

            Assert.Equal(1.0, (f.Now - start).TotalSeconds, Tolerance);
        }
    }

    public class BackstabTime : GlobalCooldownAbility<Backstab> { }
    public class WidowsBiteTime : GlobalCooldownAbility<WidowsBite> { }
    public class SkitteringBladesTime : GlobalCooldownAbility<SkitteringBlades> { }

    public class QueensFangTime : GlobalCooldownAbility<QueensFang>
    {
        protected override void Prepare(StepDriver f) => f.SetComboPoints(3);
    }

    public class ArachnidAssaultTime : GlobalCooldownAbility<ArachnidAssault>
    {
        protected override void Prepare(StepDriver f) => f.SetComboPoints(3);
    }

    public class HemorrhagingStrikeTime : GlobalCooldownAbility<HemorrhagingStrike>
    {
        protected override void Prepare(StepDriver f) => f.SetComboPoints(3);
    }

    public class StealthBackstabTime : GlobalCooldownAbility<StealthBackstab>
    {
        protected override void Prepare(StepDriver f) => f.Cast<BroodingShadows>();
    }

    public class StealthSkitteringBladesTime : GlobalCooldownAbility<StealthSkitteringBlades>
    {
        protected override void Prepare(StepDriver f) => f.Cast<BroodingShadows>();
    }

    /// <summary>
    /// Player: Brooding Shadows, Maiden of Death, Final Stratagem and Matriarch Macabre can be used during the global cooldown: they take
    /// no time.
    /// Dev: each cast alone at the start of a fight at 20% haste (Spirit Points given for Matriarch Macabre); the fight clock does not move.
    /// </summary>
    [Theory]
    [InlineData("Brooding Shadows")]
    [InlineData("Maiden of Death")]
    [InlineData("Final Stratagem")]
    [InlineData("Matriarch Macabre")]
    public void Off_Global_Cooldown_Abilities_Take_No_Time(string ability)
    {
        var f = StepFights.Mara(0.2);
        f.GiveMaraSpirit();

        switch (ability)
        {
            case "Brooding Shadows": f.Cast<BroodingShadows>(); break;
            case "Maiden of Death": f.Cast<MaidenOfDeath>(); break;
            case "Final Stratagem": f.Cast<FinalStratagem>(); break;
            default: f.Cast<MatriarchMacabre>(); break;
        }

        Assert.Equal(SimTime.Zero, f.Now);
    }

    /// <summary>
    /// Player: down times add up over a rotation, and haste does not change them: five generators take 5 seconds.
    /// Dev: at 50% haste, a Backstab, a Widow's Bite, a Skittering Blades, a Hemorrhaging Strike and a Backstab; 5.0s (1ms each).
    /// </summary>
    [Fact]
    public void Down_Times_Add_Up_Over_A_Rotation()
    {
        var f = StepFights.Mara(0.5);

        f.Cast<Backstab>();
        f.Cast<WidowsBite>();
        f.Cast<SkitteringBlades>();
        f.Cast<HemorrhagingStrike>();
        f.Cast<Backstab>();

        Assert.Equal(5.0, f.Now.TotalSeconds, 5 * Tolerance);
    }
}
