using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's legendary items, each worn on a bare Gunde against one enemy: Bloody Mess (his helm), Bleeding Hearts (his harness) and Carrion
/// Onslaught (his wraps). Any legendary also gives Mote of Creation's +10% main stat, so the damage tests wear the legendary in both
/// fights.
/// </summary>
public class GundeLegendaries
{
    // ---- Bloody Mess

    /// <summary>With Bloody Mess, Grim Carve spins 2 additional times: 5 spins instead of 3.</summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 5)]
    public void Bloody_Mess_Makes_Grim_Carve_Spin_2_More_Times(bool legendary, int spins)
    {
        var f = StepFights.Gunde(legendaries: legendary ? [BloodyMess.HelmItemId] : null);

        f.Cast<GrimCarve>();
        f.Wait(3.0); // every spin

        Assert.Equal(spins, f.Damage<GrimCarve>().Count);
    }

    // ---- Bleeding Hearts

    /// <summary>With Bleeding Hearts, Heart Splitter has 2 charges: two casts in a row, where it has 1 without.</summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void Bleeding_Hearts_Gives_Heart_Splitter_2_Charges(bool legendary, int charges)
    {
        var f = StepFights.Gunde(legendaries: legendary ? [BleedingHearts.HarnessItemId] : null);
        Assert.Equal(charges, f.Charges<HeartSplitter>());

        for (int cast = 0; cast < charges; cast++) f.Cast<HeartSplitter>();

        using var check = new Checks(f);
        check.Equal(0, f.Charges<HeartSplitter>());
        check.False(f.IsReady<HeartSplitter>());
    }

    /// <summary>With Bleeding Hearts, each Heart Splitter has a 25% chance to strike the target 1 additional time.</summary>
    [Fact]
    public void Bleeding_Hearts_Has_A_25_Percent_Chance_Of_An_Extra_Strike()
    {
        var f = StepFights.Gunde(legendaries: [BleedingHearts.HarnessItemId]);
        var procs = f.ForceProcs(key => key == BleedingHearts.ProcKey ? false : null); // only to read the chance

        f.Cast<HeartSplitter>();

        Assert.Equal([0.25], procs.ChancesOf(BleedingHearts.ProcKey));
    }

    /// <summary>
    /// When Bleeding Hearts procs, Heart Splitter strikes the target twice, each strike with its own Exsanguinate, for one cast and one
    /// charge; when it does not, once.
    ///
    /// Source: the tooltip and combat logs (NOTES.md: the extra strike has its own Exsanguinate and adds no cast event).
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void Bleeding_Hearts_Extra_Strike_Hits_Again_With_Its_Own_Exsanguinate(bool proc, int strikes)
    {
        var f = StepFights.Gunde(legendaries: [BleedingHearts.HarnessItemId]);
        f.ForceProcs(key => key == BleedingHearts.ProcKey ? proc : null);

        f.Cast<HeartSplitter>();

        using var check = new Checks(f);
        check.Equal(strikes, f.Damage<HeartSplitter>().Count);
        check.Equal(strikes, f.Damage("Exsanguinate").Count);
        check.Equal(1, f.CastsCompleted("Heart Splitter").Count);
        check.Equal(1, f.Charges<HeartSplitter>());
    }

