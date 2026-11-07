using FellowshipSim.Core.Customization.Glyphs;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// The Vehement (glyph 712): every 6 / 5 / 4 / 3 Basic abilities (ranks 1 to 4), the target erupts, dealing that hit's damage again to all
/// enemies around it, increased by your Critical Strike chance up to 50%; the eruption cannot crit. Beyond 5 enemies it loses damage as
/// sqrt(5 / enemies), from the game data and combat logs.
/// </summary>
public class GlyphVehement
{
    private const string Eruption = "The Vehement's Disdain";

    /// <summary>
    /// The Vehement erupts on every 6th / 5th / 4th / 3rd Basic ability you cast, and counts again from there.
    ///
    /// Gunde's Double Strike hits twice per cast. TODO: confirm in the game whether a cast counts once, as the tooltip says, or each hit.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 6)]
    [InlineData("Rime", "Frost Bolt", 2, 5)]
    [InlineData("Rime", "Frost Bolt", 3, 4)]
    [InlineData("Rime", "Frost Bolt", 4, 3)]
    [InlineData("Ardeos", "Infernal Wave", 4, 3)]
    [InlineData("Elarion", "Focused Shot", 4, 3)]
    [InlineData("Gunde", "Double Strike", 4, 3)]
    [InlineData("Mara", "Skittering Blades", 4, 3)]
    [InlineData("Tariq", "Wild Swing", 4, 3)]
    public void The_Vehement_Erupts_Every_Few_Basic_Abilities(string hero, string basic, int rank, int castsPerEruption)
    {
        var f = StepFights.Of(hero, glyphs: [new(TheVehement.GlyphId, rank)]);
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // Tariq's Wild Swing always lands

        var eruptionsPerCast = new List<int>();
        for (int cast = 1; cast <= 2 * castsPerEruption; cast++)
        {
            int before = f.Damage(Eruption).Count;
            f.FillResources(); // enough to cast it
            f.Cast(basic);
            f.Wait(1); // the hit lands
            eruptionsPerCast.Add(f.Damage(Eruption).Count - before);
        }

        var expected = Enumerable.Range(1, 2 * castsPerEruption).Select(cast => cast % castsPerEruption == 0 ? 1 : 0);
        Assert.Equal(expected, eruptionsPerCast);
    }

    /// <summary>
    /// The Vehement's eruption deals the hit's damage again, increased by your crit chance up to 50%, and never crits.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.8)]
    public void The_Vehements_Eruption_Repeats_The_Hit_Raised_By_Your_Crit_Chance(double crit)
    {
        var f = StepFights.Of("Rime", crit: crit, glyphs: [new(TheVehement.GlyphId, 4)]).WithoutDamageSpread();

        for (int cast = 0; cast < 3; cast++) f.Cast("Frost Bolt", crit: false);
        f.Wait(1); // the third bolt lands

        var eruption = f.Damage(Eruption).Single();

        using var check = new Checks(f);
        check.Ratio(1 + Math.Min(crit, 0.5), eruption.Value / f.Damage("Frost Bolt")[2].Value);
        check.Equal(0, eruption.CritChance);
    }

    /// <summary>
    /// The Vehement's eruption hits every enemy around the target, and beyond 5 enemies each takes sqrt(5 / enemies) of it.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(20)]
    public void The_Vehements_Eruption_Hits_Every_Enemy_For_Less_Beyond_5(int enemies)
    {
        var f = StepFights.Of("Rime", enemies: enemies, glyphs: [new(TheVehement.GlyphId, 4)]).WithoutDamageSpread();

        for (int cast = 0; cast < 3; cast++) f.Cast("Frost Bolt");
        f.Wait(1); // the third bolt lands

        var eruptions = f.FirstHitOnEachEnemy(Eruption);
        double thirdBolt = f.Damage("Frost Bolt")[2].Value;

        using var check = new Checks(f);
        check.Equal(enemies, eruptions.Count);
        foreach (double eruption in eruptions) check.Ratio(Math.Min(1, Math.Sqrt(5.0 / enemies)), eruption / thirdBolt);
    }

    /// <summary>
    /// Only Basic abilities count toward The Vehement: Power abilities do not. At rank 4, Ice Comets between three Frost Bolts leave the
    /// eruption on the third bolt.
    /// </summary>
    [Fact]
    public void Only_Basic_Abilities_Count_Toward_The_Vehement()
    {
        var f = StepFights.Of("Rime", glyphs: [new(TheVehement.GlyphId, 4)]);

        foreach (string ability in new[] { "Frost Bolt", "Ice Comet", "Frost Bolt", "Ice Comet", "Frost Bolt" })
        {
            f.FillResources(); // enough to cast it
            f.Cast(ability);
        }
        f.Wait(3); // the comets fall

        var eruption = f.Damage(Eruption).Single();

        Assert.Equal(f.Damage("Frost Bolt")[2].Time, eruption.Time);
    }
}
