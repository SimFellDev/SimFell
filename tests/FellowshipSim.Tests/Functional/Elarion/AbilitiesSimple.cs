using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// One Elarion ability on its own: how its damage scales with his stats, how many enemies it hits, and what Event Horizon does to
/// damage. Abilities that work together, and talents, are in the other files.
/// Dev: damage is the expected value (crits are not rolled), so the ratios of two casts are exact. Area abilities have either a
/// hard cap on the enemies they hit or a soft one (every enemy is hit, for less once there are more than 12); Elarion's have the
/// soft one (the game data's "target count falloff"): damage per hit is multiplied by sqrt(12 / enemies) beyond 12.
/// </summary>
public class ElarionAbilitiesSimple
{
    private const double Ratio = 0.001;

    // ---- Damage scales with Agility, Expertise and Crit

    /// <summary>The (Agility, Expertise, Crit) sets of the Python sim's damage tests. Crit is above 100%, where the damage is exact.</summary>
    public static IEnumerable<object[]> StatSets()
    {
        string[] abilities = ["Focused Shot", "Celestial Shot", "Multishot", "Highwind Arrow", "Heartseeker Barrage", "Starfall Volley"];
        (double Main, double Expertise, double Crit)[] sets = [(1000, 0.00, 1.1), (1500, 0.05, 1.2), (2000, 0.10, 1.5), (1200, 0.20, 1.3)];
        foreach (var ability in abilities)
        foreach (var (main, expertise, crit) in sets) yield return [ability, main, expertise, crit];
    }

    private static double FirstHit(string ability, double main, double expertise, double crit)
    {
        var f = StepFights.Elarion(mainStat: main, expertise: expertise, crit: crit);
        switch (ability)
        {
            case "Focused Shot": f.Cast<FocusedShot>(); break;
            case "Celestial Shot": f.Cast<CelestialShot>(); break;
            case "Multishot": f.Cast<Multishot>(); break;
            case "Highwind Arrow": f.Cast<HighwindArrow>(); break;
            case "Heartseeker Barrage": f.Cast<HeartseekerBarrage>(); break;
            case "Starfall Volley": f.Cast<StarfallVolley>(); break;
            default: throw new ArgumentException(ability);
        }
        return f.Damage(ability)[0].Value;
    }

    /// <summary>
    /// Player: an ability's damage grows with your Agility, with your Expertise, and with your critical strike chance once that
    /// is above 100% (every hit then crits, and the chance beyond 100% adds to the crit damage): it is proportional to
    /// Agility / 1000 x (1 + Expertise) x (1 + Crit).
    /// Dev: the first hit of the ability with a stat set, against the same hit with 1000 Agility, no Expertise and 100% Crit
    /// (where the factor is 2); each of Focused Shot, Celestial Shot, Multishot, Highwind Arrow, Heartseeker Barrage (an arrow) and
    /// Starfall Volley (a hit) is checked with four sets of stats.
    /// </summary>
    [Theory]
    [MemberData(nameof(StatSets))]
    public void Damage_Scales_With_Agility_Expertise_And_Crit(string ability, double main, double expertise, double crit)
    {
        double reference = FirstHit(ability, 1000, 0, 1.0);

        double hit = FirstHit(ability, main, expertise, crit);

        Assert.Equal(main / 1000 * (1 + expertise) * (1 + crit) / 2, hit / reference, Ratio);
    }

    // ---- How many enemies each ability hits

    /// <summary>
    /// Player: Focused Shot hits its target only, however many enemies there are.
    /// Dev: three enemies; one Focused Shot on the first; one hit, on it.
    /// </summary>
    [Fact]
    public void Focused_Shot_Hits_Only_Its_Target()
    {
        var f = StepFights.Elarion(enemies: 3);

        f.Cast<FocusedShot>();

        Assert.Equal(["Training Mob 1"], f.Damage("Focused Shot").Select(e => e.Target));
    }

    /// <summary>
    /// Player: Celestial Shot hits its target only, however many enemies there are.
    /// Dev: three enemies; one Celestial Shot on the first; one hit, on it.
    /// </summary>
    [Fact]
    public void Celestial_Shot_Hits_Only_Its_Target()
    {
        var f = StepFights.Elarion(enemies: 3);

        f.Cast<CelestialShot>();

        Assert.Equal(["Training Mob 1"], f.Damage("Celestial Shot").Select(e => e.Target));
    }

