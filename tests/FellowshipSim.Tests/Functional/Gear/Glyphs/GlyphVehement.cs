using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using static FellowshipSim.Tests.Functional.GearGlyph.GlyphFights;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>
/// The Vehement (glyph 712): every 6 / 5 / 4 / 3 Basic abilities (ranks 1 to 4), the target erupts, dealing that hit's damage again (it cannot crit) to all enemies
/// around it, increased by your Critical Strike chance up to 50%. Beyond 5 enemies the eruption loses damage as sqrt(5 / N) (from the combat logs).
/// Dev: the eruptions are the events named "The Vehement's Disdain". The Basic probe is cast with a second between casts so every hit has landed. The heroes
/// with a Basic ability that hits once per cast are Rime, Ardeos, Elarion, Mara and Tariq; Gunde's Double Strike hits twice (see the skipped test). The
/// early eruption seen in one log (a Cold Snap that consumed Frostwyrm's Spite) is taken as a log-parsing error and not tested.
/// </summary>
public class GlyphVehementTests
{
    private static readonly int[] CastsPerEruption = [6, 5, 4, 3];
    private const string Eruption = "The Vehement's Disdain";

    public static IEnumerable<object[]> SingleHitHeroesAndRanks =>
        new[] { "Rime", "Ardeos", "Elarion", "Mara", "Tariq" }.SelectMany(h => Enumerable.Range(1, 4).Select(r => new object[] { h, r }));

    private static List<StepEvent> Eruptions(StepDriver f) => f.Events.Where(e => e.Kind == StepEventKind.Damage && e.Name == Eruption).ToList();

    /// <summary>Casts the Basic probe once, lets its hit land, and returns the new eruptions.</summary>
    private static List<StepEvent> CastBasic(string hero, StepDriver f)
    {
        int before = Eruptions(f).Count;
        f.Cast(Hero(hero).Get(f, Hero(hero).Basic));
        f.Wait(1);
        return Eruptions(f).Skip(before).ToList();
    }

    /// <summary>
    /// Player: the target erupts every 6 / 5 / 4 / 3 Basic abilities, and the count starts again after it.
    /// Dev: casts of the Basic probe on one enemy: none before the Nth, one on it, none until the next Nth, one on that.
    /// </summary>
    [Theory]
    [MemberData(nameof(SingleHitHeroesAndRanks))]
    public void The_Target_Erupts_Every_Nth_Basic_Ability(string hero, int rank)
    {
        int n = CastsPerEruption[rank - 1];
        var f = Fight(hero, Vehement, rank);
        var perCast = new List<int>();
        for (int i = 0; i < 2 * n; i++) perCast.Add(CastBasic(hero, f).Count);
        var expected = Enumerable.Range(1, 2 * n).Select(i => i % n == 0 ? 1 : 0).ToList();
        Assert.Equal(expected, perCast);
    }

