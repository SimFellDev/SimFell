using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;
using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// One Mara ability on its own: its damage as a share of her Agility, how finishers grow with Combo Points, area falloff, Stealth and the poisons
/// and the bleed it applies, Maiden of Death.
/// Dev: damage is the expected value, so two hits' ratio is exact. The tooltips give each ability's damage as a percentage of Agility, so a hit is
/// compared with a Skittering Blades hit on one enemy (80.6% of Agility) rather than with an absolute number. No haste, so Creeping Death
/// (0.5% more poison and bleed damage per 1% haste) is out of the way.
/// </summary>
public class MaraAbilitiesSimple
{
    private const double Ratio = 0.001;

    /// <summary>A Skittering Blades hit on a single enemy with nothing else on: 80.6% of Agility.</summary>
    private static double Unit()
    {
        var f = StepFights.Mara();
        f.Cast<SkitteringBlades>();
        return f.FirstHit("Skittering Blades") / 0.806;
    }

    // ---- Damage of each ability

    /// <summary>
    /// Player: each ability deals the share of your Agility its tooltip gives: Queen's Fang 357.2% (with 1 Combo Point, which adds 20%),
    /// Arachnid Assault 117.22% (the same), Hemorrhaging Strike's hit 286.65%, Widow's Bite 145.2% with the right hand and 108% with the left,
    /// Skittering Blades 80.6%.
    /// Dev: one cast of each, with 1 Combo Point for the finishers; the first hit (the second for Widow's Bite's left hand) over a Skittering
    /// Blades hit, against the coefficients' ratio.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang", 3.572 * 1.2)]
    [InlineData("Arachnid Assault", 1.1722 * 1.2)]
    [InlineData("Hemorrhaging Strike", 2.8665)]
    [InlineData("Widow's Bite", 1.452)]
    public void Each_Ability_Deals_Its_Share_Of_Agility(string ability, double coefficient)
    {
        double unit = Unit();
        var f = StepFights.Mara();
        f.SetComboPoints(1);

        switch (ability)
        {
            case "Queen's Fang": f.Cast<QueensFang>(); break;
            case "Arachnid Assault": f.Cast<ArachnidAssault>(); break;
            case "Hemorrhaging Strike": f.Cast<HemorrhagingStrike>(); break;
            default: f.Cast<WidowsBite>(); break;
        }

        Assert.Equal(coefficient, f.FirstHit(ability) / unit, coefficient * Ratio);
    }

    /// <summary>
    /// Player: Widow's Bite strikes twice: 145.2% of Agility with the right hand, then 108% with the left.
    /// Dev: one Widow's Bite; 2 hits, the second 108 / 145.2 of the first.
    /// </summary>
    [Fact]
    public void Widows_Bite_Strikes_Twice_Right_Hand_Then_Left()
    {
        var f = StepFights.Mara();

        f.Cast<WidowsBite>();

        var hits = f.Damage("Widow's Bite");
        Assert.Equal(2, hits.Count);
        Assert.Equal(1.08 / 1.452, hits[1].Value / hits[0].Value, Ratio);
    }

    /// <summary>
    /// Player: Backstab deals 99.6% of Agility, and 40% more when you attack from behind.
    /// Dev: one Backstab; the hit over a Skittering Blades hit is 0.996 x 1.4 / 0.806.
    /// TODO: the engine always attacks from behind (the 40% is always applied, marked unverified there). Check in game whether the sim should.
    /// </summary>
    [Fact(Skip = "Backstab from behind: the engine always applies the 40%, unverified: to check in game")]
    public void Backstab_Deals_99_6_Percent_Of_Agility_Plus_40_Percent_From_Behind()
    {
        double unit = Unit();
        var f = StepFights.Mara();

        f.Cast<Backstab>();

        Assert.Equal(0.996 * 1.4, f.FirstHit("Backstab") / unit, Ratio);
    }

    // ---- Finishers

