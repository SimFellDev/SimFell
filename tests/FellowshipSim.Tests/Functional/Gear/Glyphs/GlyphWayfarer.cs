using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Wayfarer (glyph 703): "Every 60 seconds you gain The Wayfarer, granting you +6% / +10% / +16% / +25% Haste for 14 seconds. Each time
/// you use your Core ability, the time to The Wayfarer's next application is reduced by 0.2 to 8 seconds, relative to the cooldown of the
/// Core ability used. Abilities with no cooldown provide 0.2 seconds reduction" (ranks 1 to 4). The first application is at the start of
/// the fight.
/// </summary>
public class GlyphWayfarer
{
    /// <summary>The Wayfarer gives you its haste from your first ability: 6% / 10% / 16% / 25%.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.06)]
    [InlineData("Rime", "Frost Bolt", 2, 0.10)]
    [InlineData("Rime", "Frost Bolt", 3, 0.16)]
    [InlineData("Rime", "Frost Bolt", 4, 0.25)]
    [InlineData("Ardeos", "Infernal Wave", 4, 0.25)]
    [InlineData("Elarion", "Focused Shot", 4, 0.25)]
    [InlineData("Gunde", "Double Strike", 4, 0.25)]
    [InlineData("Mara", "Skittering Blades", 4, 0.25)]
    [InlineData("Tariq", "Wild Swing", 4, 0.25)]
    public void The_Wayfarer_Gives_Haste_From_Your_First_Ability(string hero, string basic, int rank, double haste)
    {
        var f = StepFights.Of(hero, glyphs: [new(TheWayfarer.GlyphId, rank)]);

        f.Cast(basic);

        using var check = new Checks(f);
        check.True(f.Has(TheWayfarer.Buff));
        check.Equal(haste, f.Haste());
    }

    /// <summary>The Wayfarer lasts 14 seconds.</summary>
    [Fact]
    public void The_Wayfarer_Lasts_14_Seconds()
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheWayfarer.GlyphId, 4)]);

        f.Cast("Frost Bolt");

        f.WaitUntil(14 - 0.1);
        Assert.True(f.Has(TheWayfarer.Buff));
        f.WaitUntil(14 + 0.1);
        Assert.False(f.Has(TheWayfarer.Buff));
    }

    /// <summary>The Wayfarer comes again 60 seconds after the first.</summary>
    [Fact]
    public void The_Wayfarer_Comes_Again_Every_60_Seconds()
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheWayfarer.GlyphId, 4)]);

        f.Cast("Frost Bolt");

        f.WaitUntil(60 - 0.1);
        Assert.False(f.Has(TheWayfarer.Buff));
        f.WaitUntil(60 + 0.1);
        Assert.True(f.Has(TheWayfarer.Buff));
    }

    /// <summary>
    /// Your Core ability brings The Wayfarer's next application closer by its cooldown x 4 / 30, from 0.2 to 8 seconds: 0.2 seconds for one
    /// with no cooldown.
    ///
    /// The 4 / 30 is from the game data, and combat logs (each cycle is 60 seconds less 1.333 per Bursting Ice).
    /// </summary>
    [Theory]
    [InlineData("Rime", "Bursting Ice", 10.0)]
    [InlineData("Ardeos", "Fire Ball", 30.0)]
    [InlineData("Elarion", "Multishot", 0.0)]
    [InlineData("Gunde", "Blood Arc", 9.0)]
    [InlineData("Mara", "Widow's Bite", 9.0)]
    [InlineData("Tariq", "Heavy Strike", 8.0)]
    public void A_Core_Ability_Brings_The_Next_Wayfarer_Closer(string hero, string core, double cooldown)
    {
        var f = StepFights.Of(hero, glyphs: [new(TheWayfarer.GlyphId, 1)]);
        f.FillResources(); // enough to cast it

        f.Cast(core);
        double next = 60 - Math.Clamp(cooldown * 4 / 30, 0.2, 8);

        f.WaitUntil(next - 0.1);
        Assert.False(f.Has(TheWayfarer.Buff));
        f.WaitUntil(next + 0.1);
        Assert.True(f.Has(TheWayfarer.Buff));
    }

    /// <summary>
    /// Only your Core ability brings The Wayfarer closer: after a Power, Major or Control ability it comes again at 60 seconds.
    /// </summary>
    [Theory]
    [InlineData("Ice Comet")]
    [InlineData("Ice Blitz")]
    [InlineData("Frigid Winds")]
    public void Only_Core_Abilities_Bring_The_Next_Wayfarer_Closer(string ability)
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheWayfarer.GlyphId, 1)]);
        f.FillResources(); // enough to cast it

        f.Cast(ability);

        f.WaitUntil(60 - 0.1);
        Assert.False(f.Has(TheWayfarer.Buff));
        f.WaitUntil(60 + 0.1);
        Assert.True(f.Has(TheWayfarer.Buff));
    }

    /// <summary>Each Core cast counts: two Bursting Ice bring The Wayfarer twice as close.</summary>
    [Fact]
    public void Each_Core_Cast_Brings_The_Next_Wayfarer_Closer()
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheWayfarer.GlyphId, 1)]);

        f.Cast("Bursting Ice");
        f.ResetCooldown<BurstingIce>(); // ready again
        f.Cast("Bursting Ice");
        double next = 60 - 2 * 10.0 * 4 / 30;

        f.WaitUntil(next - 0.1);
        Assert.False(f.Has(TheWayfarer.Buff));
        f.WaitUntil(next + 0.1);
        Assert.True(f.Has(TheWayfarer.Buff));
    }
}
