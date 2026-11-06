using FellowshipSim.Core.Customization.CommonAbilities;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Import;

namespace FellowshipSim.Tests;

/// <summary>A mount waits for no global cooldown (game data: no global cooldown effect).</summary>
public class MountSummonTests
{
    [Fact]
    public void A_Mount_Starts_Inside_The_Global_Cooldown_And_Leaves_It_As_It_Is()
    {
        var (hero, ctx, mount) = Mara();
        var cooldownEnd = SimTime.FromSeconds(0.6);
        hero.GlobalCooldownEndTime = cooldownEnd;

        var attempt = ctx.BeginCast(hero, mount, hero.CurrentTarget);

        Assert.Equal(CastStatus.Casting, attempt.Status);
        Assert.Equal(SimTime.FromSeconds(MountSummon.CastTimeSeconds), attempt.CastTime);
        Assert.Equal(cooldownEnd, hero.GlobalCooldownEndTime);
    }

    /// <summary>A mount ability blocks abilities tagged `AbilityType.Mounting`.</summary>
    [Fact]
    public void A_Running_Mount_Cast_Blocks_Another()
    {
        var (hero, ctx, mount) = Mara();
        ctx.BeginCast(hero, mount, hero.CurrentTarget);

        var attempt = ctx.BeginCast(hero, mount, hero.CurrentTarget);

        Assert.False(attempt.Succeeded);
        Assert.Equal($"already casting {MountSummon.AbilityName}", attempt.Reason);
    }

    private static (Mara Hero, SimulationContext Context, Ability Mount) Mara()
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000 });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        return (hero, ctx, hero.Abilities.OfType<MountSummon>().Single());
    }
}
