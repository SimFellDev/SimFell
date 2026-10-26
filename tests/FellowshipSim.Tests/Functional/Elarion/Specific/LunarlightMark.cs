using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Lunarlight Mark and Lunarlight Salvo: damage you deal to a marked enemy can call down a salvo, which takes one stack of the mark.
/// A hit has a 25% chance to trigger a salvo, and a crit 50%; Heartseeker Barrage's hits and Starfall Volley's have talents
/// that change that.
/// Dev: the chance of a salvo is read from the chance the engine gives the proc (<see cref="ForcedProcs.Rolls"/>), and the
/// proc is forced where a test needs a salvo or none. A hit is a crit when its crit chance is above 50% (the damage is the
/// expected value, crits are not rolled), so 150% crit gives crits and 0% gives none. The empirical rates use the dice (the
/// fight's seed is fixed, so they do not change from run to run) with a tolerance of 2 points over thousands of hits.
/// </summary>
public class ElarionLunarlightMark
{
    private const string SalvoProc = LunarlightSalvo.ProcKey;

    private static void Mark(StepDriver f, int stacks) => f.Enemy.Auras.Apply(LunarlightMark.Debuff, f.Now, stacks: stacks);

    private static int Stacks(StepDriver f) => f.Enemy.Auras.GetStacks(LunarlightMark.Debuff, f.Now);

    private static StepDriver Marked(int stacks, double crit = 0, int[]? talents = null, double spirit = 0, int enemies = 1)
    {
        var f = StepFights.Elarion(crit: crit, talents: talents, spirit: spirit, enemies: enemies);
        Mark(f, stacks);
        return f;
    }

    private static void NoImpetus(StepDriver f, Func<string, bool?>? also = null) =>
        f.ForceProcs(key => key == CelestialShot.ImpetusProcKey ? false : also?.Invoke(key));

    // ---- The chance of a salvo

    /// <summary>
    /// Player: a hit that does not crit has a 25% chance to call down a Lunarlight Salvo on a marked enemy.
    /// Dev: one stack of the mark; one Focused Shot with no crit chance; the salvo is rolled once, at 25%.
    /// </summary>
    [Fact]
    public void A_Hit_On_A_Marked_Enemy_Has_A_25_Percent_Chance_Of_A_Salvo()
    {
        var f = Marked(1);
        var procs = f.ForceProcs(_ => false);

        f.Cast<FocusedShot>();

        Assert.Equal(0.25, Assert.Single(procs.Rolls, r => r.Key == SalvoProc).Chance, 1e-9);
    }

    /// <summary>
    /// Player: a critical strike has a 50% chance to call down a Lunarlight Salvo.
    /// Dev: one stack of the mark; one Focused Shot at 150% crit (every hit crits); the salvo is rolled once, at 50%.
    /// </summary>
    [Fact]
    public void A_Crit_On_A_Marked_Enemy_Has_A_50_Percent_Chance_Of_A_Salvo()
    {
        var f = Marked(1, crit: 1.5);
        var procs = f.ForceProcs(_ => false);

        f.Cast<FocusedShot>();

        Assert.Equal(0.5, Assert.Single(procs.Rolls, r => r.Key == SalvoProc).Chance, 1e-9);
    }

    /// <summary>
    /// Player: a hit on an enemy with no mark never rolls a salvo.
    /// Dev: no mark; one Focused Shot; no salvo roll.
    /// </summary>
    [Fact]
    public void A_Hit_On_An_Unmarked_Enemy_Does_Not_Roll_A_Salvo()
    {
        var f = StepFights.Elarion();
        var procs = f.ForceProcs(_ => true);

        f.Cast<FocusedShot>();

        Assert.DoesNotContain(procs.Rolls, r => r.Key == SalvoProc);
        Assert.Empty(f.Damage("Lunarlight Salvo"));
    }

    /// <summary>
    /// Player: Heartseeker Barrage's hits have a 25% chance to call down a salvo, and with Lunar Fury twice that: 50%.
    /// Dev: one stack of the mark; one barrage on one enemy with the proc forced off; the first salvo roll is at 25%, and at 50% with
    /// Lunar Fury taken.
    /// </summary>
    [Theory]
    [InlineData(false, 0.25)]
    [InlineData(true, 0.5)]
    public void Lunar_Fury_Doubles_The_Salvo_Chance_Of_Heartseeker_Barrage(bool lunarFury, double chance)
    {
        var f = Marked(1, talents: lunarFury ? [ElarionTalents.LunarFury.TalentId] : null);
        var procs = f.ForceProcs(_ => false);

        f.Cast<HeartseekerBarrage>();

        Assert.Equal(chance, procs.Rolls.First(r => r.Key == SalvoProc).Chance, 1e-9);
    }