    /// <summary>
    /// Player: Multishot fires an arrow at each enemy around your target, all for the same damage.
    /// Dev: three enemies; one Multishot; three hits, one on each, equal.
    /// </summary>
    [Fact]
    public void Multishot_Hits_Every_Enemy_For_The_Same_Damage()
    {
        var f = StepFights.Elarion(enemies: 3);

        f.Cast<Multishot>();

        var hits = f.Damage("Multishot");
        Assert.Equal(["Training Mob 1", "Training Mob 2", "Training Mob 3"], hits.Select(e => e.Target).Order());
        Assert.All(hits, h => Assert.Equal(hits[0].Value, h.Value, 1e-6));
    }

    /// <summary>
    /// Player: with more than 12 enemies Multishot's damage per arrow falls by sqrt(12 / enemies); with 12 or fewer it does not.
    /// Dev: damage of one arrow with one enemy as the reference; the same arrow with 12 enemies (equal) and with 24 (x sqrt(12 / 24)).
    /// </summary>
    [Fact]
    public void Multishot_Damage_Per_Arrow_Falls_Off_Beyond_12_Enemies()
    {
        var alone = StepFights.Elarion();
        alone.Cast<Multishot>();
        double reference = alone.Damage("Multishot")[0].Value;

        var twelve = StepFights.Elarion(enemies: 12);
        twelve.Cast<Multishot>();
        var twentyFour = StepFights.Elarion(enemies: 24);
        twentyFour.Cast<Multishot>();

        Assert.Equal(12, twelve.Damage("Multishot").Count);
        Assert.All(twelve.Damage("Multishot"), h => Assert.Equal(1.0, h.Value / reference, Ratio));
        Assert.Equal(24, twentyFour.Damage("Multishot").Count);
        Assert.All(twentyFour.Damage("Multishot"), h => Assert.Equal(Math.Sqrt(12.0 / 24), h.Value / reference, Ratio));
    }

    /// <summary>
    /// Player: Highwind Arrow hits its target and ricochets to up to 2 more enemies: 3 hits at most.
    /// Dev: 1, 2 and 8 enemies; one Highwind Arrow each; 1, 2 and 3 hits.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(8, 3)]
    public void Highwind_Arrow_Hits_Its_Target_And_Ricochets_To_Up_To_2_More(int enemies, int hits)
    {
        var f = StepFights.Elarion(enemies: enemies);

        f.Cast<HighwindArrow>();

        Assert.Equal(hits, f.Damage("Highwind Arrow").Count);
        Assert.Equal(hits, f.Damage("Highwind Arrow").Select(e => e.Target).Distinct().Count());
    }

    /// <summary>
    /// Player: Highwind Arrow's ricochets deal 50% of the first hit.
    /// Dev: 3 enemies; one Highwind Arrow; the second and third hits are 0.5 of the first. (Combat logs, and the game's constant
    /// BounceDamageMultiplierAfterFirstHit 0.5; the Python sim's earlier game version had 70%.)
    /// </summary>
    [Fact]
    public void Highwind_Arrow_Ricochets_Deal_50_Percent_Of_The_First_Hit()
    {
        var f = StepFights.Elarion(enemies: 3);

        f.Cast<HighwindArrow>();

        var hits = f.Damage("Highwind Arrow");
        Assert.Equal(0.5, hits[1].Value / hits[0].Value, Ratio);
        Assert.Equal(0.5, hits[2].Value / hits[0].Value, Ratio);
    }

    /// <summary>
    /// Player: Lunarlight Mark puts 3 stacks on your target and on up to 11 other enemies: 12 in all.
    /// Dev: 20 enemies; one Lunarlight Mark; 12 enemies have 3 stacks and 8 have none.
    /// </summary>
    [Fact]
    public void Lunarlight_Mark_Marks_Up_To_12_Enemies_With_3_Stacks()
    {
        var f = StepFights.Elarion(enemies: 20);

        f.Cast<LunarlightMark>();

        var stacks = f.Context.AllEnemies.Select(e => e.Auras.GetStacks(LunarlightMark.Debuff, f.Now)).ToList();
        Assert.Equal(12, stacks.Count(s => s == 3));
        Assert.Equal(8, stacks.Count(s => s == 0));
        Assert.Equal(3, f.Enemy.Auras.GetStacks(LunarlightMark.Debuff, f.Now));
    }

