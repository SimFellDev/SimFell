using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 672: "Your Chain Lightning deals +65% increased damage for each unique enemy other than your target that it
/// hits." Each hit counts the other enemies hit so far, itself included.
/// </summary>
public class TheMotherload : Talent
{
    public const int TalentId = 672;
    public const string TalentName = "The Motherload";
    /// <summary>
    /// UNVERIFIED, taken as a developer bug: the tooltip says +65% (the game data has no other value), but in combat logs
    /// each other enemy adds about +50% (q3y7pWgXrjN2dftw fight 12: median non-crit hit by bounce in 182 clean 9-hit
    /// chains, 1 + 0.49 per bounce). The engine models the log until the game data or more logs say otherwise.
    /// </summary>
    public const double DamagePerEnemy = 0.50;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is { } chainLightning) chainLightning.DamagePerOtherEnemy += DamagePerEnemy;
    }
}
