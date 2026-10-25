using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 226: "The cooldown of your Engulfing Flames is reduced by 0.25 seconds each time your Searing Blaze deals
/// damage to an enemy and by 1 second each time you cast Infernal Wave."
/// </summary>
public class RollingFlames : Talent
{
    public const int TalentId = 226;
    public const string TalentName = "Rolling Flames";
    public const double PerSearingBlazeHitSeconds = 0.25;
    public const double PerInfernalWaveSeconds = 1.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var engulfing = hero.GetAbility<EngulfingFlames>();
        var searing = hero.GetAbility<SearingBlaze>()?.Dot;
        if (engulfing == null) return;
        if (searing != null)
        {
            searing.Hit += (in DamageContext hit) => hero.ReduceCooldown(engulfing.Name, SimTime.FromSeconds(PerSearingBlazeHitSeconds), hit.Timestamp);
        }
        hero.OnCastCompleted += (ability, _, now) =>
        {
            if (ability is InfernalWave) hero.ReduceCooldown(engulfing.Name, SimTime.FromSeconds(PerInfernalWaveSeconds), now);
        };
    }
}
