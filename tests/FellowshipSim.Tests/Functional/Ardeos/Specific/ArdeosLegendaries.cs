using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Ardeos;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;
using FellowshipSim.Core.Heroes.Ardeos.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Ardeos's legendary items, each worn on a bare Ardeos: Devouring Flame (his bracers), Fire Toad (his helm) and Explosivo (his helm). Any legendary also gives Mote of Creation's +10%
/// main stat, so a hero without one gets 10 more main stat on top of the base 100, to compare like with like. Explosivo's burn's tick speed and its
/// effect on Detonate's cast time are in Specific/ArdeosDotTickSpeed.cs and ArdeosCastTimes.cs.
/// </summary>
public class ArdeosLegendaries
{

    /// <summary>
    /// Cast some number of <paramref name="engulfingFlames"/> Engulfing Flames, with or without the devouring flame legendary.
    /// Measure the first detonate hit.
    /// </summary>
    private static double FirstDetonateHit(bool devouringFlame, int engulfingFlames)
    {
        var f = StepFights.Ardeos(
            legendaries: devouringFlame ? [DevouringFlame.LegsItemId] : null,
            mainStat: devouringFlame ? 0 : 10).WithoutDamageSpread(); // Mote of Creation's 10% of the base 100

        for (int i = 0; i < engulfingFlames; i++)
        {
            f.ResetCooldown<EngulfingFlames>(); // EF normally only has 2 charges
            f.Cast<EngulfingFlames>();
        }
        f.Set(Ardeos.Cinders, 100); // a Burning Ember to spend

        f.Cast<Detonate>();

        return f.Damage<Detonate>()[0].Value;
    }

    /// <summary>
    /// Devouring Flame's bonus stacks multiplicatively, 1.07 per Engulfing Flames.
    /// With K of them, Detonate deals base x K x 1.07^K, base being Detonate with one Engulfing Flames and no legendary.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Devouring_Flame_Multiplies_Detonate_Damage_1_07_Per_Engulfing_Flames(int engulfingFlames)
    {
        double baseHit = FirstDetonateHit(devouringFlame: false, engulfingFlames: 1);
        double legendaryHit = FirstDetonateHit(devouringFlame: true, engulfingFlames);

        AssertRatio(engulfingFlames * Math.Pow(1.07, engulfingFlames), legendaryHit / baseHit);
    }

    // ---- Fire Toad

    /// <summary>The hits of Fire Frogs' frogs and toads (not their burn's ticks), smallest first.</summary>
    private static List<double> FrogAndToadHits(StepDriver f) =>
        f.Ticks(FireFrogAttack.DamageEffectId).Select(hit => hit.Value).Order().ToList();

    /// <summary>
    /// With the Fire Toad legendary, each Fire Frog conjured has a 20% chance to come as a giant Fire Toad instead.
    /// </summary>
    [Fact]
    public void Fire_Toad_Gives_Each_Fire_Frog_A_20_Percent_Chance_To_Come_As_A_Toad()
    {
        var f = StepFights.Ardeos(legendaries: [FireToad.HeadItemId]);
        var procs = f.ForceProcs(key => key == FireFrogs.ToadProcKey ? false : null); // only the chance matters

        f.Cast<FireFrogs>();

        Assert.Equal([0.2, 0.2, 0.2, 0.2, 0.2], procs.ChancesOf(FireFrogs.ToadProcKey));
    }

    /// <summary>
    /// With the Fire Toad legendary, Fire Frogs always conjures one Fire Toad besides its 5 frogs, even when no frog comes as a toad:
    /// the 5 frogs' 3 attacks each, and one toad's explosion, bigger than a frog's attack.
    /// </summary>
    [Fact]
    public void Fire_Toad_Makes_Fire_Frogs_Conjure_One_Toad_Besides_Its_Frogs()
    {
        var f = StepFights.Ardeos(legendaries: [FireToad.HeadItemId]).WithoutDamageSpread();
        f.ForceProcs(key => key == FireFrogs.ToadProcKey ? false : null); // no frog comes as a toad

        f.Cast<FireFrogs>();
        f.Wait(6.0); // every frog has attacked
        var hits = FrogAndToadHits(f);
        double frogHit = hits[0];

        using var check = new Checks(f);
        check.Equal(15, hits.Count(hit => Math.Abs(hit - frogHit) < 1e-9));
        check.Equal(1, hits.Count(hit => hit > frogHit * 1.5));
        check.Equal(16, hits.Count);
    }

    /// <summary>One Fire Toad's explosion onto <paramref name="target"/>, as the cast's toad or a frog that came as one makes it: the hit
    /// and everything it triggers, with no Fire Frogs cast around it. The frog and the toad pick their enemy at random, so a test of the
    /// explosion itself picks it.</summary>
    private static void Explode(StepDriver f, EnemyTarget target) => f.Ability<FireFrogs>().ToadExplosion!(f.Context, target);

