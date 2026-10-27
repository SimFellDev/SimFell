using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's abilities together: Rend from several sources, Owed in Blood with Rend, Exsanguinate and Slaughter on a Rend built by others, Reign in
/// Blood with Serrated Edge, and what a Spirit Refund brings.
/// Dev: damage is the expected value, so ratios are exact; Spirit Refund is forced. Rend is read from the enemy (<c>RendLeft</c>) and its ticks.
/// </summary>
public class GundeAbilitiesComplex
{
    private const double Ratio = 0.001;

    private static readonly double Strength = GundeFights.StrengthUnit();

    private static double Sum(IEnumerable<StepEvent> events) => events.Sum(e => e.Value);

    private static double RendTotal(StepDriver f) => f.RendLeft() + Sum(f.Damage("Rend"));

    // ---- Rend from several sources

    /// <summary>
    /// Player: Rend adds up: the applications of different abilities are one pool, and every tick deals a tenth of what the applications still ticking
    /// hold.
    /// Dev: a Reaver's Edge and a Heart Splitter; the first tick (3s) is a tenth of the two applications.
    /// </summary>
    [Fact]
    public void Applications_Of_Different_Abilities_Tick_Together()
    {
        var f = StepFights.Gunde();
        f.Cast<ReaversEdge>();
        f.Cast<HeartSplitter>();
        double applied = 0.5 * f.FirstHitOf("Reaver's Edge") + 1.0 * f.FirstHitOf("Heart Splitter");

        f.Wait(1.5);

        Assert.Equal(applied / 10, f.Damage("Rend")[0].Value, applied * Ratio);
    }

    /// <summary>
    /// Player: Serrated Edge and Reign in Blood add up on the same ability: Heart Splitter under Reign in Blood with Serrated Edge applies 100% + 30% + 20%
    /// of its damage to Rend.
    /// Dev: a Blood Arc and a Reign in Blood, then a Heart Splitter; its Rend is 1.5 times its damage.
    /// </summary>
    [Fact]
    public void Reign_In_Blood_And_Serrated_Edge_Add_Up()
    {
        var f = StepFights.Gunde();
        f.Cast<BloodArc>();
        f.Cast<ReignInBlood>();
        double before = RendTotal(f);

        f.Cast<HeartSplitter>();

        Assert.Equal(1.5, (RendTotal(f) - before) / f.FirstHitOf("Heart Splitter"), Ratio);
    }

    // ---- Owed in Blood with Rend

    /// <summary>
    /// Player: the Rend stacks Owed in Blood adds are Rend like any other: the enemy shows them as stacks of Rend, and they tick in tenths.
    /// Dev: 10 stacks of Owed in Blood; one cast; the enemy holds 10 stacks of Rend; the first tick is a tenth of the Rend.
    /// </summary>
    [Fact]
    public void Owed_In_Blood_Stacks_Are_Rend_Stacks_That_Tick()
    {
        var f = StepFights.Gunde();
        f.SetOwedInBlood(10);

        f.Cast<OwedInBlood>();
        Assert.Equal(10, f.Enemy.Auras.GetStacks(Rend.Debuff, f.Now));

        f.Wait(3);

        Assert.Equal(10 * Strength / 10, f.Damage("Rend")[0].Value, Strength * Ratio);
    }

    /// <summary>
    /// Player: Heart Splitter's Exsanguinate counts the Rend Owed in Blood put on the target: 30% of it and the strike's own.
    /// Dev: 10 stacks of Owed in Blood on the target, then a Heart Splitter; Exsanguinate is 30% of (10 Strength + the strike's Rend).
    /// </summary>
    [Fact]
    public void Exsanguinate_Counts_The_Rend_From_Owed_In_Blood()
    {
        var f = StepFights.Gunde();
        f.SetOwedInBlood(10);
        f.Cast<OwedInBlood>();

        f.Cast<HeartSplitter>();

        double strike = f.FirstHitOf("Heart Splitter");
        Assert.Equal(0.3 * (10 * Strength + strike), f.FirstHitOf("Exsanguinate"), strike * Ratio);
    }

    /// <summary>
    /// Player: Slaughter turns the Rend from Owed in Blood into its bleed: 160% of it.
    /// Dev: 10 stacks of Owed in Blood, then Slaughter; the bleed is 160% of 10 Strength.
    /// </summary>
    [Fact]
    public void Slaughter_Bleeds_The_Rend_From_Owed_In_Blood()
    {
        var f = StepFights.Gunde();
        f.SetOwedInBlood(10);
        f.Cast<OwedInBlood>();

        f.Cast<Slaughter>();
        f.Wait(3.5);

        Assert.Equal(1.6 * 10 * Strength, Sum(f.DamageOfEffect(3237)), Strength * Ratio);
    }

    // ---- Spirit Refund

    /// <summary>
    /// Player: a Spirit Refund gives Rising Spirit: 5% more Strength for 10 seconds.
    /// Dev: Spirit Refund forced on; a Reaver's Edge; a Warbound after it is 5% stronger than one without.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Rising_Spirit()
    {
        var plain = StepFights.Gunde(spirit: 0.2);
        plain.ForceProcs(key => key == Hero.SpiritRefundProc ? false : null);
        plain.Cast<ReaversEdge>();
        plain.Cast<Warbound>();
        var f = StepFights.Gunde(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.Cast<ReaversEdge>();

        f.Cast<Warbound>();

        Assert.Equal(1.05, f.FirstHitOf("Warbound") / plain.FirstHitOf("Warbound"), Ratio);
    }

    /// <summary>
    /// Player: the Blood Feathers of a Spirit Refund come with the first hit of the cast, not after its last: a Grim Carve's refund is there after its
    /// first spin.
    /// Dev: Spirit Refund forced on; a Grim Carve; at 0.3s (after the first spin only) 1 Spirit Point.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Comes_With_The_First_Hit()
    {
        var f = StepFights.Gunde(spirit: 0.2);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : null);
        f.SetOwedInBlood(0);

        f.Context.BeginCast(f.Hero, f.Ability<GrimCarve>(), f.Enemy);
        f.Wait(0.3);

        Assert.Single(f.Damage("Grim Carve"));
        Assert.Equal(1, f.Hero.ResourceAmount(Hero.SpiritPoints), 0.01);
    }
}
