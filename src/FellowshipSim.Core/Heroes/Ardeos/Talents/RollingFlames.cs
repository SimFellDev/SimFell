using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 226: Engulfing Flames' cooldown is reduced each time Searing Blaze deals damage to an enemy and each time
/// Infernal Wave is cast.
/// </summary>
public partial class RollingFlames : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var engulfing = hero.GetAbility<EngulfingFlames>();
        var searing = hero.GetAbility<SearingBlaze>()?.Dot;
        if (engulfing == null) return;
        if (searing != null)
        {
            searing.Hit += (in DamageContext hit) => hero.ReduceCooldown(engulfing.Name, SimTime.FromSeconds(InstantSingleDotReducesCooldownReductionInSeconds), hit.Timestamp);
        }
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is InfernalWave) hero.ReduceCooldown(engulfing.Name, SimTime.FromSeconds(ReductionInSeconds), now);
        };
    }
}