    /// <summary>
    /// A Fire Toad explodes onto its target for 900% of a Fire Frog attack's damage.
    ///
    /// Source: the datamined game constants (900% on the main target, 500% on the others), matching the tooltip, per the user.
    /// </summary>
    [Fact]
    public void Fire_Toad_Explodes_On_Its_Target_For_9_Times_A_Frog_Attack()
    {
        var f = StepFights.Ardeos(legendaries: [FireToad.HeadItemId]).WithoutDamageSpread();

        f.HitWith(f.Ability<FireFrogs>().Attack, f.Enemy); // one frog's attack
        Explode(f, f.Enemy);
        var hits = FrogAndToadHits(f);

        AssertRatio(9.0, hits[^1] / hits[0]);
    }

    /// <summary>
    /// A Fire Toad's explosion also hits every enemy near its target, for 500% of a Fire Frog attack's damage, reduced beyond 1 target:
    /// 500% x sqrt(1 / N), N the enemies it hits, its target among them.
    ///
    /// Source: the datamined game constants (500% on the others), matching the tooltip, per the user.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(12)]
    public void Fire_Toad_Explosion_Hits_The_Enemies_Near_Its_Target_For_5_Times_Sqrt_1_Over_N(int enemies)
    {
        double share = 5.0 * Math.Sqrt(1.0 / enemies);
        var f = StepFights.Ardeos(enemies: enemies, legendaries: [FireToad.HeadItemId]).WithoutDamageSpread();

        f.HitWith(f.Ability<FireFrogs>().Attack, f.Enemy); // one frog's attack
        Explode(f, f.Enemy);
        var hits = FrogAndToadHits(f);
        double frogHit = hits[0];
        var nearbyHits = hits.Where(hit => Math.Abs(hit / frogHit - share) < 0.001).ToList();

        Assert.Equal(enemies - 1, nearbyHits.Count);
    }

    /// <summary>
    /// Frog Squad makes Fire Frogs deal 20% more damage, and a Fire Toad's explosion on its target with it: the 20% multiplies the 900%.
    /// </summary>
    [Fact]
    public void Frog_Squad_Makes_Fire_Toads_Explosion_On_Its_Target_Deal_20_Percent_More()
    {
        double ExplosionHit(params int[] talents)
        {
            var f = StepFights.Ardeos(talents: talents, legendaries: [FireToad.HeadItemId]).WithoutDamageSpread();

            Explode(f, f.Enemy);

            return f.Ticks(FireFrogAttack.DamageEffectId).Single().Value;
        }

        AssertRatio(1.2, ExplosionHit(FrogSquad.TalentId) / ExplosionHit());
    }

    /// <summary>
    /// Frog Squad makes Fire Frogs deal 20% more damage, and a Fire Toad's explosion on the enemies near its target with it: the 20%
    /// multiplies the 500% x sqrt(1 / N).
    /// </summary>
    [Fact]
    public void Frog_Squad_Makes_Fire_Toads_Explosion_On_The_Enemies_Near_Its_Target_Deal_20_Percent_More()
    {
        double NearbyHit(params int[] talents)
        {
            var f = StepFights.Ardeos(enemies: 4, talents: talents, legendaries: [FireToad.HeadItemId]).WithoutDamageSpread();
            var nearby = f.Enemies()[1];

            Explode(f, f.Enemy);

            return f.Ticks(FireFrogAttack.DamageEffectId).Single(hit => hit.Target == nearby.Name).Value;
        }

        AssertRatio(1.2, NearbyHit(FrogSquad.TalentId) / NearbyHit());
    }

    /// <summary>
    /// A Fire Toad attacks once: it explodes on its target and on every enemy near it, and does not bounce from enemy to enemy as the
    /// frogs do. With 4 enemies, one explosion hits each enemy exactly once, its target included.
    ///
    /// Per the user.
    /// </summary>
    [Fact]
    public void Fire_Toad_Explodes_Once_On_Its_Target_And_Every_Nearby_Enemy()
    {
        var f = StepFights.Ardeos(enemies: 4, legendaries: [FireToad.HeadItemId]).WithoutDamageSpread();

        Explode(f, f.Enemy);
        var hits = f.Ticks(FireFrogAttack.DamageEffectId);

        using var check = new Checks(f);
        check.Equal(4, hits.Count);
        foreach (var enemy in f.Enemies())
            check.Equal(1, hits.Count(hit => hit.Target == enemy.Name), what: $"explosions on {enemy.Name}");
    }

