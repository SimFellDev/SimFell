using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// One Elarion talent on its own. Talents that need several abilities or several effects are in TalentsComplex.cs; the talents that
/// take time off cooldowns are in CooldownDurationSimple.cs and the haste breakpoints Fusillade makes are in HasteBreakpoints.cs.
/// </summary>
public class ElarionTalentsSimple
{
    private const double Ratio = 0.001;

    // ---- Impending Heartseeker

    /// <summary>
    /// Player: with Impending Heartseeker up, each arrow of the next Heartseeker Barrage deals 10% more than the one before: the
    /// first arrow is normal, the second 110%, ... the tenth 190%, 14.5 arrows' worth in all.
    /// Dev: the buff applied directly; one barrage at no haste (10 arrows); arrow k (from 0) is 1 + 0.1 k times a plain arrow, from a
    /// barrage cast without the buff.
    /// TODO: the engine's first arrow already has +10% (marked UNVERIFIED in its code): 15.5 arrows' worth. Check in game.
    /// </summary>
    [Fact]
    public void Impending_Heartseeker_Raises_Each_Arrow_By_10_Percent_Over_The_One_Before()
    {
        var plain = StepFights.Elarion();
        plain.Cast<HeartseekerBarrage>();
        double reference = plain.Damage("Heartseeker Barrage")[0].Value;

        var f = StepFights.Elarion();
        f.Hero.Auras.Apply(ElarionTalents.ImpendingHeartseeker.Buff, f.Now);
        f.Cast<HeartseekerBarrage>();

        var arrows = f.Damage("Heartseeker Barrage");
        Assert.Equal(10, arrows.Count);
        for (int k = 0; k < arrows.Count; k++) Assert.Equal(1 + 0.1 * k, arrows[k].Value / reference, Ratio);
        Assert.Equal(14.5, arrows.Sum(a => a.Value) / reference, Ratio);
    }

    /// <summary>
    /// Player: the buff is used up by the barrage that benefits: the next one is back to normal damage, and the buff is gone.
    /// Dev: the buff applied; two barrages (the cooldown reset between them); the first has it, the second's arrows are all equal.
    /// </summary>
    [Fact]
    public void Impending_Heartseeker_Is_Used_Up_By_One_Barrage()
    {
        var f = StepFights.Elarion();
        f.Hero.Auras.Apply(ElarionTalents.ImpendingHeartseeker.Buff, f.Now);

        f.Cast<HeartseekerBarrage>();
        Assert.False(f.Hero.Auras.HasAura(ElarionTalents.ImpendingHeartseeker.Buff, f.Now));
        f.Hero.CooldownOf(f.Ability<HeartseekerBarrage>()).ResetAll();
        f.Cast<HeartseekerBarrage>();

        var second = f.Damage("Heartseeker Barrage").Skip(10).ToList();
        Assert.Equal(10, second.Count);
        Assert.All(second, a => Assert.Equal(second[0].Value, a.Value, 1e-6));
    }

    /// <summary>
    /// Player: Impending Heartseeker fades after 15 seconds if you do not use it.
    /// Dev: the buff applied; 16 seconds pass; it is gone.
    /// </summary>
    [Fact]
    public void Impending_Heartseeker_Expires_After_15_Seconds()
    {
        var f = StepFights.Elarion();
        f.Hero.Auras.Apply(ElarionTalents.ImpendingHeartseeker.Buff, f.Now);

        f.Wait(14);
        Assert.True(f.Hero.Auras.HasAura(ElarionTalents.ImpendingHeartseeker.Buff, f.Now));
        f.Wait(2);
        Assert.False(f.Hero.Auras.HasAura(ElarionTalents.ImpendingHeartseeker.Buff, f.Now));
    }

