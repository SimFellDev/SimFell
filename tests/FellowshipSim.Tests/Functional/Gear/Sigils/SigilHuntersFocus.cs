using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Hunter's Focus (sigil 42): "Your targeted offensive abilities grant you 4 / 8 / 12 / 16 Haste rating for 8 seconds, stacking up to 5
/// times. The Haste is lost if you use a targeted offensive ability on any other enemy" (ranks 1 to 4). A rating point is 0.16%.
/// </summary>
public class SigilHuntersFocus
{
    /// <summary>Your targeted offensive abilities give you 4 / 8 / 12 / 16 Haste rating.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 4)]
    [InlineData("Rime", "Frost Bolt", 2, 8)]
    [InlineData("Rime", "Frost Bolt", 3, 12)]
    [InlineData("Rime", "Frost Bolt", 4, 16)]
    [InlineData("Ardeos", "Infernal Wave", 4, 16)]
    [InlineData("Elarion", "Focused Shot", 4, 16)]
    [InlineData("Gunde", "Double Strike", 4, 16)]
    [InlineData("Mara", "Skittering Blades", 4, 16)]
    [InlineData("Tariq", "Heavy Strike", 4, 16)]
    public void Hunters_Focus_Targeted_Abilities_Give_Haste_Rating(string hero, string ability, int rank, double rating)
    {
        var f = StepFights.Of(hero, augmentations: [new(HuntersFocus.SigilId, rank)]);

        f.Cast(ability);

        Assert.Equal(rating * 0.0016, f.Haste(), 1e-9);
    }

    /// <summary>Hunter's Focus stacks up to 5 times: a 6th ability adds nothing.</summary>
    [Fact]
    public void Hunters_Focus_Stacks_Up_To_5_Times()
    {
        var f = StepFights.Of("Rime", augmentations: [new(HuntersFocus.SigilId, 1)]);

        for (int cast = 0; cast < 6; cast++) f.Cast("Frost Bolt");

        Assert.Equal(5 * 4 * 0.0016, f.Haste(), 1e-9);
    }

    /// <summary>Hunter's Focus lasts 8 seconds.</summary>
    [Fact]
    public void Hunters_Focus_Lasts_8_Seconds()
    {
        var f = StepFights.Of("Rime", augmentations: [new(HuntersFocus.SigilId, 1)]);

        f.Cast("Frost Bolt");
        double start = f.CompletedAt("Frost Bolt");

        f.WaitUntil(start + 8 - 0.1);
        Assert.Equal(4 * 0.0016, f.Haste(), 1e-9);
        f.WaitUntil(start + 8 + 0.1);
        Assert.Equal(0, f.Haste(), 1e-9);
    }

    /// <summary>A targeted offensive ability on another enemy loses the stacks, and starts again from one.</summary>
    [Fact]
    public void Another_Enemy_Loses_The_Hunters_Focus_Stacks()
    {
        var f = StepFights.Of("Rime", enemies: 2, augmentations: [new(HuntersFocus.SigilId, 1)]);

        f.Cast("Frost Bolt");
        f.Cast("Frost Bolt");
        f.Cast("Frost Bolt", target: f.Enemies()[1]);

        Assert.Equal(4 * 0.0016, f.Haste(), 1e-9);
    }

    /// <summary>
    /// Only targeted offensive abilities give Hunter's Focus: Ice Blitz, which only buffs you, does not, and neither do Ardeos's Fire Ball
    /// and Detonate.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Blitz")]
    [InlineData("Ardeos", "Fire Ball")]
    [InlineData("Ardeos", "Detonate")]
    public void Only_Targeted_Offensive_Abilities_Give_Hunters_Focus(string hero, string ability)
    {
        var f = StepFights.Of(hero, augmentations: [new(HuntersFocus.SigilId, 1)]);
        f.FillResources(); // enough to cast it
        if (ability == "Detonate") f.Cast("Searing Blaze"); // Detonate only hits a burning enemy
        double hasteBefore = f.Haste();

        f.Cast(ability);

        Assert.Equal(hasteBefore, f.Haste(), 1e-9);
    }
}
