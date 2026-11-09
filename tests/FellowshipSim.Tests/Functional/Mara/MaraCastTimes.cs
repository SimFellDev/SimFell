using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How long each of Mara's abilities keeps her busy. All of them are instant, so she is busy for the global cooldown: 1 second, which haste
/// does not shorten. Some abilities can be used during the global cooldown and take no time.
///
/// Every test casts at the start of the fight, so the fight clock when the cast returns is that time.
/// </summary>
public class MaraCastTimes
{
    /// <summary>
    /// Backstab, Widow's Bite, Skittering Blades, Queen's Fang, Arachnid Assault, Hemorrhaging Strike, Stealth Backstab and Stealth
    /// Skittering Blades take the 1 second global cooldown, whatever your haste.
    /// </summary>
    [Theory]
    [InlineData("Backstab", 0.0)]
    [InlineData("Backstab", 0.2)]
    [InlineData("Backstab", 0.5)]
    [InlineData("Widow's Bite", 0.0)]
    [InlineData("Widow's Bite", 0.2)]
    [InlineData("Widow's Bite", 0.5)]
    [InlineData("Skittering Blades", 0.0)]
    [InlineData("Skittering Blades", 0.2)]
    [InlineData("Skittering Blades", 0.5)]
    [InlineData("Queen's Fang", 0.0)]
    [InlineData("Queen's Fang", 0.2)]
    [InlineData("Queen's Fang", 0.5)]
    [InlineData("Arachnid Assault", 0.0)]
    [InlineData("Arachnid Assault", 0.2)]
    [InlineData("Arachnid Assault", 0.5)]
    [InlineData("Hemorrhaging Strike", 0.0)]
    [InlineData("Hemorrhaging Strike", 0.2)]
    [InlineData("Hemorrhaging Strike", 0.5)]
    [InlineData("Stealth Backstab", 0.0)]
    [InlineData("Stealth Backstab", 0.2)]
    [InlineData("Stealth Backstab", 0.5)]
    [InlineData("Stealth Skittering Blades", 0.0)]
    [InlineData("Stealth Skittering Blades", 0.2)]
    [InlineData("Stealth Skittering Blades", 0.5)]
    public void Takes_The_1_Second_Global_Cooldown_Whatever_The_Haste(string ability, double haste)
    {
        var f = StepFights.Mara(haste: haste);
        if (ability is "Queen's Fang" or "Arachnid Assault" or "Hemorrhaging Strike") f.Set(Mara.ComboPoints, 3); // enough to cast it

        if (ability is "Stealth Backstab" or "Stealth Skittering Blades") f.Cast<BroodingShadows>(); // into Stealth
        double start = f.Seconds();

        f.Cast(ability);

        AssertSeconds(1.0, f.Seconds() - start, tolerance: 0.001);
    }

    /// <summary>
    /// Brooding Shadows, Maiden of Death, Final Stratagem and Matriarch Macabre can be used during the global cooldown: they take no time.
    ///
    /// At 20% haste.
    /// </summary>
    [Theory]
    [InlineData("Brooding Shadows")]
    [InlineData("Maiden of Death")]
    [InlineData("Final Stratagem")]
    [InlineData("Matriarch Macabre")]
    public void Off_Global_Cooldown_Abilities_Take_No_Time(string ability)
    {
        var f = StepFights.Mara(haste: 0.2);
        f.Set(Hero.SpiritPoints, 100); // enough to cast Matriarch Macabre

        f.Cast(ability);

        Assert.Equal(0, f.Seconds());
    }

    /// <summary>
    /// Down times add up over a rotation, and haste does not change them: a Backstab, a Widow's Bite, a Skittering Blades, a Hemorrhaging
    /// Strike and a Backstab in a row take 5 seconds at 50% haste.
    ///
    /// Within 5ms, a millisecond per cast.
    /// </summary>
    [Fact]
    public void Down_Times_Add_Up_Over_A_Rotation()
    {
        var f = StepFights.Mara(haste: 0.5);

        f.Cast<Backstab>();
        f.Cast<WidowsBite>();
        f.Cast<SkitteringBlades>();
        f.Cast<HemorrhagingStrike>();
        f.Cast<Backstab>();

        AssertSeconds(5.0, f.Seconds(), tolerance: 0.005);
    }
}
