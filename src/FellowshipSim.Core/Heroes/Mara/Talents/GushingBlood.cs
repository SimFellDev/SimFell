using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 122: "While Maiden of Death is active, your Hemorrhaging Strike applies it's bleed to additional nearby
/// enemies. Your Hemorrhaging Strike's Bleed deals more damage to enemies that are afflicted by your Seething Poison."
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class GushingBlood : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        mara.GetAbility<HemorrhagingStrike>()?.ExtraBleedTargets.Add((ctx, target) =>
            hero.Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime) ? ctx.ActiveEnemiesExcept(target).Take(Constants.AdditionalMaxTargets) : []);
        mara.Hemorrhage.TargetMultipliers.Add((enemy, ctx) => mara.SeethingPoison.IsOn(enemy, ctx) ? Constants.ChargedDoubleAttackBuilderPoisonBleedMultiplier : 1.0);
    }
}
