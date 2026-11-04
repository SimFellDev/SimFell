using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Taking time off a cooldown (<see cref="CooldownState.Reduce"/>, which every talent, ability and gear effect reaches through
/// <see cref="Hero.ReduceCooldown(string, SimTime, SimTime)"/>). The functional tests check only which effect takes how much off what.
/// </summary>
public class CooldownStateTests
{
    private static SimTime At(double seconds) => SimTime.FromSeconds(seconds);

    /// <summary>A reduction on a ready cooldown is lost: the next spend recharges in full.</summary>
    [Fact]
    public void A_Reduction_While_Ready_Is_Not_Saved_For_Later()
    {
        var cooldown = new CooldownState(maxCharges: 1);
        cooldown.Reduce(At(5), At(0));

        cooldown.Spend(At(1), rechargeSeconds: 10);

        Assert.Equal(At(11), cooldown.NextChargeAt);
    }

    /// <summary>A reduction larger than what is left makes the cooldown ready, and the rest does not carry over to the next spend.</summary>
    [Fact]
    public void A_Reduction_Larger_Than_What_Is_Left_Makes_It_Ready_And_The_Rest_Is_Lost()
    {
        var cooldown = new CooldownState(maxCharges: 1);
        cooldown.Spend(At(0), rechargeSeconds: 10);

        cooldown.Reduce(At(5), At(8)); // 2s left

        Assert.Equal(1, cooldown.ChargesAt(At(8)));
        cooldown.Spend(At(8), rechargeSeconds: 10);
        Assert.Equal(At(18), cooldown.NextChargeAt);
    }

    /// <summary>With several charges recharging, what a reduction has left after bringing one back comes off the next.</summary>
    [Fact]
    public void With_Charges_The_Rest_Of_A_Reduction_Goes_To_The_Next_Charge()
    {
        var cooldown = new CooldownState(maxCharges: 2);
        cooldown.Spend(At(0), rechargeSeconds: 12);
        cooldown.Spend(At(0), rechargeSeconds: 12); // the second recharges once the first is back

        cooldown.Reduce(At(5), At(10)); // 2s left on the first: 3s come off the second

        Assert.Equal(1, cooldown.ChargesAt(At(10)));
        Assert.Equal(At(10 + 12 - 3), cooldown.NextChargeAt);
    }

    /// <summary>
    /// A reduction takes cooldown-seconds, not real seconds: while the cooldown recovers twice as fast, 10 seconds off saves 5 real
    /// seconds.
    /// </summary>
    [Fact]
    public void A_Reduction_Takes_Cooldown_Seconds_Not_Real_Seconds()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime" });
        hero.CooldownAccelerations.Add((_, _) => 1.0); // recovers twice as fast
        var iceBlitz = hero.Abilities.OfType<IceBlitz>().Single();
        var cooldown = hero.CooldownOf(iceBlitz);
        cooldown.Spend(At(0), rechargeSeconds: 60); // ready at 30s

        hero.ReduceCooldown(iceBlitz.Name, At(10), At(10));

        Assert.Equal(At(25), cooldown.NextChargeAt);
    }
}
