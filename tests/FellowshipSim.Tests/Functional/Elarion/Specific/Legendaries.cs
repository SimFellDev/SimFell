using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's legendary items: Starstriker's Ascent (his chest), Shimmer (his helm) and Astronomer's Hail (his leggings). The Python
/// sim calls them the neck, cloak and boots legendaries.
/// </summary>
public class ElarionLegendaries
{
    private const double Ratio = 0.001;

    private static readonly int[] Starstrikers = [StarstrikersAscent.HauberkItemId];

    // ---- Starstriker's Ascent

    /// <summary>
    /// Player: with Starstriker's Ascent, a Spirit Refund has a 50% chance to reset Heartseeker Barrage's cooldown and give you Impending
    /// Heartseeker.
    /// Dev: 25% Spirit, the refund forced; a barrage cast then a Celestial Shot; with the legendary's roll forced on the buff is up and
    /// the barrage ready; forced off, neither; the chance given to the roll is 50%.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_Spirit_Refund_Can_Reset_The_Barrage_And_Give_Impending_Heartseeker(bool proc)
    {
        var f = StepFights.Elarion(spirit: 0.25, legendaries: Starstrikers);
        var procs = f.ForceProcs(key => key == Hero.SpiritRefundProc ? true : key == StarstrikersAscent.ProcKey ? proc : false);
        f.Cast<HeartseekerBarrage>();
        f.Wait(0.5);
        f.Hero.Auras.Remove(ElarionTalents.ImpendingHeartseeker.Buff, f.Now);
        f.Hero.CooldownOf(f.Ability<HeartseekerBarrage>()).ResetAll();
        f.Cast<HeartseekerBarrage>();
        f.Hero.Auras.Remove(ElarionTalents.ImpendingHeartseeker.Buff, f.Now);
        f.Wait(3);
        f.Cast<CelestialShot>();
        f.Wait(0.5);

        Assert.Equal(0.5, procs.Rolls.First(r => r.Key == StarstrikersAscent.ProcKey).Chance, 1e-9);
        Assert.Equal(proc, f.Hero.Auras.HasAura(ElarionTalents.ImpendingHeartseeker.Buff, f.Now));
    }

    /// <summary>
    /// Player: with Starstriker's Ascent, a barrage's own Spirit Refund gives the buff to the next barrage, not the one cast; chained
    /// refunds keep it going: the first barrage is plain, the next two ramp their arrows by 10% each.
    /// Dev: 25% Spirit, the refund and the legendary's roll forced on; three barrages (the buff resets the cooldown); the first has equal
    /// arrows, the other two have arrow k (from 0) at 1 + 0.1 k times their own first arrow (the damage of different barrages is not compared: Rising
    /// Spirit raises Agility after each refund).
    /// TODO: the engine's first ramped arrow already has +10% (see Impending Heartseeker in TalentsSimple.cs).
    /// </summary>
    [Fact]
    public void Chained_Refunds_Keep_Impending_Heartseeker_Going_On_Barrages()
    {
        var f = StepFights.Elarion(spirit: 0.25, legendaries: Starstrikers);
        f.ForceProcs(key => key == Hero.SpiritRefundProc || key == StarstrikersAscent.ProcKey ? true : false);

        for (int i = 0; i < 3; i++) f.Cast<HeartseekerBarrage>();
        f.Wait(0.5);

        var arrows = f.Damage("Heartseeker Barrage");
        Assert.Equal(30, arrows.Count);
        Assert.All(arrows.Take(10), a => Assert.Equal(arrows[0].Value, a.Value, 1e-6));
        for (int barrage = 1; barrage < 3; barrage++)
        for (int k = 0; k < 10; k++)
            Assert.Equal(1 + 0.1 * k, arrows[barrage * 10 + k].Value / arrows[barrage * 10].Value, Ratio);
    }

    // ---- Shimmer

    /// <summary>
    /// Player: with Shimmer, enemies your Highwind Arrow hits are afflicted by Shimmer.
    /// Dev: the legendary worn; one Highwind Arrow; the target has Shimmer.
    /// </summary>
    [Fact]
    public void Highwind_Arrow_Applies_Shimmer()
    {
        var f = StepFights.Elarion(legendaries: [Shimmer.HelmItemId]);

        f.Cast<HighwindArrow>();
        f.Wait(0.5);

        Assert.True(f.Enemy.Auras.HasAura(Shimmer.Debuff, f.Now));
    }

    /// <summary>
    /// Player: a new Shimmer on an enemy that has it adds a stack and restarts the 9 seconds.
    /// Dev: the legendary worn; a Highwind Arrow, 5 seconds, another (the cooldown reset): 2 stacks, and 9 seconds left on it.
    /// </summary>
    [Fact]
    public void A_Second_Shimmer_Adds_A_Stack_And_Renews_The_Duration()
    {
        var f = StepFights.Elarion(legendaries: [Shimmer.HelmItemId]);
        f.Cast<HighwindArrow>();
        f.Wait(5);
        f.Hero.CooldownOf(f.Ability<HighwindArrow>()).ResetAll();
        f.Cast<HighwindArrow>();
        f.Wait(0.5);

        var shimmer = f.Enemy.Auras.Get(Shimmer.Debuff, f.Now)!;
        Assert.Equal(2, shimmer.Stacks);
        Assert.InRange((shimmer.ExpiresAt - f.Now).TotalSeconds, 8.4, 9.1);
    }