    /// <summary>
    /// A Fire Frog that comes as a Fire Toad explodes once instead of making its attacks: with every frog a toad, Fire Frogs deals 6
    /// explosions (its 5 frogs' and its own toad's) and no frog attack.
    /// </summary>
    [Fact]
    public void A_Fire_Frog_That_Comes_As_A_Toad_Explodes_Once()
    {
        var f = StepFights.Ardeos(legendaries: [FireToad.HeadItemId]).WithoutDamageSpread();
        f.ForceProcs(key => key == FireFrogs.ToadProcKey ? true : null); // every frog comes as a toad

        f.Cast<FireFrogs>();
        f.Wait(6.0); // every toad has exploded
        var hits = FrogAndToadHits(f);

        using var check = new Checks(f);
        check.Equal(6, hits.Count);
        check.Ratio(1.0, hits[^1] / hits[0]); // all explosions, no smaller frog attack
    }

    /// <summary>
    /// A Fire Toad leaves the Fire Frogs burn on the enemy, as a frog's attack does.
    /// </summary>
    [Fact]
    public void Fire_Toad_Explosion_Leaves_The_Fire_Frogs_Burn()
    {
        var f = StepFights.Ardeos(legendaries: [FireToad.HeadItemId]);

        Explode(f, f.Enemy);
        f.Wait(6.0); // the burn ticks

        Assert.NotEmpty(f.Ticks(FireFrogsDot.EffectId));
    }

    // ---- Explosivo (its burn's tick speed is in ArdeosDotTickSpeed.cs)

    /// <summary>With the Explosivo legendary, Apocalypse has 2 charges, where it has 1 without it.</summary>
    [Fact]
    public void Explosivo_Gives_Apocalypse_2_Charges()
    {
        var f = StepFights.Ardeos(legendaries: [Explosivo.ChestItemId]);

        Assert.Equal(2, f.Charges<Apocalypse>());
    }

    /// <summary>With the Explosivo legendary, casting Apocalypse leaves its burn on every enemy in the fight.</summary>
    [Fact]
    public void Explosivo_Leaves_Apocalypses_Burn_On_Every_Enemy()
    {
        var f = StepFights.Ardeos(enemies: 3, legendaries: [Explosivo.ChestItemId]);

        f.Cast<Apocalypse>();

        using var check = new Checks(f);
        foreach (var enemy in f.Enemies())
            check.True(f.HasDebuff(Explosivo.DamageName, enemy), what: $"burn on {enemy.Name}");
    }

    /// <summary>Explosivo's burn deals half of the Apocalypse hit that lit it, in all.</summary>
    [Fact]
    public void Explosivos_Burn_Deals_50_Percent_Of_The_Apocalypse_Hit()
    {
        var f = StepFights.Ardeos(legendaries: [Explosivo.ChestItemId]).WithoutDamageSpread();

        f.Cast<Apocalypse>();
        f.Wait(15); // the burn ticks out

        AssertRatio(0.5, f.Damage(Explosivo.DamageName).Sum(tick => tick.Value) / f.Damage<Apocalypse>().Single().Value);
    }

    /// <summary>
    /// A second Apocalypse on a burning enemy adds to Explosivo's burn instead of replacing it: the burn deals half of both hits in all.
    /// </summary>
    [Fact]
    public void Explosivos_Burns_Accumulate()
    {
        var f = StepFights.Ardeos(legendaries: [Explosivo.ChestItemId]).WithoutDamageSpread();

        f.Cast<Apocalypse>();
        f.Cast<Apocalypse>(); // its second charge, the first burn still going
        f.Wait(15); // the burn ticks out

        var hits = f.Damage<Apocalypse>();
        using var check = new Checks(f);
        check.Equal(2, hits.Count, what: "Apocalypse hits");
        check.Ratio(0.5, f.Damage(Explosivo.DamageName).Sum(tick => tick.Value) / hits.Sum(hit => hit.Value));
    }

    /// <summary>Each Fire Ball cast takes 8 seconds off Apocalypse's cooldown: compared with the same moment of a fight without it.
    /// The 8 seconds are of cooldown, so they pass faster by the legendary's own cooldown recovery.</summary>
    [Fact]
    public void Each_Fire_Ball_Takes_8_Seconds_Off_Apocalypses_Cooldown()
    {
        double rate = 1;
        double RechargeRemainingAfter(bool fireBall)
        {
            var f = StepFights.Ardeos(legendaries: [Explosivo.ChestItemId]);
            f.FillResources(); // enough to cast Fire Ball
            f.Cast<Apocalypse>(); // a charge now recharging
            if (fireBall) f.Cast<FireBall>();
            f.WaitUntil(10);
            rate = f.AccelerationRate<Apocalypse>();
            return f.RechargeRemaining<Apocalypse>();
        }

        double without = RechargeRemainingAfter(fireBall: false);
        double with = RechargeRemainingAfter(fireBall: true);

        AssertSeconds(8 / rate, without - with);
    }
}
