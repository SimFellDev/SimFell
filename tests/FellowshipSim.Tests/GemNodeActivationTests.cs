using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// A node is granted at every rise of the power to its requirement and removed at every fall below it, for all six colours.
/// Each colour's first node goes on, off and on again here. <see cref="TopazGemPowerTests"/> covers Topaz's Essence of the Virtuoso.
/// </summary>
public class GemNodeActivationTests
{
    private const double FirstNodePower = 80;
    private static readonly SimTime Rise = SimTime.FromSeconds(1), Fall = SimTime.FromSeconds(2), SecondRise = SimTime.FromSeconds(3);

    [Fact]
    public void Ruby_Might_Of_The_Minotaur_Follows_Each_Crossing()
    {
        var (hero, _, gem) = HeroWithGem(GemColor.Ruby);
        var might = Ruby.AllNodes[0].Grants!;

        Assert.Equal([true, false, true], Crossings(gem, now => hero.Auras.HasAura(might, now)));
    }

    [Fact]
    public void Amethyst_Sealed_Fate_Follows_Each_Crossing()
    {
        var (hero, ctx, gem) = HeroWithGem(GemColor.Amethyst);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        double without = hero.CritChanceAgainst(enemy, SimTime.Zero);

        Assert.Equal([true, false, true], Crossings(gem, now => hero.CritChanceAgainst(enemy, now) > without));
    }

    [Fact]
    public void Topaz_Adrenaline_Rush_Follows_Each_Crossing()
    {
        var (hero, ctx, gem) = HeroWithGem(GemColor.Topaz);
        var rush = Topaz.AllNodes[0].Grants!;
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e12);
        enemy.CurrentHealth = enemy.MaxHealth * 0.1;

        Assert.Equal([true, false, true], Crossings(gem, now =>
        {
            ctx.RunUntil(now);
            hero.Auras.Remove(rush, now);
            ctx.ApplyDamage(hero, enemy, 1.0, 1, "Test", canCrit: false, 0.0, 1.0);
            return hero.Auras.HasAura(rush, now);
        }));
    }

    [Fact]
    public void Emerald_First_Strike_Follows_Each_Crossing()
    {
        var (_, _, gem) = HeroWithGem(GemColor.Emerald);

        Assert.Equal([true, false, true], Crossings(gem, _ => ((Emerald)gem).FirstStrike != null));
    }

    [Fact]
    public void Sapphire_Ancestral_Surge_Follows_Each_Crossing()
    {
        var (hero, _, gem) = HeroWithGem(GemColor.Sapphire);
        double without = hero.Resources.Get(Hero.SpiritPoints).Max;

        Assert.Equal([true, false, true], Crossings(gem, _ => hero.Resources.Get(Hero.SpiritPoints).Max > without));
    }

    [Fact]
    public void Diamond_Harmonious_Soul_Follows_Each_Crossing()
    {
        var (_, _, gem) = HeroWithGem(GemColor.Diamond);

        Assert.Equal([true, false, true], Crossings(gem, _ => ((Diamond)gem).HarmoniousSoul != null));
    }

    /// <summary>Every modeled number of a colour's rank I nodes comes and goes with the power, and the second rise matches the first.</summary>
    [Theory]
    [InlineData(GemColor.Ruby)]
    [InlineData(GemColor.Amethyst)]
    [InlineData(GemColor.Topaz)]
    [InlineData(GemColor.Emerald)]
    [InlineData(GemColor.Sapphire)]
    [InlineData(GemColor.Diamond)]
    public void Every_Rank_One_Node_Follows_Each_Crossing(GemColor color)
    {
        const double allRankOneNodes = 450;
        var (hero, _, gem) = HeroWithGem(color);
        double[] Numbers(SimTime now) =>
        [
            hero.PrimaryAttribute(now), hero.CritChance(now), hero.CritMultiplier(now), hero.GetHaste(now), hero.Expertise(now),
            hero.Stats.CooldownReductionPct, hero.Resources.Get(Hero.SpiritPoints).Max,
        ];
        double[] off = Numbers(SimTime.Zero);

        gem.ChangePower(allRankOneNodes, Rise);
        double[] on = Numbers(Rise);
        gem.ChangePower(0, Fall);
        double[] offAgain = Numbers(Fall);
        gem.ChangePower(allRankOneNodes, SecondRise);
        double[] onAgain = Numbers(SecondRise);

        Assert.False(on.SequenceEqual(off));
        Assert.Equal(off, offAgain, new RoundedEquality());
        Assert.Equal(on, onAgain, new RoundedEquality());
    }

    private sealed class RoundedEquality : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;
        public int GetHashCode(double value) => 0;
    }

    /// <summary>A second rise of the power adds no second handler: Sealed Fate's bonus counts once.</summary>
    [Fact]
    public void A_Second_Rise_Binds_No_Second_Handler()
    {
        var (hero, ctx, gem) = HeroWithGem(GemColor.Amethyst);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e12);

        gem.ChangePower(FirstNodePower, Rise);
        double first = hero.CritChanceAgainst(enemy, Rise);
        gem.ChangePower(0, Fall);
        gem.ChangePower(FirstNodePower, SecondRise);

        Assert.Equal(first, hero.CritChanceAgainst(enemy, SecondRise), 9);
    }

    private static bool[] Crossings(Gem gem, Func<SimTime, bool> nodeOn)
    {
        gem.ChangePower(FirstNodePower, Rise);
        bool afterRise = nodeOn(Rise);
        gem.ChangePower(0, Fall);
        bool afterFall = nodeOn(Fall);
        gem.ChangePower(FirstNodePower, SecondRise);
        return [afterRise, afterFall, nodeOn(SecondRise)];
    }

    private static (Hero Hero, SimulationContext Ctx, Gem Gem) HeroWithGem(GemColor color)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var gem = Gem.Create(color);
        hero.Gems.Add(gem);
        gem.Bind(hero);
        return (hero, ctx, gem);
    }
}
