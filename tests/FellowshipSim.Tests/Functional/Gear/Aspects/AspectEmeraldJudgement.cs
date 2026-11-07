using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Gems;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Emerald Judgement (aspect 34): your damage has a chance (0.8 / 1.3 / 1.8 / 2.3 procs per minute, ranks 1 to 4) to conjure an Emerald
/// Spike under the enemy hit, for 800% of your main stat. The chance is forced.
/// </summary>
public class AspectEmeraldJudgement
{
    private const string Spike = "Emerald Judgement";

    /// <summary>A spike that is forced to come with every hit (none on the spike's own damage).</summary>
    private static StepDriver Fight(string hero = "Rime", double crit = 0, double expertise = 0, int enemies = 1)
    {
        var f = StepFights.Of(hero, crit: crit, expertise: expertise, enemies: enemies, augmentations: [new(EmeraldJudgement.AspectId, 1)])
            .WithoutDamageSpread();
        f.ForceProcs(key => key == EmeraldJudgement.ProcKey ? true : null);
        return f;
    }

    /// <summary>
    /// An Emerald Spike deals 800% of your main stat, times your class's balancing aura (Rime and Mara 1.04, Ardeos 1.1, Elarion 1.08,
    /// Gunde and Tariq 1.0).
    /// </summary>
    [Theory]
    [InlineData("Rime", "Frost Bolt", 1.04)]
    [InlineData("Ardeos", "Infernal Wave", 1.1)]
    [InlineData("Elarion", "Focused Shot", 1.08)]
    [InlineData("Gunde", "Double Strike", 1.0)]
    [InlineData("Mara", "Skittering Blades", 1.04)]
    [InlineData("Tariq", "Heavy Strike", 1.0)]
    public void An_Emerald_Spike_Deals_800_Percent_Main_Stat(string hero, string ability, double balancing)
    {
        var f = Fight(hero);

        f.Cast(ability);
        f.Wait(1); // the hits land

        AssertRatio(8.0 * balancing, f.Damage(Spike)[0].Value / f.MainStat());
    }

    /// <summary>An Emerald Spike can crit, with your crit chance.</summary>
    [Fact]
    public void An_Emerald_Spike_Can_Crit()
    {
        var f = Fight(crit: 0.3);

        f.Cast("Frost Bolt", crit: false);

        Assert.Equal(0.3, f.Damage(Spike)[0].CritChance, 1e-9);
    }

    /// <summary>An Emerald Spike gains from Expertise like any hit.</summary>
    [Fact]
    public void An_Emerald_Spike_Gains_From_Expertise()
    {
        double FirstSpike(double expertise)
        {
            var f = Fight(expertise: expertise);

            f.Cast("Frost Bolt");

            return f.Damage(Spike)[0].Value;
        }

        AssertRatio(1.1, FirstSpike(expertise: 0.1) / FirstSpike(expertise: 0));
    }

    /// <summary>A spike's own damage does not roll for another spike: every hit brings at most one, never a chain.</summary>
    [Fact]
    public void An_Emerald_Spike_Does_Not_Bring_Another()
    {
        var f = Fight();

        for (int cast = 0; cast < 3; cast++) f.Cast("Frost Bolt");

        int spikes = f.Damage(Spike).Count;
        Assert.Equal(f.AllDamage().Count - spikes, spikes);
    }

    /// <summary>An Emerald Spike erupts under the enemy that was hit.</summary>
    [Fact]
    public void An_Emerald_Spike_Erupts_Under_The_Enemy_Hit()
    {
        var f = Fight(enemies: 2);

        f.Cast("Frost Bolt", target: f.Enemies()[1]);

        Assert.Equal(f.Enemies()[1].Name, f.Damage(Spike).Single().Target);
    }

    /// <summary>
    /// An Emerald Spike gives the Emerald gem's First Strike again, as a first hit would: its 15 seconds start over when the spike lands.
    ///
    /// From combat logs. Rime with Emerald at 80 gem power hits once for First Strike, then a spike lands 10 seconds later.
    /// </summary>
    [Fact]
    public void An_Emerald_Spike_Starts_First_Strike_Again()
    {
        var firstStrike = Emerald.AllNodes[0].Grants!;
        var f = StepFights.Of("Rime", augmentations: [new(EmeraldJudgement.AspectId, 1)], gemPower: new() { [GemColor.Emerald] = 80 });
        bool spike = false;
        f.ForceProcs(key => key == EmeraldJudgement.ProcKey ? spike : null);

        f.Cast("Frost Bolt");
        f.WaitUntil(10);
        spike = true;
        f.Cast("Frost Bolt");
        f.Wait(1); // the bolt and its spike land
        spike = false;
        double end = f.Damage(Spike).Single().Time.TotalSeconds + 15;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(firstStrike));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(firstStrike));
    }
}
