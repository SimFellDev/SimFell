using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Philosopher (glyph 709): "Your Major ability grants you +0.2% Haste, Expertise and Critical Strike for every 4.0% / 2.5% / 1.6% /
/// 1.0% Spirit you have, up to 50% Spirit, for 8 seconds" (ranks 1 to 4). Spirit Refunds are forced off.
/// </summary>
public class GlyphPhilosopher
{
    /// <summary>A hero with <paramref name="spirit"/> and the glyph at <paramref name="rank"/> (none at 0).</summary>
    private static StepDriver Fight(string hero, int rank, double spirit)
    {
        var f = StepFights.Of(hero, spirit: spirit, glyphs: StepFights.Ranked(ThePhilosopher.GlyphId, rank));
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.FillResources(); // enough to cast anything
        return f;
    }

    /// <summary>
    /// Your Major ability gives you 0.2% haste, Expertise and crit chance for every 4% / 2.5% / 1.6% / 1% Spirit you have, up to 50%
    /// Spirit.
    ///
    /// Each stat is compared with the same Major ability's without the glyph, so the ability's own effects cancel.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Blitz", 1, 4.0, 0.4)]
    [InlineData("Rime", "Ice Blitz", 2, 2.5, 0.4)]
    [InlineData("Rime", "Ice Blitz", 3, 1.6, 0.4)]
    [InlineData("Rime", "Ice Blitz", 4, 1.0, 0.1)]
    [InlineData("Rime", "Ice Blitz", 4, 1.0, 0.4)]
    [InlineData("Rime", "Ice Blitz", 4, 1.0, 0.8)]
    [InlineData("Ardeos", "Apocalypse", 4, 1.0, 0.4)]
    [InlineData("Elarion", "Lunarlight Mark", 4, 1.0, 0.4)]
    [InlineData("Gunde", "Slaughter", 4, 1.0, 0.4)]
    [InlineData("Mara", "Maiden of Death", 4, 1.0, 0.4)]
    [InlineData("Tariq", "Thunder Call", 4, 1.0, 0.4)]
    public void The_Philosopher_Major_Abilities_Give_Haste_Expertise_And_Crit_Per_Spirit(string hero, string major, int rank,
        double spiritPerStep, double spirit)
    {
        var plainScenario = Fight(hero, rank: 0, spirit);
        var philosopherScenario = Fight(hero, rank, spirit);

        plainScenario.Cast(major);
        philosopherScenario.Cast(major);

        double bonus = 0.002 * Math.Min(spirit, 0.5) * 100 / spiritPerStep;

        using var check = new Checks(philosopherScenario);
        check.Equal(bonus, philosopherScenario.Haste() - plainScenario.Haste());
        check.Equal(bonus, philosopherScenario.Expertise() - plainScenario.Expertise());
        check.Equal(bonus, philosopherScenario.CritChance() - plainScenario.CritChance());
    }

    /// <summary>The Philosopher lasts 8 seconds.</summary>
    [Fact]
    public void The_Philosopher_Lasts_8_Seconds()
    {
        var f = Fight("Rime", rank: 4, spirit: 0.4);

        f.Cast("Ice Blitz");
        double start = f.CompletedAt("Ice Blitz");

        f.WaitUntil(start + 8 - 0.1);
        Assert.True(f.Has(ThePhilosopher.Buff));
        f.WaitUntil(start + 8 + 0.1);
        Assert.False(f.Has(ThePhilosopher.Buff));
    }

    /// <summary>Only your Major ability gives The Philosopher: your Basic, Core, Power, ultimate and Control abilities do not.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Bursting Ice")]
    [InlineData("Ice Comet")]
    [InlineData("Wrath of Winter")]
    [InlineData("Frigid Winds")]
    public void Only_Major_Abilities_Give_The_Philosopher(string ability)
    {
        var f = Fight("Rime", rank: 4, spirit: 0.4);

        f.Cast(ability);

        Assert.False(f.Has(ThePhilosopher.Buff));
    }

    /// <summary>
    /// A second Major ability during The Philosopher restarts its 8 seconds and keeps the time it had left, up to 130% of the base duration.
    /// Ice Blitz, then Winter's Blessing right after, lasts about 10.4 seconds.
    /// </summary>
    [Fact]
    public void The_Philosophers_Has_Pandemic_Time_Extension_On_Refresh()
    {
        var f = Fight("Rime", rank: 4, spirit: 0.4);

        f.Cast("Ice Blitz");
        double first = f.CompletedAt("Ice Blitz");
        f.Cast("Winter's Blessing");
        double second = f.CompletedAt("Winter's Blessing");
        double end = second + 8 * 1.3;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(ThePhilosopher.Buff));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(ThePhilosopher.Buff));
    }
}