    /// <summary>
    /// Player: every pulse of Starfall Volley hits every enemy in the area.
    /// Dev: 5 enemies; one Starfall Volley and 10 seconds; every enemy is hit as many times, and there are several pulses. (How many
    /// pulses there are is a haste breakpoint, in HasteBreakpoints.cs.)
    /// </summary>
    [Fact]
    public void Starfall_Volley_Pulses_Hit_Every_Enemy()
    {
        var f = StepFights.Elarion(enemies: 5);

        f.Cast<StarfallVolley>();
        f.Wait(10);

        var hits = f.Damage("Starfall Volley");
        var perEnemy = hits.GroupBy(h => h.Target).Select(g => g.Count()).ToList();
        Assert.Equal(5, perEnemy.Count);
        Assert.Single(perEnemy.Distinct());
        Assert.True(perEnemy[0] > 1);
    }

    /// <summary>
    /// Player: with more than 12 enemies Starfall Volley's damage per hit falls by sqrt(12 / enemies).
    /// Dev: damage of the first hit with one enemy as the reference; the first hits with 12 enemies (equal) and 24 (x sqrt(12 / 24)).
    /// </summary>
    [Fact]
    public void Starfall_Volley_Damage_Per_Hit_Falls_Off_Beyond_12_Enemies()
    {
        var alone = StepFights.Elarion();
        alone.Cast<StarfallVolley>();
        double reference = alone.Damage("Starfall Volley")[0].Value;

        var twelve = StepFights.Elarion(enemies: 12);
        twelve.Cast<StarfallVolley>();
        var twentyFour = StepFights.Elarion(enemies: 24);
        twentyFour.Cast<StarfallVolley>();

        Assert.Equal(1.0, twelve.Damage("Starfall Volley")[0].Value / reference, Ratio);
        Assert.Equal(Math.Sqrt(12.0 / 24), twentyFour.Damage("Starfall Volley")[0].Value / reference, Ratio);
    }

    // ---- Arrows of a channel

    /// <summary>
    /// Player: every arrow of Heartseeker Barrage deals the same damage.
    /// Dev: one barrage at no haste; the damage of all its arrows is equal.
    /// </summary>
    [Fact]
    public void Every_Heartseeker_Barrage_Arrow_Deals_The_Same_Damage()
    {
        var f = StepFights.Elarion();

        f.Cast<HeartseekerBarrage>();
        f.Wait(1);

        var arrows = f.Damage("Heartseeker Barrage");
        Assert.True(arrows.Count >= 10);
        Assert.All(arrows, a => Assert.Equal(arrows[0].Value, a.Value, 1e-6));
    }

    // ---- Event Horizon

    /// <summary>
    /// Player: Event Horizon makes all your damage 20% higher.
    /// Dev: a Focused Shot's damage with no Event Horizon, and after one (100 Spirit Points set).
    /// </summary>
    [Fact]
    public void Event_Horizon_Raises_Damage_By_20_Percent()
    {
        var plain = StepFights.Elarion();
        plain.Cast<FocusedShot>();
        double reference = plain.Damage("Focused Shot")[0].Value;

        var f = StepFights.Elarion();
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
        f.Cast<EventHorizon>();
        f.Cast<FocusedShot>();

        Assert.Equal(1.2, f.Damage("Focused Shot")[0].Value / reference, Ratio);
    }

    /// <summary>
    /// Player: Event Horizon costs 100 Spirit Points, and you cannot cast it with fewer.
    /// Dev: 99 Spirit Points: the cast is refused; with 100 it works and the Spirit Points are spent.
    /// </summary>
    [Fact]
    public void Event_Horizon_Needs_And_Spends_100_Spirit_Points()
    {
        var f = StepFights.Elarion();
        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 99;
        Assert.False(f.TryCast<EventHorizon>().Succeeded);

        f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
        f.Cast<EventHorizon>();

        Assert.Equal(0, f.Hero.ResourceAmount(Hero.SpiritPoints), 1e-9);
    }
}
