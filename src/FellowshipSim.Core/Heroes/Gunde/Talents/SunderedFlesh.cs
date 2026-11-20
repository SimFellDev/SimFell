using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 765: "When your Rend or Slaughter contains more damage than an enemy has left in remaining health, they
/// instantly take that damage." Blueprint GA_Gunde_Passive_Talent_InstantDeathFromDot (build 26320967): the check runs
/// after each tick of Rend or Slaughter and at no other time. It skips training dummies. It fires when
/// 0 &lt; health &lt; the damage the bleed holds (Rend's <see cref="Rend.RawTotal"/>, Slaughter's ticks left times its tick),
/// and deals the enemy's health plus one. The bleed stays on the enemy. The hit's effect has no record in the game data
/// and takes the Rend effect's ability, so it is logged as Rend. The game multiplies the bleed by
/// <c>CasualDamageScaling</c>, an attribute of casual leagues alone, taken here as 1. UNVERIFIED: no combat log has this
/// talent.
/// </summary>
public partial class SunderedFlesh : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        gunde.SunderedFleshCheck = (ctx, target, held) =>
        {
            if (target.IsTrainingDummy || target.IsSleeping || target.MaxHealth <= 0) return;
            double health = target.CurrentHealth;
            if (health <= 0.0 || health >= held) return;
            // A forced non-crit rolls no spread: the hit is the enemy's health plus one. Its preset is Empty
            // (GE_Gunde_Talent_InstantDeathFromDot_Damage, effect 3209, build 26320967): no incoming-damage multiplier.
            ctx.ApplyDamage(gunde, target, health + 1.0, Rend.AbilityId, Rend.AbilityName, canCrit: false, 0.0, 1.0, forcedCrit: false);
        };
    }
}
