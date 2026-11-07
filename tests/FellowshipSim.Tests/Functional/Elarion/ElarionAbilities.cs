using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's abilities, on a bare Elarion (no talents unless the test is about one): each ability's own rule first, then how it works with
/// the others. Focus gains and costs, cast times and cooldowns have their own files; Event Horizon's cost and Spirit of Heroism, which
/// every ultimate shares, are in Generic/Ultimate.cs, and what every hero's Spirit Refund gives in Generic/SpiritRefund.cs.
/// </summary>
public class ElarionAbilities
{
    // ---- Damage and targeting

    /// <summary>Focused Shot and Celestial Shot hit only the enemy you target, once, however many enemies there are.</summary>
    [Theory]
    [InlineData("Focused Shot")]
    [InlineData("Celestial Shot")]
    public void Single_Target_Abilities_Hit_Only_The_Chosen_Enemy(string ability)
    {
        var f = StepFights.Elarion(enemies: 3);
        var second = f.Enemies()[1]; // not the default target, so hitting the default target would show

        f.Cast(ability, target: second);
        f.Wait(1.0); // the arrow lands

        Assert.Equal([second.Name], f.Damage(ability).Select(hit => hit.Target));
    }

    // ---- Multishot

    /// <summary>Multishot fires an arrow at each enemy around your target, all for the same damage.</summary>
    [Fact]
    public void Multishot_Hits_Every_Enemy_For_The_Same_Damage()
    {
        var f = StepFights.Elarion(enemies: 3).WithoutDamageSpread();

        f.Cast<Multishot>();

        var hits = f.Damage<Multishot>();

        using var check = new Checks(f);
        check.Equal(3, hits.Select(hit => hit.Target).Distinct().Count());
        check.Equal(3, hits.Count);
        foreach (var hit in hits) check.Ratio(1.0, hit.Value / hits[0].Value, what: $"arrow on {hit.Target}");
    }

    /// <summary>
    /// An Empowered Multishot, here from Skystrider's Supremacy, fires at least 3 arrows: with fewer enemies around, the extra arrows go to
    /// the enemies struck again, in turn.
    /// </summary>
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    public void An_Empowered_Multishot_Fires_At_Least_3_Arrows(int enemies, int arrows)
    {
        var f = StepFights.Elarion(enemies: enemies);

        f.Cast<SkystridersSupremacy>();
        f.Cast<Multishot>();

        Assert.Equal(arrows, f.Damage<Multishot>().Count);
    }

    // ---- Celestial Impetus (its free Celestial Shot is in ElarionResourceEconomy.cs)

    /// <summary>You hold at most 2 charges of Celestial Impetus: a third proc while you hold 2 is lost.</summary>
    [Fact]
    public void Celestial_Impetus_Holds_At_Most_2_Charges()
    {
        var f = StepFights.Elarion();
        f.ForceProcs(key => key == CelestialShot.ImpetusProcKey ? true : null); // every shot grants a charge

        f.Cast<FocusedShot>();
        f.Cast<FocusedShot>();
        f.Cast<FocusedShot>();
        f.Wait(1.0); // the last arrow lands

        Assert.Equal(2, f.Stacks(CelestialShot.Impetus));
    }

    /// <summary>
    /// A Celestial Shot using Celestial Impetus puts 3 stacks of Lunarlight Mark on its target.
    ///
    /// Whether its own hit can call down a salvo on them is in Specific/ElarionLunarlightMark.cs.
    /// </summary>
    [Fact]
    public void A_Celestial_Shot_Using_Celestial_Impetus_Marks_Its_Target_With_3_Stacks()
    {
        var f = StepFights.Elarion();
        f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? false : null); // a salvo would take a stack
        f.Apply(CelestialShot.Impetus); // as a Focused Shot's hit would give it

        f.Cast<CelestialShot>();

