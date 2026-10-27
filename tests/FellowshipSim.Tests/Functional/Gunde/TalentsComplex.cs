using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde.Abilities;
using GundeTalents = FellowshipSim.Core.Heroes.Gunde.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Gunde's talents together, or with several abilities: what adds to Rend, Deep Rend with Owed in Blood and Slaughter, Crimson Strikes with Serrated Edge,
/// and what Exsanguinate and Rend leave out of their damage.
/// Dev: damage is the expected value, so ratios are exact; random procs are forced. Hits are compared with an auto attack swing (30% of Strength).
/// </summary>
public class GundeTalentsComplex
{
    private const double Ratio = 0.001;

    private static readonly double Strength = GundeFights.StrengthUnit();

    private static double Sum(IEnumerable<StepEvent> events) => events.Sum(e => e.Value);

    private static double RendTotal(StepDriver f) => f.RendLeft() + Sum(f.Damage("Rend"));

    /// <summary>
    /// Player: every source of Rend adds up on one Heart Splitter: its own 100%, Reign in Blood's 30%, Frenzied Reign's 25%, Serrated Edge's 20% and Superior
    /// Serration's 10%: 185% of its damage.
    /// Dev: Frenzied Reign and Superior Serration taken; a Blood Arc and a Reign in Blood, then a Heart Splitter: its Rend is 1.85 times its damage (the figure
    /// seen in a combat log).
    /// </summary>
    [Fact]
    public void Heart_Splitter_Under_Every_Rend_Bonus_Applies_185_Percent()
    {
        var f = StepFights.Gunde(talents: [GundeTalents.FrenziedReign.TalentId, GundeTalents.SuperiorSerration.TalentId]);
        f.Cast<BloodArc>();
        f.Cast<ReignInBlood>();
        double before = RendTotal(f);

        f.Cast<HeartSplitter>();

        Assert.Equal(1.85, (RendTotal(f) - before) / f.FirstHitOf("Heart Splitter"), Ratio);
    }

    /// <summary>
    /// Player: Deep Rend counts in the Rend stacks Owed in Blood adds: 10 stacks are still 10 Strength of Rend to deal, ticking in 11 tenths.
    /// Dev: Deep Rend taken; 10 stacks of Owed in Blood; one cast; the Rend is 10 times Strength, and 11 ticks of it a tenth of 10 Strength / 1.1 over time.
    /// </summary>
    [Fact]
    public void Deep_Rend_Does_Not_Change_The_Rend_Stacks_Owed_In_Blood_Adds()
    {
        var f = StepFights.Gunde(talents: [GundeTalents.DeepRend.TalentId]);
        f.SetOwedInBlood(10);

        f.Cast<OwedInBlood>();

        Assert.Equal(10, f.RendLeft() / Strength, 0.01);
        f.Wait(32);
        Assert.Equal(11, f.Damage("Rend").Count);
        Assert.Equal(10 * Strength, Sum(f.Damage("Rend")), Strength * Ratio);
    }

    /// <summary>
    /// Player: Slaughter bleeds 160% of the Rend still to deal, Deep Rend's extra included.
    /// Dev: Deep Rend taken; a Reaver's Edge then Slaughter; the bleed is 160% of 110% of the application.
    /// </summary>
    [Fact]
    public void Slaughter_Counts_Deep_Rends_Extra_Damage()
    {
        var f = StepFights.Gunde(talents: [GundeTalents.DeepRend.TalentId]);
        f.Cast<ReaversEdge>();
        double application = 0.5 * f.FirstHitOf("Reaver's Edge");

        f.Cast<Slaughter>();
        f.Wait(3.5);

        Assert.Equal(1.6 * 1.1 * application, Sum(f.DamageOfEffect(3237)), application * Ratio);
    }

    /// <summary>
    /// Player: Crimson Strikes and Serrated Edge both empower the next Rend ability: its hit is 15% stronger and 20% more of it goes to Rend: 70% of the stronger hit.
    /// Dev: a Blood Arc, then a Reaver's Edge: its hit is 1.15 times a plain one, and its Rend 0.7 of that hit.
    /// </summary>
    [Fact]
    public void Crimson_Strikes_And_Serrated_Edge_Empower_The_Same_Cast()
    {
        var plain = StepFights.Gunde();
        plain.Cast<ReaversEdge>();
        var f = StepFights.Gunde(talents: [GundeTalents.CrimsonStrikes.TalentId]);
        f.Cast<BloodArc>();
        double before = RendTotal(f);

        f.Cast<ReaversEdge>();

        double hit = f.FirstHitOf("Reaver's Edge");
        Assert.Equal(1.15, hit / plain.FirstHitOf("Reaver's Edge"), Ratio);
        Assert.Equal(0.7, (RendTotal(f) - before) / hit, Ratio);
    }

    /// <summary>
    /// Player: Rend and Exsanguinate take no damage multipliers of their own: whatever made the hit stronger (Harvester's Toll's 7.5%, Darkening Hearts' 10%),
    /// Exsanguinate is 30% of the Rend on the target, no more.
    /// Dev: Harvester's Toll (a Rupture, 50% Rend) and Darkening Hearts taken; then a Heart Splitter (100% Rend); its Exsanguinate is 30% of the two applications.
    /// </summary>
    [Fact]
    public void Exsanguinate_Takes_No_Damage_Multipliers()
    {
        var f = StepFights.Gunde(talents: [GundeTalents.HarvestersToll.TalentId, GundeTalents.DarkeningHearts.TalentId]);
        f.Cast<Rupture>();

        f.Cast<HeartSplitter>();

        double rend = 0.5 * f.FirstHitOf("Rupture") + f.FirstHitOf("Heart Splitter");
        Assert.Equal(0.3 * rend, f.FirstHitOf("Exsanguinate"), rend * Ratio);
    }

    /// <summary>
    /// Player: Death's Arc's +100% Critical Strike chance is for the next Blood Arc only.
    /// Dev: Death's Arc forced on for one Blood Arc (which resets itself), then off; the second Blood Arc has 100% more crit chance, the third (after its cooldown) none.
    /// </summary>
    [Fact]
    public void Deaths_Arcs_Crit_Is_Used_Up_By_One_Blood_Arc()
    {
        var f = StepFights.Gunde(talents: [GundeTalents.DeathsArc.TalentId]);
        f.ForceProcs(key => key == GundeTalents.DeathsArc.ProcKey ? true : null);
        f.Cast<BloodArc>();
        f.ForceProcs(key => key == GundeTalents.DeathsArc.ProcKey ? false : null);

        f.Cast<BloodArc>();
        f.Wait(9);
        f.Cast<BloodArc>();

        var hits = f.Damage("Blood Arc");
        Assert.Equal(1.0, hits[1].CritChance - hits[0].CritChance, 1e-9);
        Assert.Equal(0.0, hits[2].CritChance - hits[0].CritChance, 1e-9);
    }
}