    /// <summary>
    /// Player: Queen's Fang and Arachnid Assault grow with the Combo Points spent: +20% for each, so +120% with 6.
    /// Dev: one finisher at 1 to 6 Combo Points; the hit over the hit at 1 Combo Point is (1 + 0.2 x points) / 1.2.
    /// </summary>
    [Theory]
    [InlineData("Queen's Fang", 2)]
    [InlineData("Queen's Fang", 6)]
    [InlineData("Arachnid Assault", 3)]
    [InlineData("Arachnid Assault", 6)]
    public void Finishers_Deal_20_Percent_More_Per_Combo_Point(string ability, int points)
    {
        double Hit(int cp)
        {
            var f = StepFights.Mara();
            f.SetComboPoints(cp);
            if (ability == "Queen's Fang") f.Cast<QueensFang>(); else f.Cast<ArachnidAssault>();
            return f.FirstHit(ability);
        }

        Assert.Equal((1 + 0.2 * points) / 1.2, Hit(points) / Hit(1), Ratio);
    }

    /// <summary>
    /// Player: only the bleed of Hemorrhaging Strike grows with the Combo Points spent (in duration); its hit does not.
    /// Dev: one Hemorrhaging Strike at 1 and at 6 Combo Points; the hits are equal.
    /// </summary>
    [Fact]
    public void Hemorrhaging_Strikes_Hit_Does_Not_Grow_With_Combo_Points()
    {
        double Hit(int cp)
        {
            var f = StepFights.Mara();
            f.SetComboPoints(cp);
            f.Cast<HemorrhagingStrike>();
            return f.FirstHit("Hemorrhaging Strike");
        }

        Assert.Equal(Hit(1), Hit(6), 1e-6);
    }

    // ---- Area damage

    /// <summary>
    /// Player: Skittering Blades and Arachnid Assault hit every enemy around you, and beyond 8 of them each hit is weaker: it is multiplied
    /// by sqrt(8 / enemies).
    /// Dev: 8 and 12 enemies; one cast; a hit on each enemy; the hits at 12 over a hit at 8 are sqrt(8 / 12).
    /// </summary>
    [Theory]
    [InlineData("Skittering Blades")]
    [InlineData("Arachnid Assault")]
    public void Area_Abilities_Hit_Every_Enemy_And_Fall_Off_Beyond_8(string ability)
    {
        List<StepEvent> Hits(int enemies)
        {
            var f = StepFights.Mara(enemies: enemies);
            f.SetComboPoints(3);
            if (ability == "Skittering Blades") f.Cast<SkitteringBlades>(); else f.Cast<ArachnidAssault>();
            return f.Damage(ability);
        }

        var eight = Hits(8);
        var twelve = Hits(12);

        Assert.Equal(8, eight.Count);
        Assert.Equal(12, twelve.Count);
        Assert.Equal(Math.Sqrt(8.0 / 12.0), twelve[0].Value / eight[0].Value, Ratio);
    }

    /// <summary>
    /// Player: Skittering Blades builds a Combo Point for each enemy it hits.
    /// Dev: 3 enemies; one cast; 3 Combo Points.
    /// </summary>
    [Fact]
    public void Skittering_Blades_Builds_A_Combo_Point_Per_Enemy_Hit()
    {
        var f = StepFights.Mara(enemies: 3);

        f.Cast<SkitteringBlades>();

        Assert.Equal(3, f.ComboPoints(), 0.01);
    }

    // ---- Hemorrhaging Strike's bleed

    /// <summary>
    /// Player: Hemorrhaging Strike's bleed lasts 12 seconds, and 3 seconds more for each Combo Point spent (30 seconds at 6).
    /// Dev: one Hemorrhaging Strike at 1, 3 and 6 Combo Points; the time left on the bleed after the cast, plus the time the cast took, is
    /// 12 + 3 x points.
    /// </summary>
    [Theory]
    [InlineData(1, 15)]
    [InlineData(3, 21)]
    [InlineData(6, 30)]
    public void The_Bleed_Lasts_12_Seconds_Plus_3_Per_Combo_Point(int points, double seconds)
    {
        var f = StepFights.Mara();
        f.SetComboPoints(points);
        var start = f.Now;

        f.Cast<HemorrhagingStrike>();

        var ctx = f.Context;
        Assert.Equal(seconds, f.MaraOf().Hemorrhage.Remaining(f.Enemy, ctx) + (f.Now - start).TotalSeconds, 0.01);
    }

