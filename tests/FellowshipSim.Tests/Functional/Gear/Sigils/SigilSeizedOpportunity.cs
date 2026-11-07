using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Seized Opportunity (sigil 4): "Every 20 Critical Strikes you gain 21 / 39 / 57 / 75 Critical Strike rating for 12 seconds. You can not
/// gain stacks while Seized Opportunity is active on you" (ranks 1 to 4). A rating point is 0.16% up to 10%; beyond, diminishing returns
/// keep 98% of each point. Every cast is forced to crit; the bare hero has no crit chance, so the hero's crit chance is the sigil's.
/// </summary>
public class SigilSeizedOpportunity
{
    /// <summary>
    /// Seized Opportunity gives you 21 / 39 / 57 / 75 crit rating on your 20th critical strike, not before: 3.36% / 6.24% / 9.12% / 11.96%.
    ///
    /// Tariq casts Heavy Strike, since Wild Swing never crits; Gunde's Double Strike crits twice a cast.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.0336)]
    [InlineData("Rime", "Frost Bolt", 2, 0.0624)]
    [InlineData("Rime", "Frost Bolt", 3, 0.0912)]
    [InlineData("Rime", "Frost Bolt", 4, 0.1196)]
    [InlineData("Ardeos", "Infernal Wave", 4, 0.1196)]
    [InlineData("Elarion", "Focused Shot", 4, 0.1196)]
    [InlineData("Gunde", "Double Strike", 4, 0.1196)]
    [InlineData("Mara", "Skittering Blades", 4, 0.1196)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.1196)]
    public void Seized_Opportunity_Gives_Crit_Rating_On_The_20th_Critical_Strike(string hero, string ability, int rank, double critChance)
    {
        var f = StepFights.Of(hero, augmentations: [new(SeizedOpportunity.SigilId, rank)]);

        using var check = new Checks(f);
        for (int cast = 0; cast < 50 && f.Crits().Count < 20; cast++)
        {
            check.Equal(0, f.CritChance(), what: $"crit chance after {f.Crits().Count} crits");
            f.FillResources(); // enough to cast it
            f.CastWhenReady(ability, crit: true);
            f.Wait(1); // the hits land
        }
        check.Equal(20, f.Crits().Count, what: "crits, the 20th the last");
        check.Equal(critChance, f.CritChance());
    }

    /// <summary>Seized Opportunity lasts 12 seconds.</summary>
    [Fact]
    public void Seized_Opportunity_Lasts_12_Seconds()
    {
        var f = StepFights.Of("Rime", augmentations: [new(SeizedOpportunity.SigilId, 1)]);

        for (int cast = 0; cast < 20; cast++) f.Cast("Frost Bolt", crit: true);
        f.Wait(1); // the 20th bolt lands
        double start = f.Damage("Frost Bolt")[19].Time.TotalSeconds;

        f.WaitUntil(start + 12 - 0.1);
        Assert.Equal(0.0336, f.CritChance(), 1e-9);
        f.WaitUntil(start + 12 + 0.1);
        Assert.Equal(0, f.CritChance(), 1e-9);
    }

    /// <summary>Only critical strikes count toward Seized Opportunity, and none count while it is up.</summary>
    [Fact]
    public void Only_Critical_Strikes_Outside_Seized_Opportunity_Count()
    {
        var f = StepFights.Of("Rime", augmentations: [new(SeizedOpportunity.SigilId, 1)]);

        for (int cast = 0; cast < 3; cast++) f.Cast("Frost Bolt", crit: false);
        f.Wait(1); // the bolts land

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(SeizedOpportunity.Counter));
        for (int cast = 0; cast < 20; cast++) f.Cast("Frost Bolt", crit: true);
        f.Wait(1); // the 20th crit lands
        for (int cast = 0; cast < 3; cast++) f.Cast("Frost Bolt", crit: true);
        f.Wait(1); // the bolts land
        check.Equal(0, f.Stacks(SeizedOpportunity.Counter));
    }
}
