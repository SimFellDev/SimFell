using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The gear fixture itself: every hero builds with a loadout and every probe ability casts.
/// Dev: not a game rule; if these fail, the gear tests built on <see cref="GearFights"/> mean nothing.
/// </summary>
public class GearFixtureTests
{
    public static IEnumerable<object[]> Heroes => GearHero.All;

    /// <summary>Player: every hero has one ability of each category and can cast it. Dev: each probe is cast once on a fresh hero.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Every_Probe_Casts_And_Has_The_Category_It_Stands_For(GearHero hero)
    {
        foreach (var (type, category) in new[]
        {
            (hero.Basic, AbilityCategory.Basic), (hero.Core, AbilityCategory.Core), (hero.Power, AbilityCategory.Power),
            (hero.Major, AbilityCategory.Major), (hero.Spirit, AbilityCategory.Spirit),
        })
        {
            var f = hero.Fight();
            Assert.Equal(category, hero.Get(f, type).Category);
            hero.CastProbe(f, type);
            Assert.Contains(f.Events, e => e.Kind == StepEventKind.CastCompleted);
        }
    }

    /// <summary>Player: the unit hit does damage. Dev: the ratio base of the gear tests.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void The_Unit_Hit_Deals_Damage(GearHero hero)
    {
        var f = hero.Fight();
        hero.CastProbe(f, hero.Unit);
        Assert.True(f.Events.Any(e => e.Kind == StepEventKind.Damage && e.Value > 0));
    }

    /// <summary>Player: a loadout's glyph is on the hero. Dev: The Sinister, rank 1.</summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void A_Loadout_Puts_Its_Glyph_On_The_Hero(GearHero hero)
    {
        var f = hero.Fight(new GearLoadout { Glyphs = [(TheSinister.GlyphId, 1)] });
        Assert.Single(f.Hero.Glyphs);
    }
}
