using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 183 / effect 2302: Infernal Wave gains critical strike chance, and its critical strikes burn the target for a
/// share of the damage they dealt. Ability 1568 has a different share in its text; the talent's share is used
/// (<see cref="FractionOfCrit"/>). UNVERIFIED: no combat log has this talent.
/// </summary>
public class CracklingInferno : Talent
{
    public const int TalentId = 183;
    public const string TalentName = "Crackling Inferno";
    public const int EffectId = 2302;
    public const double CritChance = 0.20;
    public const double FractionOfCrit = 0.60;
    public const double PeriodSeconds = 3.0;
    public const double DurationSeconds = 24.0;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var wave = ardeos.GetAbility<InfernalWave>();
        if (wave == null) return;
        wave.CritChanceBonus += CritChance;
        var burn = new Burn(ardeos);
        wave.Hit += (in DamageContext hit) =>
        {
            if (hit.IsCrit && hit.Amount > 0 && hit.Target != null && hit.SimContext != null)
            {
                ardeos.Dots.Apply(hit.SimContext, burn, hit.Target, pool: hit.Amount * FractionOfCrit);
            }
        };
    }

    /// <summary>The burn of a critical Infernal Wave: the damage it adds, dealt over <see cref="DurationSeconds"/>.</summary>
    private sealed class Burn(Ardeos owner)
        : DamageOverTime(owner, EffectId, TalentName, PeriodSeconds, DurationSeconds, DotStacking.Accumulate);
}
