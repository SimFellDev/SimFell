using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Build a test hero with specific values of haste, crit, Spirit and Expertise, as fractions (0.5 is 50%).
///
/// The values are added to the stats' permanent bonuses (<see cref="StatBlock.BonusHaste"/> and the others), and the 5% base crit chance
/// every hero has is taken off, so <c>crit: 0</c> means 0%. Gear still adds its own stats on top: a test with gear that adds crit reads the
/// hero's crit chance (<c>f.CritChance()</c>) instead of assuming the value it set.
/// </summary>
public static class StatPins
{
    /// <summary>
    /// Imports the build's hero, then sets its haste, crit, Spirit and Expertise to these fractions before gear (<c>crit: 0</c> is 0%, not the
    /// 5% base). A gem or relic that adds to a stat adds to the value set here.
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