    /// <summary>
    /// Player: Lunarlight Affinity doubles the salvo chance of Starfall Volley's hits: 50% instead of 25%.
    /// Dev: one stack of the mark; one Starfall Volley with the proc forced off; the first salvo roll is at 50% with Lunarlight Affinity.
    /// TODO: the Python sim's Lunarlight Affinity. The game's talent (and the engine's) has Starfall Volley's salvos keep the mark's
    /// stack and Salvo crit more (+20%); it has no doubled chance. Check in game.
    /// </summary>
    [Fact(Skip = "The Python sim's Lunarlight Affinity doubles Starfall Volley's salvo chance; the game's talent does something else: to check in game")]
    public void Lunarlight_Affinity_Doubles_The_Salvo_Chance_Of_Starfall_Volley()
    {
        var f = Marked(1, talents: [ElarionTalents.LunarlightAffinity.TalentId]);
        var procs = f.ForceProcs(_ => false);

        f.Cast<StarfallVolley>();

        Assert.Equal(0.5, procs.Rolls.First(r => r.Key == SalvoProc).Chance, 1e-9);
    }

    // ---- What a salvo does to the mark

    /// <summary>
    /// Player: each salvo takes exactly one stack of Lunarlight Mark from the enemy.
    /// Dev: three stacks; one Focused Shot with the salvo forced; one Lunarlight Salvo hits and 2 stacks are left.
    /// </summary>
    [Fact]
    public void Each_Salvo_Takes_One_Stack()
    {
        var f = Marked(3);
        NoImpetus(f, key => key == SalvoProc ? true : null);

        f.Cast<FocusedShot>();

        Assert.Single(f.Damage("Lunarlight Salvo"));
        Assert.Equal(2, Stacks(f));
    }

    /// <summary>
    /// Player: the mark is gone when its last stack is used.
    /// Dev: one stack; one Focused Shot with the salvo forced; the enemy has no mark afterwards.
    /// </summary>
    [Fact]
    public void The_Mark_Is_Removed_When_Its_Last_Stack_Is_Used()
    {
        var f = Marked(1);
        NoImpetus(f, key => key == SalvoProc ? true : null);

        f.Cast<FocusedShot>();

        Assert.Single(f.Damage("Lunarlight Salvo"));
        Assert.False(f.Enemy.Auras.HasAura(LunarlightMark.Debuff, f.Now));
    }

    /// <summary>
    /// Player: a salvo does not call down another salvo: the hit of a salvo does not trigger the mark.
    /// Dev: three stacks; one Focused Shot with every salvo forced; exactly one salvo (not three), so 2 stacks are left.
    /// </summary>
    [Fact]
    public void A_Salvo_Does_Not_Trigger_Another_Salvo()
    {
        var f = Marked(3);
        NoImpetus(f, key => key == SalvoProc ? true : null);

        f.Cast<FocusedShot>();

        Assert.Single(f.Damage("Lunarlight Salvo"));
        Assert.Equal(2, Stacks(f));
    }

    // ---- Marks and salvos in the same cast

    /// <summary>
    /// Player: a Celestial Shot made free by Celestial Impetus puts 3 stacks of Lunarlight Mark on the target first, and its own hit
    /// can call down a salvo on them: 3 stacks, one used.
    /// Dev: Celestial Impetus applied (1 charge); one Celestial Shot on an unmarked enemy with the salvo forced; one salvo and 2 stacks.
    /// </summary>
    [Fact]
    public void A_Celestial_Shot_From_Celestial_Impetus_Can_Use_The_Marks_It_Applies()
    {
        var f = StepFights.Elarion();
        f.ForceProcs(key => key == SalvoProc ? true : null);
        f.Hero.Auras.Apply(CelestialShot.Impetus, f.Now);

        f.Cast<CelestialShot>();

        Assert.Single(f.Damage("Lunarlight Salvo"));
        Assert.Equal(2, Stacks(f));
    }

    /// <summary>
    /// Player: a Spirit Refund on a Celestial Shot puts 5 stacks of Lunarlight Mark on the target, and the shot's own hit can use one:
    /// 5 stacks, one used.
    /// Dev: 20% Spirit and the refund forced; one Celestial Shot on an unmarked enemy with the salvo forced; the mark ends with 4
    /// stacks and there was one salvo.
    /// TODO: the engine gives the refund 0.2 seconds after the spend, so the shot has landed by then, and the stacks are all 5 (no
    /// salvo); the Python sim marks first. Check in game.
    /// </summary>
    [Fact]
    public void A_Celestial_Shot_Can_Use_The_Marks_Its_Spirit_Refund_Applies()
    {
        var f = StepFights.Elarion(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc || key == SalvoProc ? true : null);

        f.Cast<CelestialShot>();
        f.Wait(0.5);

        Assert.Single(f.Damage("Lunarlight Salvo"));
        Assert.Equal(4, Stacks(f));
    }

