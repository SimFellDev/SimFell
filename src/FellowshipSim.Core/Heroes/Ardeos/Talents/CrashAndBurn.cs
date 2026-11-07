using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 184: each time Searing Blaze deals damage to an enemy, Fire Ball's cooldown is reduced. UNVERIFIED: no combat log has this talent.
/// </summary>
public class CrashAndBurn : Talent
{
    public const int TalentId = 184;
    public const string TalentName = "Crash and Burn";
    public const double ReductionSeconds = 0.05;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var searing = hero.GetAbility<SearingBlaze>()?.Dot;
        var fireBall = hero.GetAbility<FireBall>();
        if (searing == null || fireBall == null) return;
        searing.Hit += (in DamageContext hit) => hero.ReduceCooldown(fireBall.Name, SimTime.FromSeconds(ReductionSeconds), hit.Timestamp);
    }
}