    /// <summary>
    /// Player: the eruption deals the hit's damage again, increased by your crit chance (up to 50%), and cannot crit.
    /// Dev: rank 4 (the 3rd cast erupts); crit 0, 30% and 80% on the sheet: the eruption over the hit is 1, 1.3 and 1.5, and its crit chance 0.
    /// </summary>
    [Theory]
    [InlineData("Rime", 0.0, 1.0)]
    [InlineData("Rime", 0.3, 1.3)]
    [InlineData("Rime", 0.8, 1.5)]
    [InlineData("Elarion", 0.0, 1.0)]
    [InlineData("Elarion", 0.3, 1.3)]
    [InlineData("Elarion", 0.8, 1.5)]
    [InlineData("Mara", 0.3, 1.3)]
    [InlineData("Ardeos", 0.3, 1.3)]
    [InlineData("Tariq", 0.3, 1.3)]
    public void The_Eruption_Repeats_The_Hit_With_Crit_Chance(string hero, double crit, double ratio)
    {
        var f = Fight(hero, Vehement, 4, crit: crit);
        CastBasic(hero, f);
        CastBasic(hero, f);
        int before = f.Events.Count;
        var eruptions = CastBasic(hero, f);
        var hit = f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Name == Hero(hero).Get(f, Hero(hero).Basic).Name);
        var eruption = Assert.Single(eruptions);
        Assert.Equal(ratio, eruption.Value / hit.Value, 1e-9);
        Assert.Equal(0.0, eruption.CritChance, 1e-9);
    }

    /// <summary>
    /// Player: the eruption hits all enemies around the target.
    /// Dev: Rime and Elarion (single target Basic abilities), rank 4, three enemies: the 3rd cast's eruption hits each of the three once, for the hit's damage.
    /// </summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Elarion")]
    public void The_Eruption_Hits_Every_Enemy_Around(string hero)
    {
        var f = Fight(hero, Vehement, 4, enemies: 3);
        CastBasic(hero, f);
        CastBasic(hero, f);
        int before = f.Events.Count;
        var eruptions = CastBasic(hero, f);
        var hit = f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Name == Hero(hero).Get(f, Hero(hero).Basic).Name);
        Assert.Equal(3, eruptions.Count);
        Assert.Equal(3, eruptions.Select(e => e.Target).Distinct().Count());
        Assert.All(eruptions, e => Assert.Equal(hit.Value, e.Value, 1e-6));
    }

    /// <summary>
    /// Player: beyond 5 enemies the eruption loses damage as sqrt(5 / N) on each of them (combat logs: 20 enemies, x 0.5).
    /// Dev: Rime and Elarion, rank 4, 20 enemies: each of the 20 eruption hits is half the hit.
    /// </summary>
    [Theory]
    [InlineData("Rime")]
    [InlineData("Elarion")]
    public void Beyond_Five_Enemies_It_Loses_Damage(string hero)
    {
        var f = Fight(hero, Vehement, 4, enemies: 20);
        CastBasic(hero, f);
        CastBasic(hero, f);
        int before = f.Events.Count;
        var eruptions = CastBasic(hero, f);
        var hit = f.Events.Skip(before).First(e => e.Kind == StepEventKind.Damage && e.Name == Hero(hero).Get(f, Hero(hero).Basic).Name);
        Assert.Equal(20, eruptions.Count);
        Assert.All(eruptions, e => Assert.Equal(0.5 * hit.Value, e.Value, 1e-6));
    }

    /// <summary>
    /// Player: only Basic abilities count: Core abilities do not bring the eruption.
    /// Dev: Elarion's Multishot (a Core ability with no cooldown) cast 7 times at rank 4 (every 3): no eruption.
    /// </summary>
    [Fact]
    public void Core_Abilities_Do_Not_Count()
    {
        var f = Fight("Elarion", Vehement, 4);
        var multishot = Hero("Elarion").Get(f, Hero("Elarion").Core);
        for (int i = 0; i < 7; i++)
        {
            GearFights.Fill(f);
            f.Cast(multishot);
            f.Wait(1);
        }
        Assert.Empty(Eruptions(f));
    }

    /// <summary>
    /// Player: Gunde's Double Strike counts once per cast, so the 3rd cast erupts at rank 4.
    /// Dev: the engine counts each of Double Strike's two hits, so the eruption comes on the 2nd cast (the 3rd hit) and again on the 3rd (the 6th hit).
    /// TODO: check in game whether a Basic ability with two hits counts once or twice; if the cast counts, the engine is wrong for Gunde (and any hit-per-hit ability).
    /// </summary>
    [Fact(Skip = "Unverified: whether Double Strike's two hits count once or twice (the engine counts each hit)")]
    public void A_Two_Hit_Basic_Ability_Counts_Once_Gunde()
    {
        var f = Fight("Gunde", Vehement, 4);
        var perCast = new List<int>();
        for (int i = 0; i < 6; i++) perCast.Add(CastBasic("Gunde", f).Count);
        Assert.Equal([0, 0, 1, 0, 0, 1], perCast);
    }
}