    /// <summary>
    /// Player: the bleed deals 98.41% of Agility every 3 seconds.
    /// Dev: a bleed at no haste; its first tick over a Skittering Blades hit is 0.9841 / 0.806.
    /// </summary>
    [Fact]
    public void The_Bleed_Deals_98_41_Percent_Of_Agility_Per_Tick()
    {
        double unit = Unit();
        var f = StepFights.Mara();
        f.SetComboPoints(1);
        f.Cast<HemorrhagingStrike>();
        f.Wait(4);

        Assert.Equal(0.9841, f.FirstHit("Hemorrhage") / unit, 0.9841 * Ratio);
    }

    // ---- Stealth and the poisons

    /// <summary>
    /// Player: Brooding Shadows puts you in Stealth, and your next Backstab, Widow's Bite or Skittering Blades ends it.
    /// Dev: Brooding Shadows; in Stealth; then each of the three (a fresh fight each); not in Stealth afterwards.
    /// </summary>
    [Theory]
    [InlineData("Stealth Backstab")]
    [InlineData("Widow's Bite")]
    [InlineData("Stealth Skittering Blades")]
    public void An_Attack_Ends_Stealth(string ability)
    {
        var f = StepFights.Mara();
        f.Cast<BroodingShadows>();
        Assert.True(f.MaraOf().IsStealthed(f.Now));

        switch (ability)
        {
            case "Stealth Backstab": f.Cast<StealthBackstab>(); break;
            case "Widow's Bite": f.Cast<WidowsBite>(); break;
            default: f.Cast<StealthSkitteringBlades>(); break;
        }

        Assert.False(f.MaraOf().IsStealthed(f.Now));
    }

    /// <summary>
    /// Player: in Stealth Backstab and Skittering Blades are replaced by their Stealth versions, which only work in Stealth; and your auto
    /// attack does not swing.
    /// Dev: Backstab, Skittering Blades and the auto attack are refused in Stealth; the Stealth Backstab is refused outside it.
    /// </summary>
    [Fact]
    public void Stealth_Replaces_Backstab_And_Skittering_Blades_And_Stops_The_Auto_Attack()
    {
        var f = StepFights.Mara();
        Assert.False(f.TryCast<StealthBackstab>().Succeeded);
        Assert.False(f.TryCast<StealthSkitteringBlades>().Succeeded);

        f.Cast<BroodingShadows>();

        Assert.False(f.TryCast<Backstab>().Succeeded);
        Assert.False(f.TryCast<SkitteringBlades>().Succeeded);
        Assert.False(f.TryCast<Attack>().Succeeded);
    }

    /// <summary>
    /// Player: Backstab from Stealth applies Caustic Poison: 478.93% of Agility at once, always a critical strike (+100% crit chance), and
    /// it generates 6 Combo Points.
    /// Dev: a Stealth Backstab with no crit chance of her own; the Caustic Poison hit has crit chance 1; its damage over a Skittering Blades hit is
    /// 4.7893 / 0.806 times the crit factor (read as a Skittering Blades hit with 100% crit over one without); 6 Combo Points held.
    /// </summary>
    [Fact]
    public void Stealth_Backstab_Applies_Caustic_Poison()
    {
        double unit = Unit();
        var crit = StepFights.Mara(crit: 1.0);
        crit.Cast<SkitteringBlades>();
        double critFactor = crit.FirstHit("Skittering Blades") / 0.806 / unit;

        var f = StepFights.Mara();
        f.Cast<BroodingShadows>();
        f.Cast<StealthBackstab>();

        var hit = f.Damage("Caustic Poison").First();
        Assert.True(hit.CritChance >= 1.0);
        Assert.Equal(4.7893 * critFactor, hit.Value / unit, 4.7893 * critFactor * Ratio);
        Assert.Equal(6, f.ComboPoints(), 0.01);
    }

    /// <summary>
    /// Player: Widow's Bite from Stealth applies Seething Poison, 122.76% of Agility every 2 seconds for 60 seconds.
    /// Dev: a Widow's Bite from Stealth; the poison's first tick over a Skittering Blades hit is 1.2276 / 0.806; ticks 2 seconds apart.
    /// </summary>
    [Fact]
    public void Widows_Bite_From_Stealth_Applies_Seething_Poison()
    {
        double unit = Unit();
        var f = StepFights.Mara();
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>();
        f.Wait(5);

        var ticks = f.Damage("Seething Poison");
        Assert.True(ticks.Count >= 2);
        Assert.Equal(1.2276, ticks[0].Value / unit, 1.2276 * Ratio);
        Assert.Equal(2.0, (ticks[1].Time - ticks[0].Time).TotalSeconds, 0.002);
    }

