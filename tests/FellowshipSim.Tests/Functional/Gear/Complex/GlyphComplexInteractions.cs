using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// Glyphs that read what another glyph changes: The Monarch's haste bonus follows the haste The Philosopher grants, and The Vehement's eruption carries
/// The Subduer's bonus because it repeats a hit that already has it.
/// </summary>
public class GlyphComplexInteractionsTests
{
    /// <summary>
    /// Player: The Monarch's added acceleration (+0.1% for each 1% haste) counts the haste The Philosopher gives you: after a Major ability with The Philosopher
    /// at rank 4 and 50% Spirit (+10% haste), your next Major ability has 1% more acceleration than without The Philosopher.
    /// Dev: the acceleration of the hero's second Major ability right after the first one is cast (with the hero's other Major abilities' own effects on haste
    /// the same in both fights, so the difference is The Philosopher's 10% haste x 0.1).
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames), MemberType = typeof(GlyphFights))]
    public void The_Monarchs_Haste_Bonus_Counts_The_Philosophers_Haste(string hero)
    {
        double Acceleration((int, int)[] glyphs)
        {
            var f = FightWith(hero, glyphs, spirit: 0.5);
            var majors = f.Hero.Abilities.Where(a => a.Category == AbilityCategory.Major).ToList();
            GearFights.Fill(f);
            f.Cast(Hero(hero).Get(f, Hero(hero).Major));
            var other = majors.First(a => a != Hero(hero).Get(f, Hero(hero).Major));
            return f.Hero.CooldownAcceleration(other, f.Now);
        }
        double monarch = Acceleration([(Monarch, 4)]);
        double both = Acceleration([(Monarch, 4), (Philosopher, 4)]);
        Assert.Equal(0.001 * 10, both - monarch, 1e-9);
    }

    /// <summary>
    /// Player: The Vehement's eruption on an enemy at 30% health or less carries The Subduer's bonus (it repeats a hit that has it): 20% more at rank 4, and not
    /// more than that.
    /// Dev: Rime and Elarion (single target Basic abilities), both glyphs at rank 4, enemy at 20%, no crit; the 3rd cast's eruption with the Subduer over the one without,
    /// and the eruption over its own hit (1.0).
    /// TODO: check in game that the eruption counts the bonus once (the engine repeats a hit that has it already).
    /// </summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Elarion")]
    public void The_Eruption_Carries_The_Subduers_Bonus_Once(string hero)
    {
        (double Eruption, double Hit) Third((int, int)[] glyphs)
        {
            var f = FightWith(hero, glyphs);
            f.SetEnemyHealth(20_000_000);
            var basic = Hero(hero).Get(f, Hero(hero).Basic);
            for (int i = 0; i < 3; i++)
            {
                f.Cast(basic);
                f.Wait(1);
            }
            var eruption = f.Events.Single(e => e.Kind == StepEventKind.Damage && e.Name == "The Vehement's Disdain");
            var hit = f.Events.Last(e => e.Kind == StepEventKind.Damage && e.Name == basic.Name);
            return (eruption.Value, hit.Value);
        }
        var vehement = Third([(Vehement, 4)]);
        var both = Third([(Vehement, 4), (Subduer, 4)]);
        Assert.Equal(1.2, both.Eruption / vehement.Eruption, 1e-9);
        Assert.Equal(1.0, both.Eruption / both.Hit, 1e-9);
    }
}
