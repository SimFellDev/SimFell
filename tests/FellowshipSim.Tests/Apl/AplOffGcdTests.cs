using FellowshipSim.Core.APL;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests.Apl;

/// <summary>
/// <c>use_off_gcd=1</c> makes an action a candidate while the global cooldown runs, and <c>use_while_casting=1</c> while a
/// cast runs. Without them, the action waits for the hold to end.
/// </summary>
public class AplOffGcdTests
{
    [Theory]
    [InlineData("ice_blitz,use_off_gcd=1", true, false)]
    [InlineData("ice_blitz,use_while_casting=1", false, true)]
    [InlineData("ice_blitz,use_off_gcd=1,use_while_casting=1,if=1", true, true)]
    [InlineData("ice_blitz,use_off_gcd=0", false, false)]
    [InlineData("ice_blitz", false, false)]
    public void The_Flags_Are_Read_From_The_Action_Line(string line, bool offGcd, bool whileCasting)
    {
        var action = AplParser.ParseActionLine($"actions={line}")!;

        Assert.Equal(offGcd, action.UseOffGcd);
        Assert.Equal(whileCasting, action.UseWhileCasting);
    }

    [Fact]
    public void An_Action_With_Use_While_Casting_Goes_During_A_Cast()
    {
        var casts = Casts("actions=ice_blitz,use_while_casting=1,if=prev.frost_bolt\nactions+=/frost_bolt");

        // A fight's casts are listed as they finish: Ice Blitz, instant, before the Frost Bolt it went during.
        Assert.Equal([(IceBlitz.AbilityName, 0.0), (FrostBolt.AbilityName, 0.0)], casts.Take(2));
    }

    [Fact]
    public void An_Action_Without_Use_While_Casting_Waits_For_The_Cast_To_End()
    {
        var casts = Casts("actions=ice_blitz,if=prev.frost_bolt\nactions+=/frost_bolt");

        Assert.Equal((FrostBolt.AbilityName, 0.0), casts[0]);
        Assert.Equal(IceBlitz.AbilityName, casts[1].Name);
        Assert.True(casts[1].Time > 0.0);
    }

    [Fact]
    public void An_Ability_Off_The_Global_Cooldown_With_A_Cast_Time_Waits_For_The_Running_Cast()
    {
        var hero = HeroImporter.Import(new CharacterBuild { Hero = "Rime", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        var enemy = ctx.AddEnemy(100, 1, "Mob", maxHealth: 1e9);
        hero.CurrentTarget = enemy;
        hero.GainResource(Hero.SpiritPoints, 100, ctx);

        Assert.True(ctx.BeginCast(hero, hero.GetAbility<FrostBolt>()!, enemy).Succeeded);
        var attempt = ctx.BeginCast(hero, hero.GetAbility<WrathOfWinter>()!, enemy);

        Assert.False(attempt.Succeeded);
        Assert.Equal($"already casting {FrostBolt.AbilityName}", attempt.Reason);
        Assert.IsType<FrostBolt>(hero.CurrentCast?.Ability);
    }

    // The casts of a 5 s fight of the Rime fixture build.
    private static List<(string Name, double Time)> Casts(string apl)
    {
        var build = System.Text.Json.JsonSerializer.Deserialize<CharacterBuild>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "rime_character.json")))!;
        var fight = new MonteCarloSimulator(build, AplParser.ParseScript(apl)).RollFight(0, SimTime.FromSeconds(5));
        return fight.Casts.Select(c => (c.Name, c.Time)).ToList();
    }
}
