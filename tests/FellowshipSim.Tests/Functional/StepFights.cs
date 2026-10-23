using FellowshipSim.Core;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Functional;

/// <summary>Builds the heroes for functional tests: no talents, gear or Spirit (so no Spirit Refund) and no haste, unless a test asks.</summary>
public static class StepFights
{
    public static StepDriver Rime(double hasteRating = 0, int seed = 1)
    {
        var hero = HeroImporter.Import(new CharacterBuild { Name = "Test Rime", Hero = "Rime", PrimaryStat = 1000, HasteRating = hasteRating });
        return new StepDriver(hero, seed);
    }

    public static Core.Heroes.Rime.Rime RimeOf(this StepDriver driver) => (Core.Heroes.Rime.Rime)driver.Hero;
}
