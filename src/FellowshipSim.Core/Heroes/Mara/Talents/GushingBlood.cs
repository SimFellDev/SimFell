using FellowshipSim.Core.Heroes.Mara.Abilities;

namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent 122: "While Maiden of Death is active, your Hemorrhaging Strike applies it's bleed to additional nearby
/// enemies. Your Hemorrhaging Strike's Bleed deals more damage to enemies that are afflicted by your Seething Poison."
/// While Maiden of Death is active, each application of the bleed is a re-application: the struck enemy's bleed is taken off,
/// and a copy goes afresh on up to <see cref="Constants.AdditionalMaxTargets"/> other enemies, those without Mara's bleed
/// first, then on the struck enemy, with the struck enemy's duration and period restarted. Game data (build 26320967), ability
/// 1517 (`GA_Mara_Passive_Talent_ChargedResourceRefillSelfBuff_BleedAttackSpenderBleedAoe`): `WaitGameplayEffectAppliedToTarget` on
/// Mara's bleed, a refresh too, gated on Mara's effect with asset tag `CustomAssetTags.Mara.ChargedResourceRefillSelfBuff`
/// (offsets 2735 to 2830); targets within radius 600 of the struck enemy, `PreferActorsWithoutEffect` on the bleed, closest
/// first, at most 4, angle 270, then the struck enemy (offsets 319 to 759). The engine has no positions, so it takes the
/// enemies in its own order, with no radius. Combat logs (fjrCax7VzGmMZw6h fight 26, Mara player 23, the one Mara player with
/// it): the 114 Hemorrhaging Strikes with a re-application are all in Maiden of Death, 108 on a bleed they refresh and 6 on a fresh
/// one, each a removal and applications in the cast's frame, every copy with the struck enemy's new duration (@ 24.677: 39000ms
/// on 77.1 and 4 others; @ 21.570: 30000ms on a fresh one), and 62 of them with no other enemy.
/// </summary>
public partial class GushingBlood : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var mara = (Mara)hero;
        mara.Hemorrhage.ReapplicationTargets = (ctx, target) => hero.Auras.HasAura(MaidenOfDeath.Buff, ctx.CurrentTime)
            ? ctx.ActiveEnemiesExcept(target).OrderBy(e => mara.Hemorrhage.IsOn(e, ctx)).Take(Constants.AdditionalMaxTargets)
            : null;
        mara.Hemorrhage.TargetMultipliers.Add((enemy, ctx) => mara.SeethingPoison.IsOn(enemy, ctx) ? Constants.ChargedDoubleAttackBuilderPoisonBleedMultiplier : 1.0);
    }
}
