using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 151: Infernal Wave deals increased damage for each of the hero's unique damage over time effects active on the
/// target. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class IntensifyingInferno : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var wave = ardeos.GetAbility<InfernalWave>();
        if (wave == null) return;
        hero.OnCastResolving += cast =>
        {
            if (cast.Ability != wave || cast.Target == null) return;
            int unique = ardeos.Dots.On(cast.Target).Select(d => d.Effect).Distinct().Count();
            cast.DamageMultiplier *= 1.0 + DamageIncreaseFromDotsDamageIncreasePerDot * unique;
        };
    }
}
