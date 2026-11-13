using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Hidden Power (sigil 1): "Your abilities have a chance to grant you 1 stack of Hidden Power for 60 seconds. When you have 5 stacks,
/// Hidden Power is consumed and Power Revealed is granted to you for 15 seconds, increasing your Main Stat by 7.2% / 12.8% / 18.4% / 24%.
/// You can not gain Hidden Power stacks while Power Revealed is active on you" (ranks 1 to 4). The chance is forced.
/// </summary>
public class SigilHiddenPower
{
    /// <summary>
    /// Five Hidden Power stacks are consumed and give you Power Revealed for 15 seconds: 7.2% / 12.8% / 18.4% / 24% more main stat.
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1, 0.072)]
    [InlineData("Rime", "Frost Bolt", 2, 0.128)]
    [InlineData("Rime", "Frost Bolt", 3, 0.184)]
    [InlineData("Rime", "Frost Bolt", 4, 0.24)]
    [InlineData("Ardeos", "Infernal Wave", 4, 0.24)]
    [InlineData("Elarion", "Focused Shot", 4, 0.24)]
    [InlineData("Gunde", "Double Strike", 4, 0.24)]
    [InlineData("Mara", "Skittering Blades", 4, 0.24)]
    [InlineData("Tariq", "Heavy Strike", 4, 0.24)]
    public void Five_Hidden_Power_Stacks_Give_Power_Revealed_For_15_Seconds(string hero, string ability, int rank, double bonus)
    {
        var f = StepFights.Of(hero, augmentations: [new(HiddenPower.SigilId, rank)]);
        f.ForceProcs(key => key == HiddenPower.ProcKey ? true : null);
        double plainMainStat = f.MainStat();

        for (int cast = 0; cast < 5; cast++) f.CastWhenReady(ability);
        double start = f.CompletedAt(ability);

        using var check = new Checks(f);
        check.Equal(0, f.Stacks(HiddenPower.Stack));
        check.Ratio(1 + bonus, f.MainStat() / plainMainStat);
        f.WaitUntil(start + 15 - 0.1);
        check.Ratio(1 + bonus, f.MainStat() / plainMainStat);
        f.WaitUntil(start + 15 + 0.1);
        check.Ratio(1.0, f.MainStat() / plainMainStat);
    }

    /// <summary>A Hidden Power stack lasts 60 seconds.</summary>
    [Fact]
    public void A_Hidden_Power_Stack_Lasts_60_Seconds()
    {
        var f = StepFights.Of("Rime", augmentations: [new(HiddenPower.SigilId, 1)]);
        int rolls = 0;
        f.ForceProcs(key => key == HiddenPower.ProcKey ? rolls++ == 0 : null); // the first roll only

        f.Cast("Frost Bolt");
        double start = f.CompletedAt("Frost Bolt");

        f.WaitUntil(start + 60 - 0.1);
        Assert.Equal(1, f.Stacks(HiddenPower.Stack));
        f.WaitUntil(start + 60 + 0.1);
        Assert.Equal(0, f.Stacks(HiddenPower.Stack));
    }

    /// <summary>Any ability can give a Hidden Power stack, not only those that deal damage.</summary>
    [Theory]
    [InlineData("Ice Blitz")]
    [InlineData("Frigid Winds")]
    public void Any_Ability_Can_Give_A_Hidden_Power_Stack(string ability)
    {
        var f = StepFights.Of("Rime", augmentations: [new(HiddenPower.SigilId, 1)]);
        f.ForceProcs(key => key == HiddenPower.ProcKey ? true : null);

        f.Cast(ability);

        Assert.Equal(1, f.Stacks(HiddenPower.Stack));
    }

    /// <summary>You gain no Hidden Power stack while Power Revealed is on you, and gain them again once it ends.</summary>
    [Fact]
    public void No_Hidden_Power_Stack_While_Power_Revealed_Is_Up()
    {
        var f = StepFights.Of("Rime", augmentations: [new(HiddenPower.SigilId, 1)]);
        f.ForceProcs(key => key == HiddenPower.ProcKey ? true : null);

        for (int cast = 0; cast < 5; cast++) f.Cast("Frost Bolt");
        double start = f.CompletedAt("Frost Bolt");

        f.Cast("Frost Bolt");
        Assert.Equal(0, f.Stacks(HiddenPower.Stack));
        f.WaitUntil(start + 15 + 0.1);
        f.Cast("Frost Bolt");
        Assert.Equal(1, f.Stacks(HiddenPower.Stack));
    }

    /// <summary>
    /// The casts during Power Revealed roll nothing, so the first cast after it rolls for the whole time since the roll that gave Power
    /// Revealed: 2.6 procs per minute times that many seconds, not the chance of one cast's worth of time.
    /// </summary>
    [Fact]
    public void The_First_Roll_After_Power_Revealed_Counts_The_Time_Since_The_Roll_That_Gave_It()
    {
        var f = StepFights.Of("Rime", augmentations: [new(HiddenPower.SigilId, 1)]);
        var procs = f.ForceProcs(key => key == HiddenPower.ProcKey ? true : null);

        for (int cast = 0; cast < 5; cast++) f.Cast("Frost Bolt");
        double lastRoll = f.CompletedAt("Frost Bolt");
        do f.Cast("Frost Bolt"); // the casts during Power Revealed, then the first one after it
        while (f.Seconds() < lastRoll + 15 + 0.1);
        double firstAfter = f.CompletedAt("Frost Bolt");

        var chances = procs.ChancesOf(HiddenPower.ProcKey);

        using var check = new Checks(f);
        check.Equal(6, chances.Count, what: "rolls: 5, none during Power Revealed, 1 after");
        check.Ratio(HiddenPower.ProcsPerMinute * (firstAfter - lastRoll) / 60, chances[5]);
        check.True(chances[5] > 5 * chances[4], "far above an ordinary roll's chance");
    }
}
