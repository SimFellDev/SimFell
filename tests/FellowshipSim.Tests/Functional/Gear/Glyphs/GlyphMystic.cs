using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Mystic (glyph 706): "Your Power abilities gain 0.3% / 0.5% / 0.8% / 1.2% increased power for every 3% Spirit you have, up to 50%
/// Spirit" (ranks 1 to 4). A hit with the glyph is compared with the same hit without it, at the same Spirit; Spirit Refunds are forced
/// off.
/// </summary>
public class GlyphMystic
{
    /// <summary>The first hit of <paramref name="ability"/>, with <paramref name="spirit"/>, with or without the glyph.</summary>
    private static double FirstHit(string hero, string ability, int rank, double spirit)
    {
        var f = StepFights.Of(hero, spirit: spirit, glyphs: StepFights.Ranked(TheMystic.GlyphId, rank)).WithoutDamageSpread();
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        f.FillResources(); // enough to cast it

        if (ability == "Detonate") f.Cast("Searing Blaze"); // Detonate only hits a burning enemy
        f.Cast(ability);
        f.Wait(3); // the hits land

        return f.Damage(ability)[0].Value;
    }

    /// <summary>
    /// Your Power abilities deal more by The Mystic's power for every 3% Spirit you have, up to 50% Spirit: x (1 + power x Spirit / 3%).
    ///
    /// TODO: confirm in the game that Ardeos's Detonate, a Power ability, gets it. The engine gives it nothing: Detonate deals its effects'
    /// damage per second, which no Power multiplier reaches.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Ice Comet", 1, 0.003, 0.3)]
    [InlineData("Rime", "Ice Comet", 2, 0.005, 0.3)]
    [InlineData("Rime", "Ice Comet", 3, 0.008, 0.3)]
    [InlineData("Rime", "Ice Comet", 4, 0.012, 0.09)]
    [InlineData("Rime", "Ice Comet", 4, 0.012, 0.3)]
    [InlineData("Rime", "Ice Comet", 4, 0.012, 0.5)]
    [InlineData("Rime", "Ice Comet", 4, 0.012, 0.8)]
    [InlineData("Ardeos", "Detonate", 4, 0.012, 0.3)]
    [InlineData("Elarion", "Highwind Arrow", 4, 0.012, 0.3)]
    [InlineData("Gunde", "Rupture", 4, 0.012, 0.3)]
    [InlineData("Mara", "Queen's Fang", 4, 0.012, 0.3)]
    [InlineData("Tariq", "Skull Crusher", 4, 0.012, 0.3)]
    public void The_Mystic_Raises_Power_Abilities_For_Every_3_Percent_Spirit(string hero, string power, int rank, double powerPerStep,
        double spirit)
    {
        double plainHit = FirstHit(hero, power, rank: 0, spirit);
        double mysticHit = FirstHit(hero, power, rank, spirit);

        AssertRatio(1 + powerPerStep * Math.Min(spirit, 0.5) * 100 / 3, mysticHit / plainHit);
    }

    /// <summary>The Mystic leaves your other abilities alone.</summary>
    [Theory]
    [InlineData("Frost Bolt")]
    [InlineData("Bursting Ice")]
    public void The_Mystic_Leaves_Other_Abilities_Alone(string ability)
    {
        double plainHit = FirstHit("Rime", ability, rank: 0, spirit: 0.3);
        double mysticHit = FirstHit("Rime", ability, rank: 4, spirit: 0.3);

        AssertRatio(1.0, mysticHit / plainHit);
    }
}
