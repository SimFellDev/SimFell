using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 151: "Your Infernal Wave deals +15% increased damage for each of your unique damage over time effects that are
/// active on the target." UNVERIFIED: no combat log has this talent.
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
            int unique = ardeos.Dots.On(cast.Target).Select(d => d.Effect).Distinct().Count();
            cast.DamageMultiplier *= 1.0 + DamagePerEffect * unique;
        };
    }
}
