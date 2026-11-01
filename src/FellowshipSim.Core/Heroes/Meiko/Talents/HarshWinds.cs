using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 98: "Your Spirited Vortex deals 20% more damage. Each time you perform a finisher ability while Spirited Vortex is
/// active it grants you +20% dodge chance, stacking up to 3 times. All stacks of the bonus dodge are consumed upon
/// successfully dodging an attack" (Meiko.DurationalAoeDamageBuff.Talent.IncreasedDamage.DamageMultiplier 1.2 on ability
/// 1120 and effect 1569). The dodge changes no damage.
/// </summary>
public class HarshWinds : Talent
{
    public const int TalentId = 98;
    public const string TalentName = "Harsh Winds";
    public const double VortexDamage = 1.2;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => hero.GetAbility<SpiritedVortex>()?.AddDamageMultiplier(VortexDamage);
}
