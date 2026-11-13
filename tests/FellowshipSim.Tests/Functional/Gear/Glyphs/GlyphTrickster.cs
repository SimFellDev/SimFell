using FellowshipSim.Core.Customization.Glyphs;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Trickster (glyph 700): "Your Power abilities grant you 2.5% / 4.0% / 6.4% / 10% increased Critical Strike chance for 4 seconds"
/// (ranks 1 to 4). The bare hero has no crit chance, so the hero's crit chance is the glyph's.
/// </summary>
public class GlyphTrickster
{
    /// <summary>Your Power abilities give you The Trickster's crit chance: 2.5% / 4% / 6.4% / 10%.</summary>
    [Theory]
    [InlineData("Rime", "Ice Comet", 1, 0.025)]
    [InlineData("Rime", "Ice Comet", 2, 0.04)]
    [InlineData("Rime", "Ice Comet", 3, 0.064)]
    [InlineData("Rime", "Ice Comet", 4, 0.10)]
    [InlineData("Ardeos", "Detonate", 4, 0.10)]
    [InlineData("Elarion", "Highwind Arrow", 4, 0.10)]
    [InlineData("Gunde", "Rupture", 4, 0.10)]
    [InlineData("Mara", "Queen's Fang", 4, 0.10)]
    [InlineData("Tariq", "Skull Crusher", 4, 0.10)]
    public void The_Trickster_Power_Abilities_Give_Crit_Chance(string hero, string power, int rank, double critChance)
    {
        var f = StepFights.Of(hero, glyphs: [new(TheTrickster.GlyphId, rank)]);
        f.FillResources(); // enough to cast it

        f.Cast(power);

        Assert.Equal(critChance, f.CritChance(), 1e-9);
    }

    /// <summary>
    /// The Trickster's crit chance lasts 4 seconds, measured from the cast start.
    /// </summary>
    [Fact]
    public void The_Tricksters_Crit_Chance_Lasts_4_Seconds()
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheTrickster.GlyphId, 4)]);
        f.FillResources(); // enough to cast it

        f.Cast("Glacial Blast");
        double start = f.CastsStarted("Glacial Blast").Last().Time.TotalSeconds;

        using var check = new Checks(f);
        f.WaitUntil(start + 4 - 0.1);
        check.Equal(0.10, f.CritChance());
        f.WaitUntil(start + 4 + 0.1);
        check.Equal(0, f.CritChance());
    }

    /// <summary>
    /// Only Power abilities give The Trickster's crit chance: your Basic, Core, Major, ultimate and Control abilities do not.
    /// </summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Bursting Ice")]
    [InlineData("Ice Blitz")]
    [InlineData("Wrath of Winter")]
    [InlineData("Frigid Winds")]
    public void Only_Power_Abilities_Give_The_Tricksters_Crit_Chance(string ability)
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheTrickster.GlyphId, 4)]);
        f.FillResources(); // enough to cast it

        f.Cast(ability);

        Assert.Equal(0, f.CritChance());
    }

    /// <summary>A second Power ability refreshes The Trickster's 4 seconds: the crit chance does not stack.</summary>
    [Fact]
    public void A_Second_Power_Ability_Refreshes_The_Trickster()
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheTrickster.GlyphId, 4)]);
        f.FillResources(); // enough to cast them

        f.Cast("Ice Comet");
        f.Cast("Glacial Blast");
        double start = f.CastsStarted("Glacial Blast").Last().Time.TotalSeconds;

        using var check = new Checks(f);
        check.Equal(0.10, f.CritChance());
        f.WaitUntil(start + 4 - 0.1);
        check.Equal(0.10, f.CritChance());
        f.WaitUntil(start + 4 + 0.1);
        check.Equal(0, f.CritChance());
    }
}
