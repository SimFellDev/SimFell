using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's abilities working together: Starfall Volleys overlapping, Celestial Impetus, and Spirit Refund. His talents are in
/// TalentsSimple.cs and TalentsComplex.cs, the Lunarlight Mark system in Specific/LunarlightMark.cs.
/// </summary>
public class ElarionAbilitiesComplex
{
    // ---- Starfall Volley

    /// <summary>
    /// Player: you can cast Starfall Volley again while the first is still falling: both fall at once, each strikes as often as a volley
    /// alone, and each ends 8 seconds after its own cast.
    /// Dev: one enemy; a Starfall Volley, its cooldown reset, a second one 1.5s later; the hits are twice a lone volley's, there are
    /// hits after the first volley's end (the second still falling) and none after the second's.
    /// </summary>
    [Fact]
    public void Two_Starfall_Volleys_Fall_At_Once_And_End_Separately()
    {
        var alone = StepFights.Elarion();
        alone.Cast<StarfallVolley>();
        alone.Wait(12);
        int single = alone.Damage("Starfall Volley").Count;

        var f = StepFights.Elarion();
        f.Cast<StarfallVolley>();
        double first = f.Events.First(e => e.Kind == StepEventKind.CastStarted && e.Name == "Starfall Volley").Time.TotalSeconds;
        f.Hero.CooldownOf(f.Ability<StarfallVolley>()).ResetAll();
        f.Cast<StarfallVolley>();
        double second = f.Events.Last(e => e.Kind == StepEventKind.CastStarted && e.Name == "Starfall Volley").Time.TotalSeconds;
        f.Wait(12);

        var hits = f.Damage("Starfall Volley");
        Assert.Equal(2 * single, hits.Count);
        Assert.Contains(hits, h => h.Time.TotalSeconds > first + 8.4 && h.Time.TotalSeconds <= second + 8.0);
        Assert.DoesNotContain(hits, h => h.Time.TotalSeconds > second + 8.4);
    }

    // ---- Celestial Impetus

    /// <summary>
    /// Player: Celestial Impetus comes from Focused Shot's hits at 2 procs per minute (more with haste), and gets likelier the longer it
    /// has been since the last try: the first shot cannot proc, and 30 seconds later (20 at 50% haste) it is certain.
    /// Dev: two Focused Shots, the second landing 30 seconds (20 with 50% haste) after the first; the chance the engine gives the proc for
    /// the second is 1; with a half-length wait (15 and 10s) it is 0.5.
    /// TODO: the Python sim gives the first shot no chance at all (its Real PPM starts with no earlier try); the engine counts the time from
    /// the start of the fight (5% at 1.5 seconds). Not tested.
    /// </summary>
    [Theory]
    [InlineData(0.0, 28.5, 1.0)]
    [InlineData(0.0, 13.5, 0.5)]
    [InlineData(0.5, 19.0, 1.0)]
    [InlineData(0.5, 9.0, 0.5)]
    public void Celestial_Impetus_Chance_Grows_With_The_Time_Since_The_Last_Try(double haste, double waitSeconds, double chance)
    {
        var f = StepFights.Elarion(haste);
        var procs = f.ForceProcs(_ => false);

        f.Cast<FocusedShot>();
        f.Wait(waitSeconds);
        f.Cast<FocusedShot>();

        Assert.Equal(chance, procs.Rolls.Last(r => r.Key == CelestialShot.ImpetusProcKey).Chance, 0.02);
    }

    // ---- Spirit Refund

    /// <summary>
    /// Player: your Spirit gives each cast a chance, Spirit / (1 + Spirit), to be refunded: none with no Spirit, 1 in 3 with 50% Spirit,
    /// 1 in 2 with 100%.
    /// Dev: one Celestial Shot with the refund forced off; the chance the engine gives it.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 1.0 / 3)]
    [InlineData(1.0, 0.5)]
    public void Spirit_Refund_Chance_Is_Spirit_Over_1_Plus_Spirit(double spirit, double chance)
    {
        var f = StepFights.Elarion(spirit: spirit);
        var procs = f.ForceProcs(_ => false);

        f.Cast<CelestialShot>();

        var rolls = procs.Rolls.Where(r => r.Key == Hero.SpiritRefundProc).ToList();
        Assert.All(rolls, r => Assert.Equal(chance, r.Chance, 1e-9));
        if (spirit > 0) Assert.NotEmpty(rolls);
    }

    /// <summary>
    /// Player: a Spirit Refund on a Celestial Shot gives back its 15 Focus, gives you 1 Spirit Point, puts 5 stacks of Lunarlight Mark
    /// on the target and 2 on up to two nearby enemies.
    /// Dev: 20% Spirit, the refund forced; Focus set to 50 (so the refund is not lost to the cap); 4 enemies; a Celestial Shot, then 0.5 seconds (the refund comes 0.2s after): the Focus is back to what it was
    /// plus the regeneration, 1 Spirit Point, 5 stacks on the target and 2 on two others (the third has none). The salvo is forced off.
    /// </summary>
    [Fact]
    public void A_Spirit_Refund_Gives_Back_Focus_Gives_A_Spirit_Point_And_Marks_Enemies()
    {
        var f = StepFights.Elarion(spirit: 0.2, enemies: 4);
        f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : key == LunarlightSalvo.ProcKey ? false : null);
        f.Hero.Resources.Get(Elarion.Focus).Current = 50;

        f.Cast<CelestialShot>();
        f.Wait(0.5);

        Assert.Equal(50 + 5 * 2.0, f.ElarionOf().FocusAt(f.Now), 0.01); // the 15 spent came back; only the regeneration of the 2s is gained
        Assert.Equal(1, f.Hero.ResourceAmount(Hero.SpiritPoints), 1e-9);
        var stacks = f.Context.AllEnemies.Select(e => e.Auras.GetStacks(LunarlightMark.Debuff, f.Now)).ToList();
        Assert.Equal(5, stacks[0]);
        Assert.Equal(2, stacks.Skip(1).Count(s => s == 2));
        Assert.Equal(1, stacks.Skip(1).Count(s => s == 0));
    }

    // ---- Multishot charges

    /// <summary>
    /// Player: a Highwind Arrow that ricochets to at least 3 enemies gives you a charge of Multishot, up to 5.
    /// Dev: 3 enemies; ten Highwind Arrows; Multishot's charges go 0, 1, 2, ... 5.
    /// TODO: the Python sim models Multishot charges. The engine has none (combat logs show no Multishot charge: Multishot is cast with
    /// no limit but its Focus). Check in game.
    /// </summary>
    [Fact(Skip = "The Python sim has Multishot charges from Highwind Arrow; the engine has none (no charge in the combat logs): to check in game")]
    public void Highwind_Arrow_On_3_Enemies_Gives_A_Multishot_Charge()
    {
        Assert.Fail("The engine has no Multishot charges to read.");
    }

    /// <summary>
    /// Player: a Highwind Arrow that ricochets to only one other enemy gives no Multishot charge.
    /// Dev: as above, with 2 enemies.
    /// TODO: as above.
    /// </summary>
    [Fact(Skip = "The Python sim has Multishot charges from Highwind Arrow; the engine has none (no charge in the combat logs): to check in game")]
    public void Highwind_Arrow_On_2_Enemies_Gives_No_Multishot_Charge()
    {
        Assert.Fail("The engine has no Multishot charges to read.");
    }
}