    /// <summary>
    /// Player: Seething Poison can only be on one enemy at a time: putting it on another takes it off the first.
    /// Dev: 2 enemies; a Widow's Bite from Stealth on each; the poison ticks only on the second afterwards.
    /// </summary>
    [Fact]
    public void Seething_Poison_Is_On_One_Enemy_At_A_Time()
    {
        var f = StepFights.Mara(enemies: 2);
        var first = f.Context.AllEnemies[0];
        var second = f.Context.AllEnemies[1];
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>(first);
        f.Cast<BroodingShadows>();
        f.Cast<WidowsBite>(second);
        f.Wait(10);

        var ticks = f.Damage("Seething Poison");
        Assert.Equal(1, ticks.Select(t => t.Target).Where(t => t != null).Distinct().Count(t => t == second.Name));
        Assert.DoesNotContain(ticks.Where(t => t.Time > f.Now - SimTime.FromSeconds(8)), t => t.Target == first.Name);
    }

    /// <summary>
    /// Player: Skittering Blades from Stealth applies Volatile Poison to every enemy hit: 31.2% of Agility on each tick, and at the end it
    /// erupts for 124.8% (4 times a tick).
    /// Dev: 2 enemies; the Stealth Skittering Blades; ticks and eruption read by their effect IDs; each tick over a Skittering Blades hit is
    /// 0.312 / 0.806 and the eruption is 4 ticks; both enemies take them.
    /// </summary>
    [Fact]
    public void Skittering_Blades_From_Stealth_Applies_Volatile_Poison_To_Every_Enemy_Hit()
    {
        double unit = Unit();
        var f = StepFights.Mara(enemies: 2);
        f.Cast<BroodingShadows>();
        f.Cast<StealthSkitteringBlades>();
        f.Wait(10);

        var ticks = f.EffectTicks(1731);
        var eruptions = f.EffectTicks(1730);
        Assert.Equal(2, eruptions.Count);
        Assert.Equal(2, ticks.Select(t => t.Target).Distinct().Count());
        Assert.All(ticks, t => Assert.Equal(0.312, t.Value / unit, 0.312 * Ratio));
        Assert.All(eruptions, e => Assert.Equal(1.248, e.Value / unit, 1.248 * Ratio));
    }

    // ---- Maiden of Death

    /// <summary>
    /// Player: Maiden of Death makes you deal 20% more damage for 10 seconds.
    /// Dev: a Skittering Blades hit before it and one just after; 1.2 times; after 10 seconds the bonus is gone (1.0 times).
    /// </summary>
    [Fact]
    public void Maiden_Of_Death_Deals_20_Percent_More_Damage_For_10_Seconds()
    {
        var f = StepFights.Mara();
        f.SetEnergy(200);
        f.Cast<SkitteringBlades>();
        double before = f.Damage("Skittering Blades")[0].Value;

        f.Cast<MaidenOfDeath>();
        f.Cast<SkitteringBlades>();
        double during = f.Damage("Skittering Blades")[1].Value;
        f.Wait(10);
        f.Cast<SkitteringBlades>();
        double after = f.Damage("Skittering Blades")[2].Value;

        Assert.Equal(1.2, during / before, Ratio);
        Assert.Equal(1.0, after / before, Ratio);
    }

    /// <summary>
    /// Player: while Maiden of Death is active every Combo Point generator generates 6 Combo Points.
    /// Dev: Maiden of Death, then one Backstab, Skittering Blades or Widow's Bite (a fresh fight each); 6 Combo Points.
    /// </summary>
    [Theory]
    [InlineData("Backstab")]
    [InlineData("Skittering Blades")]
    [InlineData("Widow's Bite")]
    public void Maiden_Of_Death_Makes_Generators_Generate_6_Combo_Points(string ability)
    {
        var f = StepFights.Mara();
        f.Cast<MaidenOfDeath>();

        switch (ability)
        {
            case "Backstab": f.Cast<Backstab>(); break;
            case "Skittering Blades": f.Cast<SkitteringBlades>(); break;
            default: f.Cast<WidowsBite>(); break;
        }

        Assert.Equal(6, f.ComboPoints(), 0.01);
    }
}
