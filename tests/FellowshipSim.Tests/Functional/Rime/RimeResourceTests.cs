using FellowshipSim.Core;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>Rime's Anima and Winter Orb economy: Frost Bolt and Cold Snap build it, Glacial Blast and Ice Comet spend it.</summary>
public class RimeResourceTests
{
    private static double Anima(StepDriver f) => f.Hero.ResourceAmount(Rime.Anima);

    [Fact]
    public void Frost_Bolt_Grants_3_Anima_When_The_Cast_Completes()
    {
        var f = StepFights.Rime();

        f.Cast<FrostBolt>();

        Assert.Equal(3, Anima(f));
        var gained = Assert.Single(f.Events, e => e.Kind == StepEventKind.ResourceGained && e.Name == "Anima");
        Assert.Equal(3, gained.Value);
        Assert.Equal(SimTime.FromSeconds(1.5), gained.Time); // not at the cast start (0s)
    }

    [Fact]
    public void Nine_Anima_Make_A_Winter_Orb_And_Reset_Anima()
    {
        var f = StepFights.Rime();

        f.Cast<FrostBolt>();
        f.Cast<FrostBolt>();
        Assert.Equal(0, f.RimeOf().WinterOrbs);
        f.Cast<FrostBolt>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
        Assert.Equal(0, Anima(f));
    }

    [Fact]
    public void Anima_Above_Nine_Keeps_The_Excess_After_Making_The_Orb()
    {
        var f = StepFights.Rime();
        f.Hero.Resources.Get(Rime.Anima).Current = 8;

        f.Cast<FrostBolt>(); // 8 + 3 = 11

        Assert.Equal(1, f.RimeOf().WinterOrbs);
        Assert.Equal(2, Anima(f));
    }

    [Fact]
    public void Winter_Orbs_Are_Capped_At_5()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 5;

        f.Cast<ColdSnap>(); // +1 orb

        Assert.Equal(5, f.RimeOf().WinterOrbs);
        Assert.Contains(f.Events, e => e.Kind == StepEventKind.ResourceOverflow && e.Name == "Winter Orbs");
    }

    [Fact]
    public void Cold_Snap_Grants_1_Winter_Orb()
    {
        var f = StepFights.Rime();

        f.Cast<ColdSnap>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    [Fact]
    public void Glacial_Blast_Costs_2_Winter_Orbs()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 3;

        f.Cast<GlacialBlast>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    [Fact]
    public void Ice_Comet_Costs_2_Winter_Orbs()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 3;

        f.Cast<IceComet>();

        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }

    [Fact]
    public void Spenders_Are_Refused_With_Fewer_Than_2_Winter_Orbs()
    {
        var f = StepFights.Rime();
        f.RimeOf().WinterOrbs = 1;

        Assert.False(f.TryCast<GlacialBlast>().Succeeded);
        Assert.False(f.TryCast<IceComet>().Succeeded);
        Assert.Equal(1, f.RimeOf().WinterOrbs);
    }
}
