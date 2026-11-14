using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Lunarlight Mark and Lunarlight Salvo: ability damage you deal to a marked enemy can call down a salvo, which takes a stack of the mark.
/// These tests are about which hits roll a salvo and at what chance, what a salvo does to the mark, and the casts that mark an enemy and
/// can call down a salvo on it at once.
///
/// What Lunarlight Mark itself applies, a Spirit Refund's marks, a Celestial Shot's marks from Celestial Impetus and Heartseeker Barrage's
/// eruption are in ElarionAbilities.cs; Lunar Fury and Lunarlight Affinity in ElarionTalents.cs.
/// </summary>
public class ElarionLunarlightMark
{
    // ---- The chance of a salvo

    /// <summary>A hit on an enemy with Lunarlight Mark has a 25% chance to call down a Lunarlight Salvo, and a critical strike 50%.</summary>
    [Theory]
    [InlineData(false, 0.25)]
    [InlineData(true, 0.5)]
    public void A_Hit_On_A_Marked_Enemy_Calls_Down_A_Salvo_25_Percent_Of_The_Time_And_A_Crit_50(bool crit, double chance)
    {
        var f = StepFights.Elarion();
        var procs = f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? false : null); // only to read the chance
        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy);

        f.Cast<FocusedShot>(crit: crit);

        Assert.Equal(chance, Assert.Single(procs.ChancesOf(LunarlightSalvo.ProcKey)), 1e-9);
    }

    /// <summary>
    /// Every hit of Elarion's abilities on a marked enemy can call down a salvo, at 25% when it does not crit.
    /// </summary>
    [Theory]
    [InlineData("Focused Shot")]
    [InlineData("Celestial Shot")]
    [InlineData("Multishot")]
    [InlineData("Highwind Arrow")]
    [InlineData("Heartseeker Barrage")]
    [InlineData("Starfall Volley")]
    public void Every_Hit_Of_An_Ability_On_A_Marked_Enemy_Can_Call_Down_A_Salvo(string ability)
    {
        var f = StepFights.Elarion();
        var procs = f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? false : null); // only to read the chances
        f.Set(Elarion.Focus, 100); // enough to cast it
        f.Apply(LunarlightMark.Debuff, stacks: LunarlightMark.Constants.MaxStacks, on: f.Enemy);

        f.Cast(ability);
        f.Wait(10.0); // every hit lands, the volley included

        var chances = procs.ChancesOf(LunarlightSalvo.ProcKey);

        using var check = new Checks(f);
        check.True(f.Damage(ability).Count > 0);
        check.Equal(f.Damage(ability).Count, chances.Count);
        check.True(chances.All(chance => chance == 0.25));
    }

    /// <summary>
    /// Every tick of a Kindling burn or an Amethyst Splinter on a marked enemy can call down a salvo, at 25% (the ticks do not crit).
    /// </summary>
    [Theory]
    [InlineData(Kindling.SigilId, "Kindling")]
    [InlineData(AmethystSplinters.AspectId, "Amethyst Splinter")]
    public void Every_Tick_Of_Kindling_Or_Amethyst_Splinters_On_A_Marked_Enemy_Can_Call_Down_A_Salvo(int augmentation, string ticks)
    {
        var f = StepFights.Elarion(augmentations: [new(augmentation, 1)]);
        var procs = f.ForceProcs(key => key == Kindling.ProcKey ? true // the shot lights the burn
            : key == LunarlightSalvo.ProcKey ? false // only to read the chances
            : null);

        f.Cast<FocusedShot>(crit: true); // lights the burn, and a crit makes the splinter, on an enemy not yet marked
        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy); // the shot's own hit had no mark: only the ticks roll
        f.Wait(10.0); // the burn or the splinter ticks out

        var chances = procs.ChancesOf(LunarlightSalvo.ProcKey);
        int tickCount = f.Damage(ticks).Count;

        using var check = new Checks(f);
        check.True(tickCount > 0, "it ticks");
        check.Equal(tickCount, chances.Count, what: "salvo rolls: each tick's");
        check.True(chances.All(chance => chance == 0.25), "each tick at 25%");
    }

    /// <summary>
    /// Shoot's hits never call down a salvo, even on a marked enemy.
    /// </summary>
    [Fact]
    public void Shoot_Never_Calls_Down_A_Salvo()
    {
        var f = StepFights.Elarion();
        var procs = f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : null);
        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy);

        f.Cast<Shoot>();

        using var check = new Checks(f);
        check.Equal(1, f.Damage<Shoot>().Count);
        check.Equal(0, procs.ChancesOf(LunarlightSalvo.ProcKey).Count);
        check.Equal(0, f.Damage("Lunarlight Salvo").Count);
    }

    /// <summary>A hit on an enemy with no Lunarlight Mark never calls down a salvo; once the enemy is marked, the same hit does.</summary>
    [Fact]
    public void A_Hit_On_An_Unmarked_Enemy_Never_Calls_Down_A_Salvo()
    {
        var f = StepFights.Elarion();
        var procs = f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : null);

        f.Cast<FocusedShot>();

        using var check = new Checks(f);
        check.Equal(0, procs.ChancesOf(LunarlightSalvo.ProcKey).Count);
        check.Equal(0, f.Damage("Lunarlight Salvo").Count);

        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy); // to see the salvo the same hit calls down on a marked enemy
        f.Cast<FocusedShot>();
        check.Equal(1, f.Damage("Lunarlight Salvo").Count);
    }

    // ---- What a salvo does to the mark

    /// <summary>Each Lunarlight Salvo takes one stack of Lunarlight Mark from its enemy.</summary>
    [Fact]
    public void Each_Salvo_Takes_One_Stack_Of_The_Mark()
    {
        var f = StepFights.Elarion();
        f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : null);
        f.Apply(LunarlightMark.Debuff, stacks: 3, on: f.Enemy);

        f.Cast<FocusedShot>();

        using var check = new Checks(f);
        check.Equal(1, f.Damage("Lunarlight Salvo").Count);
        check.Equal(2, f.Stacks(LunarlightMark.Debuff, on: f.Enemy));
    }

    /// <summary>When a salvo takes the last stack of Lunarlight Mark, the enemy is no longer marked.</summary>
    [Fact]
    public void The_Mark_Is_Gone_When_A_Salvo_Takes_Its_Last_Stack()
    {
        var f = StepFights.Elarion();
        f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : null);
        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy);

        f.Cast<FocusedShot>();

        using var check = new Checks(f);
        check.Equal(1, f.Damage("Lunarlight Salvo").Count);
        check.False(f.Has(LunarlightMark.Debuff, on: f.Enemy));
    }

    /// <summary>A salvo's own damage does not call down another salvo: only the shot's hit rolls one.</summary>
    [Fact]
    public void A_Salvo_Does_Not_Call_Down_Another_Salvo()
    {
        var f = StepFights.Elarion();
        var procs = f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : null); // every roll would call one down
        f.Apply(LunarlightMark.Debuff, stacks: 3, on: f.Enemy);

        f.Cast<FocusedShot>();

        using var check = new Checks(f);
        check.Equal(1, procs.ChancesOf(LunarlightSalvo.ProcKey).Count);
        check.Equal(1, f.Damage("Lunarlight Salvo").Count);
    }

    // ---- Marks and salvos in the same cast

    /// <summary>
    /// A Celestial Shot using Celestial Impetus marks its target before it hits, so its own hit can call down a salvo on an enemy that had
    /// no mark: 3 stacks, one taken.
    /// </summary>
    [Fact]
    public void A_Celestial_Shot_Using_Celestial_Impetus_Can_Call_Down_A_Salvo_On_The_Marks_It_Applies()
    {
        var f = StepFights.Elarion();
        f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : null);
        f.Apply(CelestialShot.Impetus); // as a Focused Shot's hit would give it

        f.Cast<CelestialShot>();

        using var check = new Checks(f);
        check.Equal(1, f.Damage("Lunarlight Salvo").Count);
        check.Equal(2, f.Stacks(LunarlightMark.Debuff, on: f.Enemy));
    }

    /// <summary>
    /// A Spirit Refund on a Celestial Shot marks the target before the shot hits, so the shot's own hit can call down a salvo on the 5
    /// stacks it applies: 5 stacks, one taken.
    /// </summary>
    [Fact]
    public void A_Celestial_Shot_Can_Call_Down_A_Salvo_On_The_Marks_Its_Spirit_Refund_Applies()
    {
        var f = StepFights.Elarion(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc || key == LunarlightSalvo.ProcKey ? true : null);

        f.Cast<CelestialShot>();

        using var check = new Checks(f);
        check.Equal(1, f.Damage("Lunarlight Salvo").Count);
        check.Equal(4, f.Stacks(LunarlightMark.Debuff, on: f.Enemy));
    }
}
