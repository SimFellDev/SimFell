using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>Topaz power from Anomalous Residue gives Essence of the Virtuoso on every activation of its node.</summary>
public class TopazGemPowerTests
{
    private const double ResiduePower = 480;
    private static readonly AuraDef Essence = Topaz.AllNodes.First(n => n.Id == 316).Grants!;

    [Fact]
    public void Each_Residue_Window_Gives_Essence_Of_The_Virtuoso()
    {
        var (hero, topaz) = HeroWithNoTopaz();

        topaz.ChangePower(ResiduePower, SimTime.FromSeconds(1));
        Assert.True(hero.Auras.HasAura(Essence, SimTime.FromSeconds(1)));

        topaz.ChangePower(0, SimTime.FromSeconds(2));
        Assert.False(hero.Auras.HasAura(Essence, SimTime.FromSeconds(2)));

        topaz.ChangePower(ResiduePower, SimTime.FromSeconds(3));
        Assert.True(hero.Auras.HasAura(Essence, SimTime.FromSeconds(3)));
    }

    [Fact]
    public void A_Residue_Window_Inside_Spirit_Of_Heroism_Gives_No_Essence()
    {
        var (hero, topaz) = HeroWithNoTopaz();

        topaz.ChangePower(ResiduePower, SimTime.FromSeconds(1));
        topaz.ChangePower(0, SimTime.FromSeconds(2));
        hero.Auras.Apply(Hero.SpiritOfHeroism, SimTime.FromSeconds(2));

        topaz.ChangePower(ResiduePower, SimTime.FromSeconds(3));
        Assert.False(hero.Auras.HasAura(Essence, SimTime.FromSeconds(3)));
    }

    private static (Hero Hero, Gem Topaz) HeroWithNoTopaz()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var topaz = Gem.Create(GemColor.Topaz);
        hero.Gems.Add(topaz);
        topaz.Bind(hero);
        return (hero, topaz);
    }
}