    // ---- Empirical rates

    /// <summary>
    /// Player: over many hits, a marked enemy takes a salvo from 25% of the hits that do not crit and 50% of the crits: 25% x (1 + crit
    /// chance) of the hits.
    /// Dev: this base class casts the ability thousands of times at a marked enemy (20 stacks, put back before each cast; no
    /// Spirit, so no refund) with the dice deciding crits and the salvo, and compares the share of hits with a salvo with the
    /// expected rate within 2 points. A variant gives the ability, its talents and the factor they give.
    /// </summary>
    public abstract class SalvoRate
    {
        protected abstract int Casts { get; }
        protected abstract int[]? Talents { get; }
        protected abstract string Ability { get; }
        protected abstract double Factor { get; }
        protected abstract void Cast(StepDriver f);

        [Theory]
        [InlineData(0.1)]
        [InlineData(0.2)]
        [InlineData(0.4)]
        public void Salvos_Come_From_25_Percent_Times_1_Plus_Crit_Of_The_Hits(double crit)
        {
            var f = StepFights.Elarion(crit: crit, talents: Talents);
            f.RandomOutcomes().Mode = OutcomeMode.Discrete;
            for (int i = 0; i < Casts; i++)
            {
                f.Hero.Resources.Get(Elarion.Focus).Current = 100;
                f.Enemy.Auras.Apply(LunarlightMark.Debuff, f.Now, setStacks: LunarlightMark.DebuffMaxStacks);
                Cast(f);
            }

            double rate = f.Damage("Lunarlight Salvo").Count / (double)f.Damage(Ability).Count;

            Assert.Equal(Math.Min(1.0, 0.25 * (1 + crit) * Factor), rate, 0.02);
        }
    }

    public class CelestialShotSalvoRate : SalvoRate
    {
        protected override int Casts => 4000;
        protected override int[]? Talents => null;
        protected override string Ability => "Celestial Shot";
        protected override double Factor => 1.0;
        protected override void Cast(StepDriver f) => f.Cast<CelestialShot>();
    }

    public class StarfallVolleySalvoRate : SalvoRate
    {
        protected override int Casts => 600;
        protected override int[]? Talents => null;
        protected override string Ability => "Starfall Volley";
        protected override double Factor => 1.0;

        protected override void Cast(StepDriver f)
        {
            f.Hero.CooldownOf(f.Ability<StarfallVolley>()).ResetAll();
            f.Cast<StarfallVolley>();
        }
    }

    public class HeartseekerBarrageSalvoRate : SalvoRate
    {
        protected override int Casts => 400;
        protected override int[]? Talents => null;
        protected override string Ability => "Heartseeker Barrage";
        protected override double Factor => 1.0;

        protected override void Cast(StepDriver f)
        {
            f.Hero.CooldownOf(f.Ability<HeartseekerBarrage>()).ResetAll();
            f.Cast<HeartseekerBarrage>();
        }
    }

    public class LunarFuryHeartseekerBarrageSalvoRate : SalvoRate
    {
        protected override int Casts => 400;
        protected override int[]? Talents => [ElarionTalents.LunarFury.TalentId];
        protected override string Ability => "Heartseeker Barrage";
        protected override double Factor => 2.0;

        protected override void Cast(StepDriver f)
        {
            f.Hero.CooldownOf(f.Ability<HeartseekerBarrage>()).ResetAll();
            f.Cast<HeartseekerBarrage>();
        }
    }

    /// <summary>
    /// Player: Heartseeker Barrage's salvos erupt in 20% of cases, in a blast on the enemies around the target (up to 12 hit in all,
    /// the target included), instead of the salvo.
    /// Dev: 12 enemies, the target marked, one barrage with the salvo and the eruption forced; 12 eruption hits and no salvo.
    /// TODO: this is the Python sim. The engine, from combat logs, has the eruption 25% of the time after a salvo (not instead of
    /// it), on the enemies near the target and not on it. Check in game.
    /// </summary>
    [Fact(Skip = "The Python sim's eruption replaces the salvo, 20%, on 12 enemies; the engine's follows it, 25%, on the others: to check in game")]
    public void Heartseeker_Barrage_Salvo_Can_Erupt_On_12_Enemies_Instead()
    {
        var f = Marked(1, enemies: 12);
        f.ForceProcs(key => key == SalvoProc || key == LunarlightSalvo.EruptProcKey ? true : null);

        f.Cast<HeartseekerBarrage>();

        Assert.Equal(12, f.Damage("Lunarlight Salvo: Erupt").Count);
        Assert.Empty(f.Damage("Lunarlight Salvo"));
    }
}
