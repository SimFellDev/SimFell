using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Subduer (glyph 704): "You deal 5% / 10% / 15% / 20% more damage to Low Health Enemies" (ranks 1 to 4); low health is 30% or less.
/// Enemy health is set as a fraction of its maximum.
///
/// The tooltip's second part, Major abilities reducing a Control ability's cooldown, is not in the engine and not tested.
/// </summary>
public class GlyphSubduer
{
    /// <summary>
    /// The first hit named <paramref name="hit"/> after <paramref name="ability"/>, on an enemy at <paramref name="health"/> of its health.
    /// </summary>
    private static double FirstHit(string hero, string ability, string hit, int rank, double health)
    {
        var f = StepFights.Of(hero, glyphs: StepFights.Ranked(TheSubduer.GlyphId, rank)).WithoutDamageSpread();
        f.FillResources(); // enough to cast it
        f.SetHealth(health);

        f.Cast(ability);
        f.Wait(8); // the hits land, and an effect ticks

        return f.Damage(hit)[0].Value;
    }

    /// <summary>The Subduer makes you deal 5% / 10% / 15% / 20% more damage to an enemy at 30% health.</summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.05)]
    [InlineData("Rime", "Frost Bolt", 2, 0.10)]
    [InlineData("Rime", "Frost Bolt", 3, 0.15)]
    [InlineData("Rime", "Frost Bolt", 4, 0.20)]
    [InlineData("Ardeos", "Infernal Wave", 4, 0.20)]
    [InlineData("Elarion", "Focused Shot", 4, 0.20)]
    [InlineData("Gunde", "Double Strike", 4, 0.20)]
    [InlineData("Mara", "Skittering Blades", 4, 0.20)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.20)]
    public void The_Subduer_Deals_More_Damage_To_An_Enemy_At_30_Percent_Health(string hero, string ability, int rank, double bonus)
    {
        double plainHit = FirstHit(hero, ability, ability, rank: 0, health: 0.30);
        double subduerHit = FirstHit(hero, ability, ability, rank, health: 0.30);

        AssertRatio(1 + bonus, subduerHit / plainHit);
    }

    /// <summary>
    /// An enemy has low health at 30% health or less: at 34% The Subduer adds nothing.
    /// </summary>
    [Theory]
    [InlineData(0.26, 1.2)]
    [InlineData(0.34, 1.0)]
    public void The_Subduer_Counts_Enemies_At_30_Percent_Health_Or_Less(double health, double ratio)
    {
        double BoltHit(int rank)
        {
            var f = StepFights.Of("Rime", glyphs: StepFights.Ranked(TheSubduer.GlyphId, rank)).WithoutDamageSpread();
            f.SetHealth(health);

            f.Cast("Frost Bolt");

            return f.Damage("Frost Bolt")[0].Value;
        }

        double plainHit = BoltHit(rank: 0);
        double subduerHit = BoltHit(rank: 4);

        AssertRatio(ratio, subduerHit / plainHit);
    }

    /// <summary>
    /// The Subduer raises all your damage to a low health enemy, whatever deals it: every ability, Control abilities and damage over time
    /// effects included.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Bursting Ice", "Bursting Ice")]
    [InlineData("Rime", "Ice Comet", "Ice Comet")]
    [InlineData("Ardeos", "Searing Blaze", "Searing Blaze")]
    [InlineData("Gunde", "Double Strike", "Rend", Skip = "Per the user: the sim may be wrong (Rend takes the Subduer twice, 1.44), not sure")]
    [InlineData("Gunde", "Butcher's Hook", "Butcher's Hook")]
    [InlineData("Mara", "Hemorrhaging Strike", "Hemorrhage")]
    public void The_Subduer_Raises_All_Your_Damage_To_A_Low_Health_Enemy(string hero, string ability, string hit)
    {
        double plainHit = FirstHit(hero, ability, hit, rank: 0, health: 0.30);
        double subduerHit = FirstHit(hero, ability, hit, rank: 4, health: 0.30);

        AssertRatio(1.2, subduerHit / plainHit);
    }
}
