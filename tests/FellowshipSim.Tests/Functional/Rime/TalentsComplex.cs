using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Rime;
using FellowshipSim.Core.Heroes.Rime.Abilities;
using FellowshipSim.Core.Heroes.Rime.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Rime's talents working together, or with several abilities. One talent on its own is in TalentsSimple.cs.
/// </summary>
public class TalentsComplex
{
    private static int Stacks(StepDriver f, AuraDef buff) => f.Hero.Auras.GetStacks(buff, f.Now);

    // ---- Frostweaver's Wrath with Icy Talons

    /// <summary>
    /// Player: with Icy Talons and Frostweaver's Wrath, only the first spike of a Talon Strike, or the first wave of a Rising
    /// Talons, is the guaranteed critical strike, and it uses up one stack; the others are not.
    /// Dev: both talents; the Frostweaver's Wrath chance forced on, so a Cold Snap gives 1 stack; orbs set to 3 for 3
    /// spikes or waves. The first hit's crit chance is 1 and the other two are 0; no stack is left.
    /// </summary>
    [Theory]
    [InlineData("Talon Strike")]
    [InlineData("Rising Talons")]
    public void Frostweavers_Wrath_Crits_Only_The_First_Spike_Or_Wave(string spender)
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId, FrostweaversWrath.TalentId]);
        f.ForceProcs(key => key == FrostweaversWrath.ProcKey ? true : null);
        f.Cast<ColdSnap>();
        f.RimeOf().WinterOrbs = 3;

        f.Cast(f.Hero.GetAbility(spender)!);
        f.Wait(5.0);

        var hits = f.Damage(spender);
        Assert.Equal(3, hits.Count);
        Assert.Equal(1.0, hits[0].CritChance, 9);
        Assert.Equal(0.0, hits[1].CritChance, 9);
        Assert.Equal(0.0, hits[2].CritChance, 9);
        Assert.Equal(0, Stacks(f, FrostweaversWrath.Buff));
    }

    // ---- Avalanche and Cold Shower: which comets count (TO CHECK IN GAME)

    /// <summary>
    /// Player: with Icy Talons and Avalanche, the first wave of a Rising Talons counts as an Ice Comet that struck, so it
    /// can be followed by extra comets.
    /// Dev: both talents; the Avalanche chances forced (one extra comet); 1 orb; a Rising Talons, a 5s wait. Counts the
    /// "Ice Comet" hits: 1 extra comet. SKIPPED: to check in game that Rising Talons can trigger Avalanche.
    /// </summary>
    [Fact(Skip = "To check in game: does the first wave of Rising Talons trigger Avalanche?")]
    public void Avalanche_Is_Triggered_By_The_First_Wave_Of_Rising_Talons()
    {
        var f = StepFights.Rime(talents: [IcyTalons.TalentId, Avalanche.TalentId]);
        f.ForceProcs(key => key == Avalanche.ProcKey ? true : key == Avalanche.SecondProcKey ? false : null);
        f.RimeOf().WinterOrbs = 1;

        f.Cast<RisingTalons>();
        f.Wait(5.0);

        Assert.Equal(1, f.Damage("Ice Comet").Count);
    }

    /// <summary>
    /// Player: the extra comets of Avalanche do not trigger Avalanche themselves, however lucky you are.
    /// Dev: the Avalanche chances forced on (2 extra comets); one Ice Comet (2 orbs), a 4s wait: 3 comet hits in all, no
    /// more. SKIPPED: to check in game.
    /// </summary>
    [Fact(Skip = "To check in game: Avalanche's own comets cannot trigger Avalanche")]
    public void Avalanche_Extra_Comets_Do_Not_Trigger_Avalanche()
    {
        var f = StepFights.Rime(talents: [Avalanche.TalentId]);
        f.ForceProcs(key => key == Avalanche.ProcKey || key == Avalanche.SecondProcKey ? true : null);
        f.RimeOf().WinterOrbs = 2;

        f.Cast<IceComet>();
        f.Wait(4.0);

        Assert.Equal(3, f.Damage("Ice Comet").Count);
    }

    /// <summary>
    /// Player: a free comet from Cold Shower counts as an Ice Comet that struck, so it can trigger Avalanche.
    /// Dev: both talents, both chances forced (Avalanche gives 1 extra comet); a Freezing Torrent, a 5s wait: each tick's
    /// comet is followed by an extra one, so there are 2 comet hits per tick. SKIPPED: to check in game.
    /// </summary>
    [Fact(Skip = "To check in game: can a Cold Shower comet trigger Avalanche?")]
    public void Avalanche_Is_Triggered_By_Cold_Shower_Comets()
    {
        var f = StepFights.Rime(talents: [ColdShower.TalentId, Avalanche.TalentId]);
        f.ForceProcs(key => key == ColdShower.ProcKey || key == Avalanche.ProcKey ? true : key == Avalanche.SecondProcKey ? false : null);

        f.Cast<FreezingTorrent>();
        f.Wait(5.0);

        int ticks = f.Damage("Freezing Torrent").Count;
        Assert.Equal(2 * ticks, f.Damage("Ice Comet").Count);
    }

    /// <summary>
    /// Player: Icy Flow applies to the free comets of Cold Shower but not to the extra comets of Avalanche.
    /// Dev: Icy Flow, Cold Shower and Avalanche, chances forced; a Cold Snap gives 1 Icy Flow stack; a Freezing Torrent.
    /// Exactly one comet hit has the 20% crit chance (the first Cold Shower comet, which uses the stack), not its Avalanche
    /// extras. SKIPPED: to check in game.
    /// </summary>
    [Fact(Skip = "To check in game: Icy Flow works on Cold Shower comets and not on Avalanche's")]
    public void Icy_Flow_Applies_To_Cold_Shower_Comets_And_Not_To_Avalanche_Extras()
    {
        var f = StepFights.Rime(talents: [IcyFlow.TalentId, ColdShower.TalentId, Avalanche.TalentId]);
        f.ForceProcs(key => key == ColdShower.ProcKey || key == Avalanche.ProcKey ? true : key == Avalanche.SecondProcKey ? false : null);
        f.Cast<ColdSnap>();

        f.Cast<FreezingTorrent>();
        f.Wait(5.0);

        var boosted = f.Damage("Ice Comet").Where(h => Math.Abs(h.CritChance - 0.2) < 1e-9).ToList();
        Assert.Single(boosted);
        Assert.Equal(f.Damage("Ice Comet").Min(h => h.Time), boosted[0].Time);
    }

    // ---- Navir's Keeper with Glacial Assault

    /// <summary>
    /// Player: with Navir's Keeper and Glacial Assault, the Cold Snaps that cost no charge still give Glacial Assault
    /// stacks: after Flight of the Navir you reach 4 stacks in four casts, without waiting for charges.
    /// Dev: both talents; Flight of the Navir, then four Cold Snaps back to back (no recharge in 7.5s); 4 stacks.
    /// </summary>
    [Fact]
    public void Free_Cold_Snaps_From_Navirs_Keeper_Still_Give_Glacial_Assault_Stacks()
    {
        var f = StepFights.Rime(talents: [NavirsKeeper.TalentId, GlacialAssault.TalentId]);
        f.Cast<FlightOfTheNavir>();

        for (int i = 0; i < 4; i++) Assert.True(f.TryCast<ColdSnap>().Succeeded, $"Cold Snap {i + 1}");

        Assert.Equal(GlacialAssault.MaxStacks, Stacks(f, GlacialAssault.Buff));
    }

    // ---- Cascading Blitz with Bursting Swallows

    /// <summary>
    /// Player: with Cascading Blitz and Bursting Swallows, during Ice Blitz a swallow that hits can trigger a Bursting Ice
    /// pulse, whose Anima sends another swallow, which can trigger another pulse, and so on, until a swallow does not
    /// trigger. A Cold Snap sends 3 swallows; if the first N swallow hits trigger, there are N pulses (N Anima) and N extra
    /// swallows, so 3 + N hits in all.
    /// Dev: both talents; the Bursting Swallows chance is forced on for the first N rolls and off after (a chain that
    /// always triggers never ends); Ice Blitz, then a Cold Snap, a 3s wait. Counts pulses, Anima gains and Frost Swallows
    /// hits, for N from 0 to 3.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cascading_Blitz_And_Bursting_Swallows_Chain_Until_A_Swallow_Does_Not_Trigger(int triggers)
    {
        var f = StepFights.Rime(talents: [CascadingBlitz.TalentId, BurstingSwallows.TalentId]);
        int rolls = 0;
        f.ForceProcs(key => key == BurstingSwallows.ProcKey ? rolls++ < triggers : null);
        f.Cast<IceBlitz>();

        f.Cast<ColdSnap>();
        f.Wait(3.0);

        Assert.Equal(triggers, f.Damage("Bursting Ice").Count);
        Assert.Equal(triggers, f.AnimaGains().Count);
        Assert.Equal(Rime.SwallowsPerWinterOrb + triggers, f.Damage("Frost Swallows").Count);
    }
}
