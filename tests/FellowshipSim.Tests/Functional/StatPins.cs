using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gives a test hero exact percentages without making its build a character sheet.
/// Dev: a character sheet's percentages replace the stats' base and switch off every bonus that "a sheet already includes" (gem nodes' flat stats,
/// relics' Main Stat, Covetous Greed...). A test hero is not from a sheet, so the percentages are added as the stats' permanent bonuses
/// (<see cref="StatBlock.BonusHaste"/> and its siblings) on top of the base: gear still works as it would on any hero.
/// </summary>
public static class StatPins
{
    /// <summary>
    /// The build's hero with haste, crit chance, Spirit and Expertise at those fractions (0.5 is 50%) before gear: crit chance 0 is 0%, not the
    /// 5% base. A gem or relic that adds to a stat adds to the pinned percentage.
    /// </summary>
    public static Hero Import(CharacterBuild build, double haste = 0, double crit = 0, double spirit = 0, double expertise = 0)
    {
        var hero = HeroImporter.Import(build);
        hero.Stats.BonusHaste += haste;
        hero.Stats.BonusCritChance += crit - StatBlock.BaseCritChance;
        hero.Stats.BonusSpirit += spirit - StatBlock.BaseSpirit;
        hero.Stats.BonusExpertise += expertise;
        return hero;
    }
}
