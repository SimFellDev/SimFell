using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>
/// Incinerate starts while the global cooldown of an instant runs; an ability on the global cooldown does not. A sim casts
/// it then for an action with <c>use_off_gcd=1</c> alone.
/// </summary>
public class ArdeosGlobalCooldownTests
{
    [Fact]
    public void Incinerate_Starts_During_The_Global_Cooldown()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Ardeos", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Dummy", 1e9);
        hero.CurrentTarget = enemy;
        hero.GainResource(Hero.SpiritPoints, 100, ctx);

        Assert.True(ctx.BeginCast(hero, hero.GetAbility<FireBall>()!, enemy).Succeeded);
        Assert.True(hero.GlobalCooldownEndTime > ctx.CurrentTime);
        Assert.False(ctx.BeginCast(hero, hero.GetAbility<SearingBlaze>()!, enemy).Succeeded);

        Assert.True(ctx.BeginCast(hero, hero.GetAbility<Incinerate>()!, enemy).Succeeded);
        Assert.IsType<Incinerate>(hero.CurrentCast?.Ability);
    }

    [Fact]
    public void A_Sim_Casts_Incinerate_With_Use_Off_Gcd_Without_Waiting_For_The_Global_Cooldown()
    {
        var casts = FirstCasts("actions+=/fire_ball\nactions+=/incinerate,use_off_gcd=1");

        Assert.Equal([(FireBall.AbilityName, 0.0), (Incinerate.AbilityName, 0.0)], casts);
    }

    [Fact]
    public void A_Sim_Casts_Incinerate_Without_Use_Off_Gcd_Once_The_Global_Cooldown_Ends()
    {
        var casts = FirstCasts("actions+=/fire_ball,if=!prev.fire_ball\nactions+=/incinerate");

        Assert.Equal(FireBall.AbilityName, casts[0].Name);
        Assert.Equal(Incinerate.AbilityName, casts[1].Name);
        Assert.True(casts[1].Time > 0.0);
    }

    // The first two casts of a 10 s fight of the fixture build with 100 Spirit.
    private static List<(string Name, double Time)> FirstCasts(string apl)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ardeos_character.json")))!;
        var result = new MonteCarloSimulator(build, AplParser.ParseScript(apl))
            .Run(SimTime.FromSeconds(10), iterations: 1, fight: new FightOptions(StartingSpirit: 100));
        return result.Casts.Take(2).Select(c => (c.Name, c.Time)).ToList();
    }
}
