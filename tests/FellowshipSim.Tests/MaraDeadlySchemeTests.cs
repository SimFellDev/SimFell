using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Talents;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

/// <summary>Effect 2434 is off under Deadly Scheme: Active with its stack count kept, and the log reads so.</summary>
public class MaraDeadlySchemeTests
{
    private static readonly int StacksId = DeadlyScheme.Stacks.LogId;
    private static readonly int ActiveId = DeadlyScheme.Active.LogId;

    [Fact]
    public void Active_Running_Out_With_No_Spend_Brings_The_Stacks_Back_At_40_Then_A_Spend_Shows_The_Count()
    {
        var (hero, ctx) = Mara();
        Spend(hero, 240, 1); // Stacks past the Active threshold.
        Assert.False(hero.Auras.HasAura(DeadlyScheme.Stacks, SimTime.FromSeconds(1)));

        ctx.RunUntil(SimTime.FromSeconds(14));
        Assert.Equal(40, hero.Auras.GetStacks(DeadlyScheme.Stacks, SimTime.FromSeconds(14)));

        Spend(hero, 35, 15);
        Assert.Equal(15, hero.Auras.GetStacks(DeadlyScheme.Stacks, SimTime.FromSeconds(15)));
    }

    [Fact]
    public void Active_Running_Out_After_A_Spend_Brings_The_Stacks_Back_At_The_Count()
    {
        var (hero, ctx) = Mara();
        Spend(hero, 240, 1);
        Spend(hero, 35, 2);
        Assert.False(hero.Auras.HasAura(DeadlyScheme.Stacks, SimTime.FromSeconds(2)));

        ctx.RunUntil(SimTime.FromSeconds(14));
        Assert.Equal(15, hero.Auras.GetStacks(DeadlyScheme.Stacks, SimTime.FromSeconds(14)));
    }

    [Fact]
    public void Stacks_Given_From_Outside_Set_The_Count()
    {
        var (hero, _) = Mara();
        Spend(hero, 35, 1);
        hero.Auras.Apply(DeadlyScheme.Stacks, SimTime.FromSeconds(1), setStacks: 14);

        Spend(hero, 35, 2);

        Assert.Equal(21, hero.Auras.GetStacks(DeadlyScheme.Stacks, SimTime.FromSeconds(2)));
    }

    [Fact]
    public void The_Log_Shows_No_Stacks_Under_Active_And_Its_Return_Has_The_Last_Number()
    {
        var log = Timeline(engineStacks: 99,
            Aura(10.0, "applybuffstack", StacksId, 40), Aura(10.002, "removebuff", StacksId), Aura(10.002, "applybuff", ActiveId),
            Aura(11.0, "refreshbuff", StacksId),
            Aura(12.0, "applybuffstack", StacksId, 21), Aura(12.004, "applybuff", StacksId), Aura(12.004, "removebuff", ActiveId));

        Assert.False(log.TrackedStateAt(11.5).ContainsKey(StacksId));
        Assert.Equal(21, log.TrackedStateAt(12.5)[StacksId]);
    }

    [Fact]
    public void A_Return_With_No_Number_Reads_As_The_Engine_Until_The_Next_Number()
    {
        var log = Timeline(engineStacks: 13,
            Aura(10.0, "removebuff", StacksId), Aura(10.0, "applybuff", ActiveId),
            Aura(11.0, "refreshbuff", StacksId), Aura(11.005, "applybuff", StacksId), Aura(11.005, "removebuff", ActiveId),
            Aura(12.0, "applybuffstack", StacksId, 20));

        Assert.False(log.TrackedStateAt(10.5).ContainsKey(StacksId));
        Assert.Equal(13, log.TrackedStateAt(11.5)[StacksId]);
        Assert.Equal(20, log.TrackedStateAt(12.5)[StacksId]);
    }

    [Fact]
    public void A_Removal_Away_From_Active_Stays_A_Removal()
    {
        var log = Timeline(engineStacks: 99,
            Aura(10.0, "applybuffstack", StacksId, 21), Aura(11.0, "removebuff", StacksId),
            Aura(12.0, "applybuff", StacksId), Aura(20.0, "applybuff", ActiveId));

        Assert.False(log.TrackedStateAt(11.5).ContainsKey(StacksId));
        Assert.Equal(1, log.TrackedStateAt(12.5)[StacksId]);
    }

    private static (Mara Hero, SimulationContext Context) Mara()
    {
        var hero = (Mara)HeroImporter.Import(new CharacterBuild { Hero = "Mara", PrimaryStat = 1000, Talents = [DeadlyScheme.TalentId] });
        var ctx = new SimulationContext(hero);
        hero.ResetForIteration();
        hero.BeginFight(ctx);
        return (hero, ctx);
    }

    private static void Spend(Mara hero, double energy, double seconds) =>
        hero.NotifyResourceSpent(Core.Heroes.Mara.Mara.Energy, energy, SimTime.FromSeconds(seconds));

    private static LogAuraTimeline Timeline(int engineStacks, params LogAura[] events) =>
        new(events, [StacksId, ActiveId], _ => engineStacks);

    private static LogAura Aura(double time, string type, int id, int? stack = null) =>
        new() { Time = time, Type = type, AbilityId = id, AbilityName = "Deadly Scheme", Stack = stack };
}
