using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's resources, on a bare Gunde. He has no resource of his own (his abilities cost nothing): what builds up are the Blood Feathers
/// he picks up, each a stack of Owed in Blood, and Spirit Points from Spirit Refunds. What a Spirit Refund gives every hero is in
/// Generic/SpiritRefund.cs.
///
/// A feather is picked up about half a second after it spawns, so the tests wait at least a second before reading the stacks. The stack
/// the engine gives at the start of a fight is removed first.
/// </summary>
public class GundeResourceEconomy
{
    private const string FeatherProc = "blood_feather";

    /// <summary>
    /// You start a fight with no stack of Owed in Blood.
    ///
    /// TODO: the engine starts every fight with one stack (one of the hero's "situational" auras, kept up all fight for replays), so a
    /// sim's Owed in Blood is castable at once. Check in game whether any fight starts with a stack.
    /// </summary>
    [Fact(Skip = "To check in game: does a fight start with a stack of Owed in Blood? The engine gives one (a situational aura kept up all fight, for replays).")]
    public void A_Fight_Starts_With_No_Owed_In_Blood()
    {
        var f = StepFights.Gunde();

        Assert.Equal(0, f.Stacks(OwedInBlood.Buff));
    }

    /// <summary>Gunde has no resource of his own: his abilities cost nothing, so he can cast them as often as their cooldowns allow.</summary>
    [Fact]
    public void Gunde_Has_No_Resource_Of_His_Own()
    {
        var f = StepFights.Gunde();

        Assert.Equal(0, f.Amount(Hero.SpiritPoints));
        foreach (var resource in f.Hero.ResourceDefs) Assert.Empty(f.Spends(resource.Name));
    }

    // ---- Blood Feathers

    /// <summary>
    /// Each time Rend deals damage there is a chance a Blood Feather is spawned, and picking it up gives a stack of Owed in Blood: after a
    /// Reaver's Edge, no stack at 2.5s, and one at 3.6s (the tick at 3s, then the pickup).
    /// </summary>
    [Fact]
    public void A_Rend_Tick_Can_Spawn_A_Blood_Feather()
    {
        var f = StepFights.Gunde();
        f.Remove(OwedInBlood.Buff); // the stack the engine starts the fight with
        f.ForceProcs(key => key == FeatherProc ? true : null);

        f.Cast<ReaversEdge>();

        f.WaitUntil(2.5);
        Assert.Equal(0, f.Stacks(OwedInBlood.Buff));
        f.WaitUntil(3.6);
        Assert.Equal(1, f.Stacks(OwedInBlood.Buff));
    }

    /// <summary>
    /// Each Rend tick has a 15% chance to spawn a Blood Feather.
    ///
    /// TODO: the game data has no chance; the engine's 15% is an estimate from one combat log. Check in game.
    /// </summary>
    [Fact(Skip = "The chance of a Blood Feather per Rend tick is unknown (the engine's 15% is an estimate from one log)")]
    public void A_Rend_Tick_Has_A_Chance_Of_A_Blood_Feather()
    {
        var f = StepFights.Gunde();
        f.Remove(OwedInBlood.Buff); // the stack the engine starts the fight with
        var procs = f.ForceProcs(_ => null); // only to read the chance

        f.Cast<ReaversEdge>();
        f.Wait(3.1); // the first Rend tick

        Assert.Contains(procs.ChancesOf(FeatherProc), chance => Math.Abs(chance - 0.15) < 1e-9);
    }

    /// <summary>An enemy that dies with your Rend on it has a 25% chance to create a Blood Feather, which gives a stack of Owed in Blood.</summary>
    [Fact]
    public void An_Enemy_Dying_With_Rend_Has_A_25_Percent_Chance_Of_A_Blood_Feather()
    {
        var f = StepFights.Gunde(enemies: 2);
        f.Remove(OwedInBlood.Buff); // the stack the engine starts the fight with
        var procs = f.ForceProcs(key => key == FeatherProc ? true : null);

        f.Cast<ReaversEdge>();
        procs.Rolls.Clear(); // only the death's roll counts

        f.Defeat(f.Enemy);
        f.Wait(1); // the feather is picked up

        using var check = new Checks(f);
        check.True(procs.ChancesOf(FeatherProc).Any(chance => Math.Abs(chance - 0.25) < 1e-9), "a roll at 25%");
        check.Equal(1, f.Stacks(OwedInBlood.Buff));
    }