    /// <summary>
    /// Player: each Shimmer stack makes you deal 8% more damage to the enemy, up to 3 stacks.
    /// Dev: a Highwind Arrow's first hit on an enemy with 1 stack against one with none: 1.08; with 2: 1.16; a third application makes
    /// 3 stacks. (The game data's text; the Python sim's earlier game version had 10% up to 2.) Any legendary also gives Mote of Creation's
    /// +10% Main Stat, so the reference hero without one has 1100 Main Stat to compare like with like.
    /// </summary>
    [Fact]
    public void Each_Shimmer_Stack_Raises_Damage_By_8_Percent_Up_To_3_Stacks()
    {
        var plain = StepFights.Elarion(mainStat: 1100);
        plain.Cast<HighwindArrow>();
        double reference = plain.Damage("Highwind Arrow")[0].Value;

        var f = StepFights.Elarion(legendaries: [Shimmer.HelmItemId]);
        for (int i = 0; i < 4; i++)
        {
            f.Cast<HighwindArrow>();
            f.Wait(0.5);
            f.Hero.CooldownOf(f.Ability<HighwindArrow>()).ResetAll();
        }

        var hits = f.Damage("Highwind Arrow");
        Assert.Equal(1.08, hits[1].Value / reference, Ratio);
        Assert.Equal(1.16, hits[2].Value / reference, Ratio);
        Assert.Equal(3, f.Enemy.Auras.GetStacks(Shimmer.Debuff, f.Now));
    }

    /// <summary>
    /// Player: with Final Crescendo's empowered arrow, Shimmer lands on 9 enemies of a group of 12 (the target and 8 ricochets).
    /// Dev: 12 enemies; Final Crescendo and Shimmer; its two stacks given and one arrow; 9 enemies have Shimmer.
    /// </summary>
    [Fact]
    public void The_Empowered_Highwind_Arrow_Applies_Shimmer_To_9_Of_12_Enemies()
    {
        var f = StepFights.Elarion(enemies: 12, talents: [ElarionTalents.FinalCrescendo.TalentId], legendaries: [Shimmer.HelmItemId]);
        f.Hero.Auras.Apply(ElarionTalents.FinalCrescendo.Buff, f.Now, setStacks: 2);
        f.Cast<HighwindArrow>();
        f.Wait(0.5);

        Assert.Equal(9, f.Context.AllEnemies.Count(e => e.Auras.HasAura(Shimmer.Debuff, f.Now)));
    }

    // ---- Astronomer's Hail

    /// <summary>
    /// Player: Astronomer's Hail makes Starfall Volley last 3 seconds longer (11 seconds).
    /// Dev: the legendary worn; a Starfall Volley is still falling at 10.5 seconds and over at 11.5. (The legendary's tooltip; the
    /// Python sim's earlier game version had +2 seconds.)
    /// </summary>
    [Fact]
    public void The_Legendary_Makes_Starfall_Volley_Last_11_Seconds()
    {
        var f = StepFights.Elarion(legendaries: [AstronomersHail.LeggingsItemId]);
        f.Cast<StarfallVolley>();

        f.Wait(10.5 - f.Now.TotalSeconds);
        Assert.True(f.Ability<StarfallVolley>().IsActive);
        f.Wait(1.0);
        Assert.False(f.Ability<StarfallVolley>().IsActive);
    }

    /// <summary>
    /// Player: each Multishot you cast while Starfall Volley falls extends it by 0.25 seconds.
    /// Dev: the legendary worn; a Starfall Volley, then 3 Multishots (Focus topped up each time; empowered by Fervent Supremacy), all while it falls; the volley ends at its
    /// cast + 8 + 3 + 3 x 0.25 seconds.
    /// </summary>
    [Fact]
    public void Each_Multishot_Extends_Starfall_Volley_By_A_Quarter_Of_A_Second()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.FerventSupremacy.TalentId], legendaries: [AstronomersHail.LeggingsItemId]);
        f.Cast<SkystridersSupremacy>();
        f.Cast<StarfallVolley>();
        double start = f.Now.TotalSeconds - 1.5;
        for (int i = 0; i < 3; i++)
        {
            f.Hero.Resources.Get(Elarion.Focus).Current = 100;
            f.Cast<Multishot>();
        }
        double end = start + 8 + 3 + 3 * 0.25;

        f.Wait(end - 0.3 - f.Now.TotalSeconds);
        Assert.True(f.Ability<StarfallVolley>().IsActive);
        f.Wait(0.6);
        Assert.False(f.Ability<StarfallVolley>().IsActive);
    }
}