    /// <summary>
    /// Player: a Celestial Shot that uses Celestial Impetus resets Heartseeker Barrage's cooldown and gives you Impending Heartseeker.
    /// Dev: Impending Heartseeker taken; a barrage cast (on cooldown); Celestial Impetus applied; a Celestial Shot; the barrage is ready
    /// and the buff is up. The same Celestial Shot without the talent does neither.
    /// </summary>
    [Fact]
    public void A_Celestial_Shot_Using_Impetus_Resets_The_Barrage_And_Gives_Impending_Heartseeker()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.ImpendingHeartseeker.TalentId]);
        f.Cast<HeartseekerBarrage>();
        Assert.False(f.Hero.IsCooldownReady<HeartseekerBarrage>(f.Now));
        f.Hero.Auras.Apply(CelestialShot.Impetus, f.Now);

        f.Cast<CelestialShot>();

        Assert.True(f.Hero.IsCooldownReady<HeartseekerBarrage>(f.Now));
        Assert.True(f.Hero.Auras.HasAura(ElarionTalents.ImpendingHeartseeker.Buff, f.Now));

        var without = StepFights.Elarion();
        without.Cast<HeartseekerBarrage>();
        without.Hero.Auras.Apply(CelestialShot.Impetus, without.Now);
        without.Cast<CelestialShot>();
        Assert.False(without.Hero.IsCooldownReady<HeartseekerBarrage>(without.Now));
        Assert.False(without.Hero.Auras.HasAura(ElarionTalents.ImpendingHeartseeker.Buff, without.Now));
    }

    // ---- Lethal Shots

    /// <summary>
    /// Player: Lethal Shots gives Highwind Arrow a 40% chance of +100% critical strike chance: when it procs, the hit is a crit
    /// (grievous), twice the damage with no crit chance of its own; when it does not, the damage is normal.
    /// Dev: Lethal Shots taken, no crit chance; one Highwind Arrow with the proc forced on, one with it forced off; the ratio is 2; the
    /// chance given to the proc is 40%.
    /// </summary>
    [Fact]
    public void Lethal_Shots_Makes_Highwind_Arrow_Crit_Twice_As_Hard_When_It_Procs()
    {
        var off = StepFights.Elarion(talents: [ElarionTalents.LethalShots.TalentId]);
        off.ForceProcs(_ => false);
        off.Cast<HighwindArrow>();

        var on = StepFights.Elarion(talents: [ElarionTalents.LethalShots.TalentId]);
        var procs = on.ForceProcs(key => key == ElarionTalents.LethalShots.ProcKey ? true : null);
        on.Cast<HighwindArrow>();

        Assert.Equal(0.4, procs.Rolls.Single(r => r.Key == ElarionTalents.LethalShots.ProcKey).Chance, 1e-9);
        Assert.Equal(2.0, on.Damage("Highwind Arrow")[0].Value / off.Damage("Highwind Arrow")[0].Value, Ratio);
    }

    // ---- Crit chances

    /// <summary>
    /// Player: Fusillade adds 20% critical strike chance to Heartseeker Barrage's arrows, and to nothing else.
    /// Dev: 15% crit; Fusillade taken; a Focused Shot and a barrage; the barrage arrows' crit chance is the shot's plus 0.2.
    /// </summary>
    [Fact]
    public void Fusillade_Adds_20_Percent_Crit_Chance_To_Heartseeker_Barrage_Only()
    {
        var f = StepFights.Elarion(crit: 0.15, talents: [ElarionTalents.Fusillade.TalentId]);
        f.ForceProcs(_ => false);

        f.Cast<FocusedShot>();
        f.Cast<HeartseekerBarrage>();

        double shot = f.Damage("Focused Shot")[0].CritChance;
        Assert.Equal(shot + 0.2, f.Damage("Heartseeker Barrage")[0].CritChance, 1e-9);
    }

    /// <summary>
    /// Player: Last Lights adds 30% critical strike chance against enemies below 30% health.
    /// Dev: 15% crit; Last Lights taken; the enemy at 20% health; a Focused Shot; its crit chance is the shot's on a healthy enemy plus 0.3.
    /// TODO: the Python sim's value. The game's talent (and the engine's) has +20%. Check in game.
    /// </summary>
    [Fact(Skip = "The Python sim has +30% crit chance, the game's talent +20%: to check in game")]
    public void Last_Lights_Adds_30_Percent_Crit_Chance_Below_30_Percent_Health()
    {
        var healthy = StepFights.Elarion(crit: 0.15, talents: [ElarionTalents.LastLights.TalentId]);
        healthy.Cast<FocusedShot>();
        var weak = StepFights.Elarion(crit: 0.15, talents: [ElarionTalents.LastLights.TalentId]);
        weak.Enemy.CurrentHealth = weak.Enemy.MaxHealth * 0.2;
        weak.Cast<FocusedShot>();

        Assert.Equal(healthy.Damage("Focused Shot")[0].CritChance + 0.3, weak.Damage("Focused Shot")[0].CritChance, 1e-9);
    }

    /// <summary>
    /// Player: Lunarlight Affinity adds 40% critical strike chance to Lunarlight Salvo.
    /// Dev: 15% crit; Lunarlight Affinity taken; a marked enemy and a Focused Shot with the salvo forced; the salvo's crit chance is the
    /// shot's plus 0.4.
    /// TODO: the Python sim's value. The game's talent (and the engine's) has +20%. Check in game.
    /// </summary>
    [Fact(Skip = "The Python sim has +40% crit chance on salvos, the game's talent +20%: to check in game")]
    public void Lunarlight_Affinity_Adds_40_Percent_Crit_Chance_To_Lunarlight_Salvo()
    {
        var f = StepFights.Elarion(crit: 0.15, talents: [ElarionTalents.LunarlightAffinity.TalentId]);
        f.ForceProcs(key => key == LunarlightSalvo.ProcKey ? true : false);
        f.Enemy.Auras.Apply(LunarlightMark.Debuff, f.Now, stacks: 3);

        f.Cast<FocusedShot>();

        Assert.Equal(f.Damage("Focused Shot")[0].CritChance + 0.4, f.Damage("Lunarlight Salvo")[0].CritChance, 1e-9);
    }
}
