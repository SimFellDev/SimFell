using FellowshipSim.Core.Customization.CommonAbilities;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Dungeons.Affixes;
using FellowshipSim.Tests.Functional;

namespace FellowshipSim.Tests;

public class RelicPowerCooldownTests
{
    public static TheoryData<string, double, double> DiamondCases()
    {
        var cases = new TheoryData<string, double, double>();
        foreach (var (name, cooldown) in new[] { ("Bloodrite Fervor", 90.0), ("Conjure Portal", 120.0), ("Rejuvenate", 120.0) })
        {
            cases.Add(name, 0, cooldown);
            cases.Add(name, 450, cooldown * 0.92);
            cases.Add(name, 1500, cooldown * 0.76);
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(DiamondCases))]
    public void Relic_Cooldown_Is_Its_Cooldown_Times_The_Diamond_Relic_Factor(string ability, double diamondPower, double expected)
    {
        var f = StepFights.Mara(gemPower: new() { [GemColor.Diamond] = diamondPower });

        Assert.Equal(expected, CooldownAfterCast(f, ability), 6);
    }

    [Theory]
    [InlineData("Bloodrite Fervor", 90.0)]
    [InlineData("Conjure Portal", 120.0)]
    [InlineData("Rejuvenate", 120.0)]
    public void Ability_Cooldown_Reduction_Leaves_A_Relic_Cooldown_Unchanged(string ability, double cooldown)
    {
        var f = StepFights.Mara(gemPower: new() { [GemColor.Emerald] = 450 });
        DungeonAffix.ById(Ultimatum.AffixId)!.ApplyToHero(f.Hero);

        Assert.True(f.Hero.Stats.CooldownReductionPct >= 0.04 + 0.10 - 1e-9);
        Assert.Equal(cooldown, CooldownAfterCast(f, ability), 6);
    }

    private static double CooldownAfterCast(StepDriver f, string name)
    {
        var ability = f.Hero.Abilities.OfType<RelicPower>().Single(a => a.Name == name);
        var start = f.Now;
        f.Cast(ability);
        return (f.Hero.CooldownOf(ability).NextChargeAt - start).TotalSeconds;
    }
}
