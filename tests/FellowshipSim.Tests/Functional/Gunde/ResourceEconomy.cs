using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// What Gunde gets and spends. He has no resource of his own (his abilities cost nothing); what builds up are the Blood Feathers he picks up, each a
/// stack of Owed in Blood, and Spirit Points from Spirit Refunds.
/// Dev: Spirit Refund is forced on to make feathers; a feather picked up is a gain of Owed in Blood, read as stacks after a pickup delay of
/// about half a second (so the tests wait at least a second). The stacks the engine gives at the start of a fight are removed first.
/// </summary>
public class GundeResourceEconomy
{
    private const string FeatherProc = "blood_feather";

    private static StepDriver WithoutStacks(double spirit = 0, int enemies = 1, int[]? talents = null)
    {
        var f = StepFights.Gunde(enemies: enemies, spirit: spirit, talents: talents);
        f.SetOwedInBlood(0);
        return f;
    }

    /// <summary>
    /// Player: you start a fight with no stack of Owed in Blood.
    /// Dev: the stacks at the first moment of a fight.
    /// TODO: fails: the engine starts every fight with one stack of Owed in Blood (it is one of the hero's "situational" auras, kept up all fight for
    /// replays). A sim's Owed in Blood is then castable at once. Check whether any fight starts with a stack.
    /// </summary>
    [Fact]
    public void A_Fight_Starts_With_No_Owed_In_Blood()
    {
        var f = StepFights.Gunde();

        Assert.Equal(0, f.OwedInBloodStacks());
    }

    /// <summary>
    /// Player: Gunde has no resource: his abilities cost nothing, so he can cast them as often as their cooldowns allow.
    /// Dev: his resources are Spirit Points alone, at 0.
    /// </summary>
    [Fact]
    public void Gunde_Has_No_Resource_Of_His_Own()
    {
        var f = StepFights.Gunde();

        Assert.Equal(0, f.Hero.ResourceAmount(Hero.SpiritPoints));
        Assert.DoesNotContain(f.Events, e => e.Kind is StepEventKind.ResourceSpent);
    }

    // ---- Blood Feathers

    /// <summary>
    /// Player: each time Rend deals damage there is a chance a Blood Feather is spawned; picking it up gives a stack of Owed in Blood.
    /// Dev: the feather chance forced on; a Reaver's Edge; no stack at 2.5s, and one at 3.6s (the tick at 3s and a pickup delay).
    /// </summary>
    [Fact]
    public void A_Rend_Tick_Can_Spawn_A_Blood_Feather()
    {
        var f = WithoutStacks();
        f.ForceProcs(key => key == FeatherProc ? true : null);
        f.Cast<ReaversEdge>();

        f.Wait(1.0);
        Assert.Equal(0, f.OwedInBloodStacks());
        f.Wait(1.1);

        Assert.Equal(1, f.OwedInBloodStacks());
    }

    /// <summary>
    /// Player: each Rend tick has a chance to spawn a Blood Feather.
    /// Dev: the chance the engine gives the roll at a tick, read from the rolls.
    /// TODO: skipped: the game data has no chance; 0.15 in the engine is an estimate from one combat log. Check in game.
    /// </summary>
    [Fact(Skip = "The chance of a Blood Feather per Rend tick is unknown (the engine's 15% is an estimate from one log)")]
    public void A_Rend_Tick_Has_A_Chance_Of_A_Blood_Feather()
    {
        var f = WithoutStacks();
        var procs = f.ForceProcs(_ => null);
        f.Cast<ReaversEdge>();

        f.Wait(3.1);

        Assert.Contains(procs.Rolls, r => r.Key == FeatherProc && Math.Abs(r.Chance - 0.15) < 1e-9);
    }

    /// <summary>
    /// Player: enemies that die with your Rend on them have a 25% chance to create a Blood Feather.
    /// Dev: Rend on an enemy; it dies; the roll has a 25% chance, and forced on gives a stack.
    /// </summary>
    [Fact]
    public void An_Enemy_Dying_With_Rend_Has_A_25_Percent_Chance_Of_A_Blood_Feather()
    {
        var f = WithoutStacks(enemies: 2);
        var procs = f.ForceProcs(key => key == FeatherProc ? true : null);
        f.Cast<ReaversEdge>();
        procs.Rolls.Clear();

        f.Context.RemoveEnemy(f.Enemy, defeated: true);
        f.Wait(1);

        Assert.Contains(procs.Rolls, r => r.Key == FeatherProc && Math.Abs(r.Chance - 0.25) < 1e-9);
        Assert.Equal(1, f.OwedInBloodStacks());
    }

    /// <summary>
    /// Player: a Blood Feather picked up gives 1 stack of Owed in Blood, up to 150.
    /// Dev: 150 stacks held; one more feather (a Spirit Refund's five): still 150.
    /// </summary>
    [Fact]
    public void Feathers_Stop_Adding_Stacks_At_150()
    {
        var f = StepFights.Gunde(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetOwedInBlood(150);

        f.Cast<ReaversEdge>();
        f.Wait(1);

        Assert.Equal(150, f.OwedInBloodStacks());
    }

    // ---- Spirit Refund

    /// <summary>
    /// Player: a Spirit Refund on an ability that applies Rend creates 5 Blood Feathers near you, and gives a Spirit Point.
    /// Dev: Spirit Refund forced on; a Reaver's Edge; a second later 5 stacks of Owed in Blood and 1 Spirit Point.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Creates_5_Blood_Feathers_And_A_Spirit_Point()
    {
        var f = WithoutStacks(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);

        f.Cast<ReaversEdge>();
        f.Wait(1);

        Assert.Equal(5, f.OwedInBloodStacks());
        Assert.Equal(1, f.Hero.ResourceAmount(Hero.SpiritPoints), 0.01);
    }

    /// <summary>
    /// Player: the abilities that apply Rend each roll a Spirit Refund once per cast, however many hits they have.
    /// Dev: Spirit Refund forced off; one cast of each on three enemies; one roll each.
    /// </summary>
    [Theory]
    [InlineData("Double Strike")]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Heart Splitter")]
    [InlineData("Grim Carve")]
    [InlineData("Rupture")]
    public void Abilities_That_Apply_Rend_Roll_One_Spirit_Refund_Per_Cast(string ability)
    {
        var f = StepFights.Gunde(enemies: 3, spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);

        f.Cast(f.Hero.Abilities.First(a => a.Name == ability));
        f.Wait(1.5);

        Assert.Single(procs.Rolls, r => r.Key == Hero.SpiritRefundProc);
    }

    /// <summary>
    /// Player: abilities that do not apply Rend do not roll a Spirit Refund.
    /// Dev: Spirit Refund forced on; a Reaver's Edge puts Rend on the enemy and its roll and Spirit Point are cleared; the auto attack, Warbound and Slaughter cast; no roll, no Spirit Point.
    /// </summary>
    [Fact]
    public void Abilities_That_Apply_No_Rend_Do_Not_Roll_A_Spirit_Refund()
    {
        var f = StepFights.Gunde(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Cast<ReaversEdge>();
        procs.Rolls.Clear();
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 0;
        f.Cast<Attack>();
        f.Cast<Warbound>();
        f.Cast<Slaughter>();

        Assert.DoesNotContain(procs.Rolls, r => r.Key == Hero.SpiritRefundProc);
        Assert.Equal(0, f.Hero.ResourceAmount(Hero.SpiritPoints), 0.01);
    }
}
