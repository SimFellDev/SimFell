using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Diamond Strike (aspect 33): "Your Damage has a chance to conjure a Spectral Beam from above, dealing 72% / 127% / 182% / 237% Main Stat
/// damage to them. Diamond Strike applies an amplifying effect for 20 seconds on targets it strikes, increasing its damage to that target
/// by 40%, stacking up to 5 times. Diamond Strike gains an additional +35% increased damage for every stack of Harmonious Soul you have
/// active" (ranks 1 to 4). The chance is forced.
/// </summary>
public class AspectDiamondStrike
{
    private const string Beam = "Diamond Strike";

    /// <summary>
    /// A Diamond Strike beam deals 72% / 127% / 182% / 237% of your main stat, times your class's balancing aura (Rime and Mara 1.04,
    /// Ardeos 1.1, Elarion 1.08, Gunde and Tariq 1.0).
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.72, 1.04)]
    [InlineData("Rime", "Frost Bolt", 2, 1.27, 1.04)]
    [InlineData("Rime", "Frost Bolt", 3, 1.82, 1.04)]
    [InlineData("Rime", "Frost Bolt", 4, 2.37, 1.04)]
    [InlineData("Ardeos", "Infernal Wave", 4, 2.37, 1.1)]
    [InlineData("Elarion", "Focused Shot", 4, 2.37, 1.08)]
    [InlineData("Gunde", "Double Strike", 4, 2.37, 1.0)]
    [InlineData("Mara", "Skittering Blades", 4, 2.37, 1.04)]
    [InlineData("Tariq", "Heavy Strike", 4, 2.37, 1.0)]
    public void A_Diamond_Strike_Beam_Deals_A_Share_Of_Main_Stat(string hero, string ability, int rank, double coefficient,
        double balancing)
    {
        var f = StepFights.Of(hero, augmentations: [new(DiamondStrike.AspectId, rank)]).WithoutDamageSpread();
        f.ForceProcs(key => key == DiamondStrike.ProcKey ? true : null);

        f.Cast(ability);
        f.Wait(1); // the hits land

        AssertRatio(coefficient * balancing, f.Damage(Beam)[0].Value / f.MainStat());
    }

    /// <summary>
    /// Each beam on an enemy makes the next deal 40% more, up to 5 stacks: the 2nd deals 140% of the first, the 6th and after 300%.
    /// </summary>
    [Fact]
    public void Each_Diamond_Strike_Beam_Amplifies_The_Next_By_40_Percent_Up_To_5_Stacks()
    {
        var f = StepFights.Of("Rime", augmentations: [new(DiamondStrike.AspectId, 1)]).WithoutDamageSpread();
        f.ForceProcs(key => key == DiamondStrike.ProcKey ? true : null);

        for (int cast = 0; cast < 8; cast++) f.Cast("Frost Bolt");
        f.Wait(1); // the last bolt lands

        var beams = f.Damage(Beam);

        using var check = new Checks(f);
        check.True(beams.Count >= 8, "at least one beam per bolt");
        check.Equal(f.DamageBetween(except: Beam).Count, beams.Count, what: "one beam per hit, the bolts' and the others'");
        for (int i = 1; i < beams.Count; i++) check.Ratio(1 + 0.4 * Math.Min(i, 5), beams[i].Value / beams[0].Value, what: $"beam {i + 1}");
    }

    /// <summary>The amplification lasts 20 seconds from the last beam.</summary>
    [Fact]
    public void Diamond_Strikes_Amplification_Lasts_20_Seconds()
    {
        var f = StepFights.Of("Rime", augmentations: [new(DiamondStrike.AspectId, 1)]);
        int rolls = 0;
        f.ForceProcs(key => key == DiamondStrike.ProcKey ? rolls++ == 0 : null); // one beam

        f.Cast("Frost Bolt");
        double start = f.Damage(Beam).Single().Time.TotalSeconds;

        f.WaitUntil(start + 20 - 0.1);
        Assert.True(f.Has(DiamondStrike.Amplified, on: f.Enemy));
        f.WaitUntil(start + 20 + 0.1);
        Assert.False(f.Has(DiamondStrike.Amplified, on: f.Enemy));
    }

    /// <summary>The amplification is on the enemy the beam struck: another enemy's first beam is not amplified.</summary>
    [Fact]
    public void Diamond_Strikes_Amplification_Is_On_One_Enemy()
    {
        var f = StepFights.Of("Rime", enemies: 2, augmentations: [new(DiamondStrike.AspectId, 1)]).WithoutDamageSpread();
        f.ForceProcs(key => key == DiamondStrike.ProcKey ? true : null);

        for (int cast = 0; cast < 3; cast++) f.Cast("Frost Bolt", target: f.Enemies()[0]);
        f.Cast("Frost Bolt", target: f.Enemies()[1]);
        f.Wait(1); // the bolt lands

        var firstBeamOnEachEnemy = f.FirstHitOnEachEnemy(Beam);

        AssertRatio(1.0, firstBeamOnEachEnemy[1] / firstBeamOnEachEnemy[0]);
    }

    /// <summary>
    /// Each stack of the Diamond gem's Harmonious Soul makes a beam deal 35% more.
    ///
    /// Harmonious Soul also gives 0.6% Expertise a stack, which raises the beam like any hit: (1 + 0.35 x stacks) x (1 + 0.006 x stacks).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Each_Harmonious_Soul_Stack_Adds_35_Percent_To_Diamond_Strike(int stacks)
    {
        double FirstBeam(int stacks)
        {
            var f = StepFights.Of("Rime", enemies: 1 + stacks, augmentations: [new(DiamondStrike.AspectId, 1)],
                gemPower: new() { [GemColor.Diamond] = 100 }).WithoutDamageSpread();
            f.ForceProcs(key => key == DiamondStrike.ProcKey ? true : null);

            foreach (var enemy in f.Enemies().Skip(1).ToList()) f.Defeat(enemy); // a Harmonious Soul stack each
            f.Cast("Frost Bolt");

            return f.Damage(Beam)[0].Value;
        }

        AssertRatio((1 + 0.35 * stacks) * (1 + 0.006 * stacks), FirstBeam(stacks) / FirstBeam(stacks: 0));
    }

    /// <summary>
    /// Diamond Strike's amplification multiplies with its Harmonious Soul bonus: with 2 Harmonious Soul stacks, the second beam on an enemy
    /// still deals 1.4 times the first.
    /// </summary>
    [Fact]
    public void Diamond_Strikes_Amplification_Multiplies_With_Its_Harmonious_Soul_Bonus()
    {
        var f = StepFights.Of("Rime", enemies: 3, augmentations: [new(DiamondStrike.AspectId, 1)],
            gemPower: new() { [GemColor.Diamond] = 100 }).WithoutDamageSpread();
        f.ForceProcs(key => key == DiamondStrike.ProcKey ? true : null);

        foreach (var enemy in f.Enemies().Skip(1).ToList()) f.Defeat(enemy); // a Harmonious Soul stack each
        f.Cast("Frost Bolt");
        f.Cast("Frost Bolt");
        f.Wait(1); // the bolt lands

        var beams = f.Damage(Beam);

        AssertRatio(1.4, beams[1].Value / beams[0].Value);
    }
}
