using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 151: Infernal Wave deals increased damage for each of the hero's unique damage over time effects active on the
/// target. Per the user, each instance counts (two Engulfing Flames count twice), despite the tooltip's "unique".
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public class IntensifyingInferno : Talent
{
    public const int TalentId = 151;
    public const string TalentName = "Intensifying Inferno";
    public const double DamagePerEffect = 0.15;

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
            int effects = ardeos.Dots.On(cast.Target).Count;
            cast.DamageMultiplier *= 1.0 + DamagePerEffect * effects;
        };
    }
}
