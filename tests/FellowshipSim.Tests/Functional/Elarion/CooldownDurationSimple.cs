using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Elarion.Abilities;
using ElarionTalents = FellowshipSim.Core.Heroes.Elarion.Talents;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Elarion's cooldowns shortened by one effect at a time: the talents that take time off a cooldown. Event Horizon's effects on
/// cooldowns, and how they stack with other acceleration, are in Specific/EventHorizonCooldown.cs.
/// Dev: with no haste a cooldown recovers at the normal rate, so the time an ability is ready again is its cooldown remaining
/// now plus the time now; a reduction moves that time earlier by exactly its size.
/// </summary>
public class ElarionCooldownDurationSimple
{
    private const double Tolerance = 0.002;

    private static double ReadyAt(StepDriver f, Ability ability)
    {
        var cooldown = f.Hero.CooldownOf(ability);
        return f.Now.TotalSeconds + (cooldown.NextChargeAt - f.Now).TotalSeconds;
    }

    /// <summary>
    /// Player: with Skyward Munitions, a Celestial Shot takes 1 second off Highwind Arrow's cooldown.
    /// Dev: Skyward Munitions taken; a Highwind Arrow, then a Celestial Shot; the charge is back 1 second sooner.
    /// </summary>
    [Fact]
    public void Skyward_Munitions_Celestial_Shot_Takes_1_Second_Off_Highwind_Arrow()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.SkywardMunitions.TalentId]);
        f.Cast<HighwindArrow>();
        double before = ReadyAt(f, f.Ability<HighwindArrow>());

        f.Cast<CelestialShot>();

        Assert.Equal(before - 1.0, ReadyAt(f, f.Ability<HighwindArrow>()), Tolerance);
    }

    /// <summary>
    /// Player: with Skyward Munitions, a Celestial Shot takes 1 second off Heartseeker Barrage's cooldown.
    /// Dev: Skyward Munitions taken; a barrage, then a Celestial Shot; the barrage is ready 1 second sooner.
    /// </summary>
    [Fact]
    public void Skyward_Munitions_Celestial_Shot_Takes_1_Second_Off_Heartseeker_Barrage()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.SkywardMunitions.TalentId]);
        f.Cast<HeartseekerBarrage>();
        double before = f.Now.TotalSeconds + f.Hero.GetCooldownRemains<HeartseekerBarrage>(f.Now);

        f.Cast<CelestialShot>();

        Assert.Equal(before - 1.0, f.Now.TotalSeconds + f.Hero.GetCooldownRemains<HeartseekerBarrage>(f.Now), Tolerance);
    }

    /// <summary>
    /// Player: with Skyward Munitions, a Multishot takes 1 second off both Highwind Arrow's and Heartseeker Barrage's cooldowns.
    /// Dev: Skyward Munitions taken; a Highwind Arrow and a barrage, then a Multishot; both are ready 1 second sooner.
    /// </summary>
    [Fact]
    public void Skyward_Munitions_Multishot_Takes_1_Second_Off_Highwind_Arrow_And_Heartseeker_Barrage()
    {
        var f = StepFights.Elarion(talents: [ElarionTalents.SkywardMunitions.TalentId]);
        f.Cast<HighwindArrow>();
        f.Cast<HeartseekerBarrage>();
        double arrowBefore = ReadyAt(f, f.Ability<HighwindArrow>());
        double barrageBefore = f.Now.TotalSeconds + f.Hero.GetCooldownRemains<HeartseekerBarrage>(f.Now);

        f.Cast<Multishot>();

        Assert.Equal(arrowBefore - 1.0, ReadyAt(f, f.Ability<HighwindArrow>()), Tolerance);
        Assert.Equal(barrageBefore - 1.0, f.Now.TotalSeconds + f.Hero.GetCooldownRemains<HeartseekerBarrage>(f.Now), Tolerance);
    }

    /// <summary>
    /// Player: with Repeating Stars, each Multishot takes 1.5 seconds off Starfall Volley's cooldown, whatever the number of enemies.
    /// Dev: Repeating Stars taken; 4 enemies; a Starfall Volley, then a Multishot; the volley is ready 1.5 sooner. (The game's talent;
    /// the Python sim's earlier game version took 0.3 per hit.)
    /// </summary>
    [Fact]
    public void Repeating_Stars_Takes_1_5_Seconds_Per_Multishot_Off_Starfall_Volley()
    {
        var f = StepFights.Elarion(enemies: 4, talents: [ElarionTalents.RepeatingStars.TalentId]);
        f.Cast<StarfallVolley>();
        double before = f.Now.TotalSeconds + f.Hero.GetCooldownRemains<StarfallVolley>(f.Now);

        f.Cast<Multishot>();

        Assert.Equal(before - 1.5, f.Now.TotalSeconds + f.Hero.GetCooldownRemains<StarfallVolley>(f.Now), Tolerance);
    }
}
