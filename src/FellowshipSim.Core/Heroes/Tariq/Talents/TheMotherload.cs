using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Core.Heroes.Tariq.Talents;

/// <summary>
/// Talent 672: "Your Chain Lightning deals increased damage for each unique enemy other than your target that it
/// hits." Each hit counts the other enemies hit so far, itself included. UNVERIFIED: the tooltip states a larger bonus than
/// the game data. Combat logs (q3y7pWgXrjN2dftw fight 12: median non-crit hit by bounce in 182 clean chains) have the game
/// data's bonus.
/// </summary>
public partial class TheMotherload : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<ChainLightning>() is { } chainLightning) chainLightning.DamagePerOtherEnemy += DamagePerUniqueTargetScaler;
    }
}