    /// <summary>A Blood Feather picked up gives 1 stack of Owed in Blood, up to 150: at 150 stacks, a Spirit Refund's five feathers leave 150.</summary>
    [Fact]
    public void Feathers_Stop_Adding_Stacks_At_150()
    {
        var f = StepFights.Gunde(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Apply(OwedInBlood.Buff, stacks: 150);

        f.Cast<ReaversEdge>();
        f.Wait(1); // the feathers are picked up

        Assert.Equal(150, f.Stacks(OwedInBlood.Buff));
    }

    // ---- Owed in Blood (what the ability does with the stacks is in GundeAbilities.cs)

    /// <summary>
    /// Owed in Blood needs a stack of Owed in Blood: refused with none, allowed with one.
    ///
    /// 10ms pass after the stacks are removed, since the engine lets a cast use stacks removed in the same instant.
    /// </summary>
    [Fact]
    public void Owed_In_Blood_Needs_A_Stack()
    {
        var f = StepFights.Gunde();
        f.Remove(OwedInBlood.Buff); // the stack the engine starts the fight with
        f.Wait(0.01);

        using var check = new Checks(f);
        check.False(f.TryCast<OwedInBlood>().Succeeded);
        f.Apply(OwedInBlood.Buff, stacks: 1);
        check.True(f.TryCast<OwedInBlood>().Succeeded);
    }

    /// <summary>Owed in Blood's stacks last 90 seconds, and there can be at most 150: 200 stacks gained one by one leave 150, still there at
    /// 89 seconds and gone at 91.</summary>
    [Fact]
    public void Owed_In_Blood_Stacks_Last_90_Seconds_Up_To_150()
    {
        var f = StepFights.Gunde();
        for (int i = 0; i < 200; i++) f.Hero.Auras.Apply(OwedInBlood.Buff, f.Now); // one stack each, as a feather gives; f.Apply sets the stacks instead

        using var check = new Checks(f);
        check.Equal(150, f.Stacks(OwedInBlood.Buff));
        f.WaitUntil(89);
        check.Equal(150, f.Stacks(OwedInBlood.Buff));
        f.WaitUntil(91);
        check.Equal(0, f.Stacks(OwedInBlood.Buff));
    }

    // ---- Spirit Refund

    /// <summary>A Spirit Refund on an ability that applies Rend creates 5 Blood Feathers near you, and gives a Spirit Point.</summary>
    [Fact]
    public void A_Spirit_Refund_Creates_5_Blood_Feathers_And_A_Spirit_Point()
    {
        var f = StepFights.Gunde(spirit: 0.2);
        f.Remove(OwedInBlood.Buff); // the stack the engine starts the fight with
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);

        f.Cast<ReaversEdge>();
        f.Wait(1); // the feathers are picked up

        using var check = new Checks(f);
        check.Equal(5, f.Stacks(OwedInBlood.Buff));
        check.Equal(1, f.Amount(Hero.SpiritPoints), 0.01);
    }

    /// <summary>
    /// Each cast of an ability that applies Rend rolls a single Spirit Refund, however many hits it lands and enemies it strikes.
    ///
    /// 3 enemies, so an area ability strikes several. TODO: confirm in the game that it is one roll per cast, as the user expects.
    /// </summary>
    [Theory]
    [InlineData("Double Strike")]
    [InlineData("Reaver's Edge")]
    [InlineData("Blood Arc")]
    [InlineData("Grim Carve")]
    [InlineData("Heart Splitter")]
    [InlineData("Rupture")]
    [InlineData("Bloodbound Spirit")]
    public void Each_Cast_That_Applies_Rend_Rolls_One_Spirit_Refund(string ability)
    {
        var f = StepFights.Gunde(enemies: 3, spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null); // only to count the rolls
        f.FillResources(); // enough to cast it, Spirit Points included

        f.Cast(ability);
        f.Wait(5); // every hit lands

        Assert.Single(procs.ChancesOf(Hero.SpiritRefundProc));
    }

    /// <summary>
    /// Abilities that do not apply Rend do not roll a Spirit Refund: with Rend already on the enemy, the auto attack, Warbound and
    /// Slaughter give no roll and no Spirit Point.
    /// </summary>
    [Fact]
    public void Abilities_That_Apply_No_Rend_Do_Not_Roll_A_Spirit_Refund()
    {
        var f = StepFights.Gunde(spirit: 0.2);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);

        f.Cast<ReaversEdge>(); // Rend on the enemy
        procs.Rolls.Clear(); // Reaver's Edge's own roll
        f.Set(Hero.SpiritPoints, 0); // and its Spirit Point

        f.Cast<Attack>();
        f.Cast<Warbound>();
        f.Cast<Slaughter>();

        using var check = new Checks(f);
        check.Equal(0, procs.ChancesOf(Hero.SpiritRefundProc).Count, what: "Spirit Refund rolls");
        check.Equal(0, f.Amount(Hero.SpiritPoints), 0.01);
    }
}
