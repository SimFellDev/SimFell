using FellowshipSim.Core.APL;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq.Abilities;
using FellowshipSim.Core.Import;
using Xunit.Abstractions;

namespace FellowshipSim.Tests;

/// <summary>A sim's damage sources under another name than the ability dealing them are part of that ability.</summary>
public class AbilityGroupTests(ITestOutputHelper output)
{
    [Fact]
    public void Thunder_Call_Hits_Are_Part_Of_Their_Ability()
    {
        var build = new CharacterBuild { Hero = "Tariq", PrimaryStat = 1000, CritRating = 400 };
        var result = new MonteCarloSimulator(build, AplParser.ParseScript(DefaultActionLists.Read("Tariq", DefaultActionLists.DefaultName)!))
            .Run(SimTime.FromSeconds(120), 20, 3, degreeOfParallelism: 2);
        foreach (var a in result.Abilities) output.WriteLine($"{a.Name} ({a.Group ?? "-"}): {a.Damage:N0}, {a.Casts:N1} casts");

        var lightning = result.Abilities.Single(a => a.Name == HammerStorm.LightningName);
        Assert.Equal(HammerStorm.AbilityName, lightning.Group);
        Assert.Contains(result.Abilities, a => a.Name == HammerStorm.AbilityName && a.Group == null && a.Casts > 0);
        Assert.All(result.Abilities.Where(a => a.Group != null), a => Assert.Contains(result.Abilities, p => p.Name == a.Group));
    }
}
