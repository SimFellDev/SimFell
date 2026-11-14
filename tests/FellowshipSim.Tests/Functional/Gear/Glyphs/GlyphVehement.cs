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
    /// The Vehement erupts on every 6th / 5th / 4th / 3rd Basic ability you cast, and counts again from there. A cast counts once,
    /// however many hits it has: every Basic ability of every hero, on 3 enemies, so the area and multi-hit ones (Gunde's Double
    /// Strike hits twice per cast, Mara's Skittering Blades every enemy) count once per cast too.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 6)]
    [InlineData("Rime", "Frost Bolt", 2, 5)]
    [InlineData("Rime", "Frost Bolt", 3, 4)]
    [InlineData("Ardeos", "Infernal Wave", 4, 3)]
    [InlineData("Elarion", "Celestial Shot", 4, 3)]
    [InlineData("Elarion", "Focused Shot", 4, 3)]
    [InlineData("Gunde", "Double Strike", 4, 3)]
    [InlineData("Gunde", "Reaver's Edge", 4, 3)]
    [InlineData("Helena", "Measured Strike", 4, 3)]
    [InlineData("Helena", "Power Strike", 4, 3)]
    [InlineData("Mara", "Backstab", 4, 3)]
    [InlineData("Mara", "Skittering Blades", 4, 3)]
    [InlineData("Mara", "Stealth Backstab", 4, 3)]
    [InlineData("Mara", "Stealth Skittering Blades", 4, 3)]
    [InlineData("Meiko", "Earth Fist", 4, 3)]
    [InlineData("Meiko", "Spirit Palm", 4, 3)]
    [InlineData("Meiko", "Wind Kick", 4, 3)]
    [InlineData("Rime", "Cold Snap", 4, 3)]
    [InlineData("Rime", "Frost Bolt", 4, 3)]
    [InlineData("Tariq", "Face Breaker", 4, 3)]
    [InlineData("Tariq", "Wild Swing", 4, 3)]
    [InlineData("Xavian", "Blinding Slash", 4, 3)]
    [InlineData("Xavian", "Sun Strike", 4, 3)]
    public void The_Vehement_Erupts_Every_Few_Basic_Abilities(string hero, string basic, int rank, int castsPerEruption)
    {
        const int Enemies = 3;
        var f = StepFights.Of(hero, enemies: Enemies, glyphs: [new(TheVehement.GlyphId, rank)]);
        f.ForceProcs(key => key == WildSwing.AvoidedProcKey ? false : null); // Tariq's Wild Swing always lands

        var erupted = new List<bool>();
        for (int cast = 1; cast <= 2 * castsPerEruption; cast++)
        {
            int before = f.Damage(Eruption).Count;
            f.FillResources(); // enough to cast it
            f.Hero.CooldownOf(f.Ability(basic)).ResetAll(); // ready again: only the count matters here
            if (basic.StartsWith("Stealth "))
            {
                f.Hero.CooldownOf(f.Ability("Brooding Shadows")).ResetAll();
                f.Cast("Brooding Shadows"); // it needs Stealth
            }
            f.Cast(basic);
            f.Wait(1); // the hits land
            erupted.Add(f.Damage(Eruption).Count > before);
        }

        var expected = Enumerable.Range(1, 2 * castsPerEruption).Select(cast => cast % castsPerEruption == 0);
        Assert.Equal(expected, erupted);
    }

    /// <summary>
    /// Every Basic ability of every hero is in <see cref="The_Vehement_Erupts_Every_Few_Basic_Abilities"/>.
    /// </summary>
    [Fact]
    public void Every_Basic_Ability_Is_Checked_For_The_Vehement()
    {
        var listed = typeof(GlyphVehement).GetMethod(nameof(The_Vehement_Erupts_Every_Few_Basic_Abilities))!
            .GetCustomAttributes(typeof(InlineDataAttribute), false).Cast<InlineDataAttribute>()
            .SelectMany(row => row.GetData(null!)).Select(row => $"{row[0]}: {row[1]}").ToHashSet();
        var basics = FellowshipSim.Core.Catalog.Heroes.Keys
            .SelectMany(hero => StepFights.Of(hero).Hero.Abilities.Where(a => a.Category == FellowshipSim.Core.Domain.AbilityCategory.Basic)
                .Select(a => $"{hero}: {a.Name}"))
            .ToHashSet();
        Assert.Empty(basics.Except(listed));
    }

    /// <summary>
    /// A Basic ability that hits several enemies counts once per cast, and its eruption repeats one hit, not their sum. At rank 4, the
    /// third Skittering Blades on 4 enemies erupts once on each enemy, for its first hit's damage (combat logs: nZwRApWB8aKPDQrk fight 3,
    /// a Skittering Blades on 11 to 15 enemies adds one stack, and its eruption is one amount on every enemy).
    /// </summary>
    [Fact]
    public void A_Cast_Hitting_Several_Enemies_Counts_Once_And_Repeats_One_Hit()
    {
        const int enemies = 4;
        var f = StepFights.Of("Mara", enemies: enemies, glyphs: [new(TheVehement.GlyphId, 4)]).WithoutDamageSpread();

        var eruptionsPerCast = new List<int>();
        for (int cast = 0; cast < 3; cast++)
        {
            int before = f.Damage(Eruption).Count;
            f.FillResources();
            f.Cast("Skittering Blades", crit: false);
            f.Wait(1);
            eruptionsPerCast.Add(f.Damage(Eruption).Count - before);
        }

        Assert.Equal([0, 0, enemies], eruptionsPerCast); // eruption hits per cast
        using var check = new Checks(f);
        check.Equal(enemies, f.Damage("Skittering Blades").Count / 3, what: "Skittering Blades hits per cast");
        var firstHitOfThird = f.Damage("Skittering Blades")[2 * enemies];
        foreach (var eruption in f.Damage(Eruption)) check.Ratio(1 + Math.Min(f.Hero.CritChance(eruption.Time), 0.5), eruption.Value / firstHitOfThird.Value);
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
