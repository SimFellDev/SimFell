using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Customization.WeaponAbilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Monarch (glyph 705): "Your Major abilities gain 3% / 5% / 8% / 12% Cooldown Acceleration. The bonus is increased by +0.1% for
/// every 1% Haste you have, up to a maximum of 5%" (ranks 1 to 4). A cooldown's recovery rate with the glyph is compared with the same
/// cooldown's without it, at the same haste.
/// </summary>
public class GlyphMonarch
{
    /// <summary>How fast <paramref name="ability"/>'s cooldown recovers, with or without the glyph.</summary>
    private static double RecoveryRate(string hero, string ability, int rank, double haste, int weaponId = 0)
    {
        var f = StepFights.Of(hero, haste: haste, weaponAbilityId: weaponId, glyphs: StepFights.Ranked(TheMonarch.GlyphId, rank));

        return f.RecoveryRate(ability);
    }

    /// <summary>
    /// Your Major abilities recover faster by The Monarch's acceleration, 3% / 5% / 8% / 12%, plus 0.1% for every 1% haste, up to 5%.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Blitz", 1, 0.03, 0.0)]
    [InlineData("Rime", "Ice Blitz", 2, 0.05, 0.0)]
    [InlineData("Rime", "Ice Blitz", 3, 0.08, 0.0)]
    [InlineData("Rime", "Ice Blitz", 4, 0.12, 0.0)]
    [InlineData("Rime", "Ice Blitz", 4, 0.12, 0.2)]
    [InlineData("Rime", "Ice Blitz", 4, 0.12, 0.5)]
    [InlineData("Rime", "Ice Blitz", 4, 0.12, 1.5)]
    [InlineData("Ardeos", "Apocalypse", 4, 0.12, 0.0)]
    [InlineData("Elarion", "Lunarlight Mark", 4, 0.12, 0.0)]
    [InlineData("Gunde", "Slaughter", 4, 0.12, 0.0)]
    [InlineData("Mara", "Maiden of Death", 4, 0.12, 0.0)]
    [InlineData("Tariq", "Thunder Call", 4, 0.12, 0.0)]
    public void The_Monarch_Speeds_Up_Major_Abilities_Cooldowns(string hero, string major, int rank, double acceleration, double haste)
    {
        double plainRate = RecoveryRate(hero, major, rank: 0, haste);
        double monarchRate = RecoveryRate(hero, major, rank, haste);

        AssertRatio(1 + acceleration + Math.Min(0.05, 0.001 * haste * 100), monarchRate / plainRate);
    }

    /// <summary>The Monarch leaves your other cooldowns alone, a weapon ability's included.</summary>
    [Theory]
    [InlineData("Bursting Ice", 0)]
    [InlineData("Frigid Winds", 0)]
    [InlineData("Chronoshift", Chronoshift.AbilityId)]
    public void The_Monarch_Leaves_Other_Cooldowns_Alone(string ability, int weaponId)
    {
        double plainRate = RecoveryRate("Rime", ability, rank: 0, haste: 0.2, weaponId);
        double monarchRate = RecoveryRate("Rime", ability, rank: 4, haste: 0.2, weaponId);

        AssertRatio(1.0, monarchRate / plainRate);
    }
}