    /// <summary>
    /// With Bleeding Hearts, Heart Splitter has +100% crit chance against a target above 80% health, and none at 80% or below.
    /// </summary>
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.81, 1.0)]
    [InlineData(0.8, 0.0)]
    [InlineData(0.5, 0.0)]
    public void Bleeding_Hearts_Gives_Heart_Splitter_100_Percent_Crit_Above_80_Percent_Health(double health, double critChance)
    {
        var f = StepFights.Gunde(legendaries: [BleedingHearts.HarnessItemId]);
        f.ForceProcs(key => key == BleedingHearts.ProcKey ? false : null); // a single strike
        f.SetHealth(health);

        f.Cast<HeartSplitter>();

        Assert.Equal(critChance, f.Damage<HeartSplitter>().Single().CritChance, 1e-9);
    }

    // ---- Carrion Onslaught

    /// <summary>With Carrion Onslaught, Owed in Blood gives you Carrion Onslaught for 12 seconds.</summary>
    [Fact]
    public void Owed_In_Blood_Gives_Carrion_Onslaught_For_12_Seconds()
    {
        var f = StepFights.Gunde(legendaries: [CarrionOnslaught.WrapsItemId]);
        f.Apply(OwedInBlood.Buff, stacks: 10); // feathers picked up earlier

        f.Cast<OwedInBlood>();
        double end = f.CompletedAt("Owed in Blood") + 12.0;

        f.WaitUntil(end - 0.1);
        Assert.True(f.Has(CarrionOnslaught.Buff));
        f.WaitUntil(end + 0.1);
        Assert.False(f.Has(CarrionOnslaught.Buff));
    }

    /// <summary>
    /// Carrion Onslaught makes your next Blood Arc deal 1.2 + 0.02 x the Owed in Blood stacks consumed times its damage.
    ///
    /// Source: game data effect 3256 and combat logs (NOTES.md, cnjfpzmD7ZXakMFH fight 33).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(84)]
    public void Carrion_Onslaught_Raises_The_Next_Blood_Arc_By_1_2_Plus_0_02_Per_Stack_Consumed(int stacks)
    {
        double BloodArcHit(int stacks)
        {
            var f = StepFights.Gunde(legendaries: [CarrionOnslaught.WrapsItemId]).WithoutDamageSpread();
            if (stacks > 0) f.Apply(OwedInBlood.Buff, stacks: stacks); // feathers picked up earlier

            if (stacks > 0) f.Cast<OwedInBlood>();
            f.Cast<BloodArc>();

            return f.Damage<BloodArc>().Single().Value;
        }

        AssertRatio(1.2 + 0.02 * stacks, BloodArcHit(stacks) / BloodArcHit(0));
    }

    /// <summary>
    /// Only the next Blood Arc gets Carrion Onslaught: it uses the buff up, and the Blood Arc after it deals its usual damage.
    /// </summary>
    [Fact]
    public void Carrion_Onslaught_Is_Used_Up_By_The_Next_Blood_Arc()
    {
        var f = StepFights.Gunde(legendaries: [CarrionOnslaught.WrapsItemId]).WithoutDamageSpread();
        f.Apply(OwedInBlood.Buff, stacks: 10); // feathers picked up earlier

        f.Cast<OwedInBlood>();
        f.Cast<BloodArc>();
        bool buffAfterFirstArc = f.Has(CarrionOnslaught.Buff);
        f.CastWhenReady("Blood Arc");

        var arcs = f.Damage<BloodArc>();

        using var check = new Checks(f);
        check.False(buffAfterFirstArc);
        check.Ratio(1.0 / (1.2 + 0.02 * 10), arcs[1].Value / arcs[0].Value);
    }

    /// <summary>
    /// The Blood Arc that uses Carrion Onslaught drops one Blood Feather per third Owed in Blood stack consumed, rounded: once all have
    /// dropped and been picked up, you hold that many Owed in Blood stacks again.
    ///
    /// Source: game data effect 3256 and combat logs (NOTES.md: 84 -> 28, 51 -> 17, 80 -> 27). Rend's own feather rolls are forced off.
    /// </summary>
    [Theory]
    [InlineData(84, 28)]
    [InlineData(80, 27)]
    [InlineData(51, 17)]
    public void Carrion_Onslaught_Blood_Arc_Drops_A_Feather_Per_Third_Stack_Consumed(int stacks, int feathers)
    {
        var f = StepFights.Gunde(legendaries: [CarrionOnslaught.WrapsItemId]);
        f.ForceProcs(key => key == FellowshipSim.Core.Heroes.Gunde.Gunde.FeatherProcKey ? false : null); // no feather from Rend ticks
        f.Apply(OwedInBlood.Buff, stacks: stacks); // feathers picked up earlier

        f.Cast<OwedInBlood>();
        f.Cast<BloodArc>();
        f.Wait(12.0); // every feather drops and is picked up

        using var check = new Checks(f);
        check.False(f.Has(CarrionOnslaught.Feathers));
        check.Equal(feathers, f.Stacks(OwedInBlood.Buff));
    }

    /// <summary>
    /// The feathers drop 10 at once as Blood Arc lands, then 10 more every 2 seconds until none are left: of 28, 18 are left until 2
    /// seconds after the Blood Arc, 8 until 4 seconds after, then none.
    ///
    /// Source: combat logs (NOTES.md, cnjfpzmD7ZXakMFH fight 33); the game data's text says 3 per period, out of date per the logs.
    /// </summary>
    [Fact]
    public void Carrion_Onslaught_Drops_10_Feathers_At_Once_Then_10_Every_2_Seconds()
    {
        var f = StepFights.Gunde(legendaries: [CarrionOnslaught.WrapsItemId]);
        f.ForceProcs(key => key == FellowshipSim.Core.Heroes.Gunde.Gunde.FeatherProcKey ? false : null); // no feather from Rend ticks
        f.Apply(OwedInBlood.Buff, stacks: 84); // 28 feathers

        f.Cast<OwedInBlood>();
        f.Cast<BloodArc>();
        double arc = f.CompletedAt("Blood Arc");

        using var check = new Checks(f);
        check.Equal(18, f.Stacks(CarrionOnslaught.Feathers));
        f.WaitUntil(arc + 2.0 - 0.1);
        check.Equal(18, f.Stacks(CarrionOnslaught.Feathers));
        f.WaitUntil(arc + 2.0 + 0.1);
        check.Equal(8, f.Stacks(CarrionOnslaught.Feathers));
        f.WaitUntil(arc + 4.0 - 0.1);
        check.Equal(8, f.Stacks(CarrionOnslaught.Feathers));
        f.WaitUntil(arc + 4.0 + 0.1);
        check.False(f.Has(CarrionOnslaught.Feathers));
    }
}