        Assert.Equal(3, f.Stacks(LunarlightMark.Debuff, on: f.Enemy));
    }

    // ---- Highwind Arrow

    /// <summary>Highwind Arrow hits its target and ricochets to up to 2 more enemies, each struck once: 3 hits at most.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(8, 3)]
    public void Highwind_Arrow_Hits_Its_Target_And_Ricochets_To_Up_To_2_More(int enemies, int hits)
    {
        var f = StepFights.Elarion(enemies: enemies);

        f.Cast<HighwindArrow>();
        f.Wait(1.0); // the ricochets land

        using var check = new Checks(f);
        check.Equal(hits, f.Damage<HighwindArrow>().Count);
        check.Equal(hits, f.Damage<HighwindArrow>().Select(hit => hit.Target).Distinct().Count());
        check.Equal(1, f.Damage<HighwindArrow>().Count(hit => hit.Target == f.Enemy.Name));
    }

    /// <summary>
    /// Each of Highwind Arrow's ricochets deals 50% of the first hit.
    ///
    /// From combat logs and the game data (Bowguy constant BounceDamageMultiplierAfterFirstHit 0.5).
    /// </summary>
    [Fact]
    public void Highwind_Arrow_Ricochets_Deal_50_Percent_Of_The_First_Hit()
    {
        var f = StepFights.Elarion(enemies: 3).WithoutDamageSpread();

        f.Cast<HighwindArrow>();
        f.Wait(1.0); // the ricochets land

        var hits = f.Damage<HighwindArrow>();

        using var check = new Checks(f);
        check.Ratio(0.5, hits[1].Value / hits[0].Value);
        check.Ratio(0.5, hits[2].Value / hits[0].Value);
    }

    // ---- Heartseeker Barrage (how many arrows it fires at each haste is in ElarionHasteBreakpoints.cs)

    /// <summary>Every arrow of Heartseeker Barrage deals the same damage.</summary>
    [Fact]
    public void Every_Heartseeker_Barrage_Arrow_Deals_The_Same_Damage()
    {
        var f = StepFights.Elarion().WithoutDamageSpread();

        f.Cast<HeartseekerBarrage>();
        f.Wait(1.0); // the last arrow lands

        var arrows = f.Damage<HeartseekerBarrage>();

        using var check = new Checks(f);
        check.True(arrows.Count > 1);
        for (int i = 1; i < arrows.Count; i++) check.Ratio(1.0, arrows[i].Value / arrows[0].Value, what: $"arrow {i + 1}");
    }

    /// <summary>
    /// A Lunarlight Salvo called down by Heartseeker Barrage can erupt, striking every enemy near its target but not the target itself.
    ///
    /// The eruption follows the salvo, 25% of the time (combat logs, NOTES.md); the 25% is not read here.
    /// </summary>
    [Fact]
    public void A_Heartseeker_Barrage_Salvo_Can_Erupt_On_The_Enemies_Near_Its_Target()
    {
        var f = StepFights.Elarion(enemies: 12);
        f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true // one salvo, on the mark's only stack
            : key == LunarlightSalvo.EruptProcKey ? true
            : null);
        f.Apply(LunarlightMark.Debuff, stacks: 1, on: f.Enemy); // a salvo needs a marked enemy

        f.Cast<HeartseekerBarrage>();
        f.Wait(1.0); // the last arrow lands

        var eruption = f.Damage("Lunarlight Salvo: Erupt");

        using var check = new Checks(f);
        check.Equal(1, f.Damage("Lunarlight Salvo").Count);
        check.Equal(11, eruption.Count);
        check.Equal(11, eruption.Select(hit => hit.Target).Distinct().Count());
        check.False(eruption.Any(hit => hit.Target == f.Enemy.Name));
    }

    // ---- Starfall Volley (how many times it strikes at each haste is in ElarionHasteBreakpoints.cs)

    /// <summary>Every strike of Starfall Volley hits every enemy in the area, so each enemy is hit as many times.</summary>
    [Fact]
    public void Every_Starfall_Volley_Strike_Hits_Every_Enemy()
    {
        var f = StepFights.Elarion(enemies: 5);

        f.Cast<StarfallVolley>();
        f.Wait(10.0); // the volley ends

        var hitsPerEnemy = f.Damage<StarfallVolley>().GroupBy(hit => hit.Target).Select(g => g.Count()).ToList();

        using var check = new Checks(f);
        check.Equal(5, hitsPerEnemy.Count, what: "enemies hit");
        check.True(hitsPerEnemy[0] > 1);
        check.Equal(1, hitsPerEnemy.Distinct().Count(), what: "different hit counts");
    }

    /// <summary>
    /// A second Starfall Volley cast while the first is still falling falls with it: each strikes as often as a volley alone, and each
    /// ends 8 seconds after its own cast.
    ///
    /// The cooldown is reset to cast the second volley 1.5 seconds after the first. In the game, Repeating Stars brings volleys this close;
    /// Skylit Grace's talent text confirms volleys overlap.
    /// </summary>
    [Fact]
    public void Two_Starfall_Volleys_Fall_At_Once_And_Each_Ends_8_Seconds_After_Its_Cast()
    {
        var aloneScenario = StepFights.Elarion();

        aloneScenario.Cast<StarfallVolley>();
        aloneScenario.Wait(12.0); // the volley ends
        int aloneHits = aloneScenario.Damage<StarfallVolley>().Count;

        var overlapScenario = StepFights.Elarion();

        overlapScenario.Cast<StarfallVolley>();
        double firstEnd = overlapScenario.CompletedAt("Starfall Volley") + 8.0;
        overlapScenario.ResetCooldown<StarfallVolley>(); // ready again at once
        overlapScenario.Cast<StarfallVolley>();
        double secondEnd = overlapScenario.CompletedAt("Starfall Volley") + 8.0;
        overlapScenario.Wait(12.0); // both volleys end
        var hits = overlapScenario.Damage<StarfallVolley>();

        using var check = new Checks(overlapScenario);
        check.Equal(2 * aloneHits, hits.Count);
        check.True(hits.Any(hit => hit.Time.TotalSeconds > firstEnd && hit.Time.TotalSeconds <= secondEnd), "the second volley strikes after the first ends");
        check.False(hits.Any(hit => hit.Time.TotalSeconds > secondEnd), "a hit after the second volley ends");
    }

    // ---- Skystrider's Grace (its cooldown is in ElarionCooldowns.cs)

    /// <summary>Skystrider's Grace gives you 30% haste for 20 seconds.</summary>
    [Fact]
    public void Skystriders_Grace_Gives_30_Percent_Haste_For_20_Seconds()
    {
        var f = StepFights.Elarion();

        f.Cast<SkystridersGrace>();
        double start = f.CompletedAt("Skystrider's Grace");

        using var check = new Checks(f);
        check.Equal(0.3, f.Haste());
        f.WaitUntil(start + 19.9);
        check.Equal(0.3, f.Haste());
        f.WaitUntil(start + 20.1);
        check.Equal(0.0, f.Haste());
    }

    // ---- Lunarlight Mark

    /// <summary>Lunarlight Mark puts 3 stacks on your target and on up to 11 other enemies: 12 enemies in all.</summary>
    [Fact]
    public void Lunarlight_Mark_Marks_Up_To_12_Enemies_With_3_Stacks()
    {
        var f = StepFights.Elarion(enemies: 20);

        f.Cast<LunarlightMark>();

        var stacks = f.Enemies().Select(enemy => f.Stacks(LunarlightMark.Debuff, on: enemy)).ToList();

        using var check = new Checks(f);
        check.Equal(3, f.Stacks(LunarlightMark.Debuff, on: f.Enemy));
        check.Equal(12, stacks.Count(s => s == 3));
        check.Equal(8, stacks.Count(s => s == 0));
    }

    // ---- Spirit Refund (the chance, Focus and Spirit Points every hero's refund gives are in Generic/SpiritRefund.cs)

    /// <summary>A Spirit Refund puts 5 stacks of Lunarlight Mark on your target and 2 on up to two other enemies.</summary>
    [Fact]
    public void A_Spirit_Refund_Marks_The_Target_With_5_Stacks_And_2_Other_Enemies_With_2()
    {
        var f = StepFights.Elarion(enemies: 4);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true
            : key == LunarlightSalvo.ProcKey ? false // a salvo would take a stack
            : null);

        f.Cast<CelestialShot>();
        f.Wait(0.5); // the refund lands

        var others = f.Enemies().Where(enemy => enemy != f.Enemy).Select(enemy => f.Stacks(LunarlightMark.Debuff, on: enemy)).ToList();

        using var check = new Checks(f);
        check.Equal(5, f.Stacks(LunarlightMark.Debuff, on: f.Enemy));
        check.Equal(2, others.Count(s => s == 2));
        check.Equal(1, others.Count(s => s == 0));
    }

    // ---- Event Horizon (its cost and Spirit of Heroism, shared by every hero's ultimate, are in Generic/Ultimate.cs; its cast time in
    // ElarionCastTimes.cs, its halved Focus costs in ElarionResourceEconomy.cs, its effects on cooldowns in
    // Specific/ElarionEventHorizonCooldown.cs)

    /// <summary>Event Horizon makes you deal 20% more damage for 20 seconds.</summary>
    [Fact]
    public void Event_Horizon_Gives_20_Percent_More_Damage_For_20_Seconds()
    {
        var plainScenario = StepFights.Elarion().WithoutDamageSpread();

        plainScenario.Cast<FocusedShot>();
        double plainShotDamage = plainScenario.Damage<FocusedShot>().Single().Value;

        var horizonScenario = StepFights.Elarion().WithoutDamageSpread();
        horizonScenario.Set(Hero.SpiritPoints, 100); // enough to cast it

        horizonScenario.Cast<EventHorizon>();
        double start = horizonScenario.CompletedAt("Event Horizon");

        using var check = new Checks(horizonScenario);
        check.True(horizonScenario.Has(EventHorizon.Buff));

        horizonScenario.Cast<FocusedShot>(); // to measure the bonus
        double horizonShotDamage = horizonScenario.Damage<FocusedShot>().Single().Value;
        check.Ratio(1.2, horizonShotDamage / plainShotDamage);

        horizonScenario.WaitUntil(start + 19.9);
        check.True(horizonScenario.Has(EventHorizon.Buff));
        horizonScenario.WaitUntil(start + 20.1);
        check.False(horizonScenario.Has(EventHorizon.Buff));
    }

    /// <summary>
    /// Event Horizon cannot be cast in the middle of another cast: 0.5 seconds into a Focused Shot it is refused, the Focused Shot still
    /// lands, and Event Horizon's buff never comes.
    /// </summary>
    [Fact]
    public void Event_Horizon_Cannot_Interrupt_Another_Cast()
    {
        var f = StepFights.Elarion();

        f.Set(Hero.SpiritPoints, 100); // enough to cast it

        f.StartCast<FocusedShot>();
        f.Wait(0.5); // a third of the way through the cast

        var attempt = f.TryCast<EventHorizon>();
        f.Wait(2.0); // the Focused Shot lands

        using var check = new Checks(f);
        check.False(attempt.Succeeded);
        check.True(attempt.Reason?.Contains("already casting") == true, "refused as already casting");
        check.Equal(1, f.Damage<FocusedShot>().Count);
        check.False(f.Has(EventHorizon.Buff));
    }

    // ---- Area damage reduction: past a number of enemies, each enemy takes the hit times sqrt(threshold / enemies)

    /// <summary>Multishot loses damage once it hits more than 12 enemies: each enemy takes the arrow's damage times sqrt(12 / enemies).</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(24)]
    [InlineData(48)]
    public void Multishot_Loses_Damage_Past_12_Enemies(int enemies)
    {
        StepDriver Fight(int n)
        {
            var f = StepFights.Elarion(enemies: n).WithoutDamageSpread();

            f.Cast<Multishot>();
            return f;
        }

        double alone = Fight(1).FirstHitOnEachEnemy("Multishot").Single();
        var crowd = Fight(enemies).FirstHitOnEachEnemy("Multishot");

        double expected = enemies <= 12 ? 1.0 : Math.Sqrt(12.0 / enemies);

        using var check = new Checks();
        check.Equal(enemies, crowd.Count, what: "enemies hit");
        for (int i = 0; i < crowd.Count; i++)
            check.Ratio(expected, crowd[i] / alone, what: $"enemy {i + 1}");
    }

    /// <summary>Starfall Volley's strikes lose damage once they hit more than 12 enemies: each enemy takes the damage times sqrt(12 / enemies).</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(24)]
    [InlineData(48)]
    public void Starfall_Volley_Loses_Damage_Past_12_Enemies(int enemies)
    {
        StepDriver Fight(int n)
        {
            var f = StepFights.Elarion(enemies: n).WithoutDamageSpread();

            f.Cast<StarfallVolley>();
            f.Wait(1.0); // the first strike lands
            return f;
        }

        double alone = Fight(1).FirstHitOnEachEnemy("Starfall Volley").Single();
        var crowd = Fight(enemies).FirstHitOnEachEnemy("Starfall Volley");

        double expected = enemies <= 12 ? 1.0 : Math.Sqrt(12.0 / enemies);

        using var check = new Checks();
        check.Equal(enemies, crowd.Count, what: "enemies hit");
        for (int i = 0; i < crowd.Count; i++)
            check.Ratio(expected, crowd[i] / alone, what: $"enemy {i + 1}");
    }
}
