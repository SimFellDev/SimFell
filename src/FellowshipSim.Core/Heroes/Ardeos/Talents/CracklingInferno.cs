using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 183 / effect 2302: Infernal Wave gains critical strike chance, and its critical strikes burn the target for a
/// share of the damage they dealt. Ability 1568 has a different share in its text; the talent's share is used
/// (<see cref="DamageDoneToDotDamageScaler"/>). UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class CracklingInferno : Talent
{
    public const int EffectId = 2302;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var wave = ardeos.GetAbility<InfernalWave>();
        if (wave == null) return;
        wave.CritChanceBonus += IncreasedCritChance;
        var burn = new Burn(ardeos);
        wave.Hit += (in DamageContext hit) =>
        {
            if (hit.IsCrit && hit.Amount > 0 && hit.Target != null && hit.SimContext != null)
            {
                ardeos.Dots.Apply(hit.SimContext, burn, hit.Target, pool: hit.Amount * DamageDoneToDotDamageScaler);
            }
        };
    }

    /// <summary>The burn of a critical Infernal Wave: the damage it adds, dealt over <see cref="DotDuration"/>.</summary>
    private sealed class Burn(Ardeos owner)
        : DamageOverTime(owner, EffectId, TalentName, TickPeriod, DotDuration, DotStacking.Accumulate);
}
