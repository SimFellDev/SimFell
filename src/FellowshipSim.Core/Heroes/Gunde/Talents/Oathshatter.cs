using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 762: "Your Heart Splitter has increased Critical Strike chance. When the direct damage portion of Heart
/// Splitter Critically Strikes, it causes Exsanguinate to explode, dealing a share of its damage to all enemies near your
/// target. (Damage reduced beyond a target threshold)" Each other enemy takes <see cref="Constants.AoeDamageScaler"/> of
/// Exsanguinate, times sqrt(<see cref="Constants.TargetThresholdScaling"/> / N) beyond that many other enemies. Blueprint
/// GA_Gunde_Passive_Talent_HeavyMeleeDotBased_CritToAoe (build 26320967): the falloff's threshold is the row
/// Gunde.HeavyMeleeDotBased.Talent.CritToAoe.TargetThresholdScaling and its count the enemies acquired, the strike's
/// target excluded. Combat logs (patch 0.5.0.0, the explosion's hit over the strike's own Exsanguinate): 0.5000 on 9 and
/// on 10 others, 0.4767 on 11 and 0.4082 on 15 (Ab1yCxVcNBHPz6t8 fight 7: 99, 190, 121 and 135 hits; CPNgLQD4Htynkh2G
/// fight 6: 153, 100, 264 and 390 hits; 19 of the 20 fights, 15,877 hits on 9 to 22 others). The explosion is logged as
/// Exsanguinate.
/// </summary>
public partial class Oathshatter : Talent
{
    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var heartSplitter = hero.GetAbility<HeartSplitter>();
        if (heartSplitter == null) return;
        heartSplitter.CritChanceBonus += Constants.IncreasedCritChanceAdditionalCritChance;
        int id = LogIds.Effect(HeartSplitter.ExsanguinateEffectId);
        heartSplitter.Exsanguinated += (ctx, target, damage, crit) =>
        {
            if (!crit) return;
            var others = ctx.Outcomes.SelectTargets(id, ctx.ActiveEnemiesExcept(target), ctx).Where(e => e != target).ToList();
            if (others.Count == 0) return;
            double falloff = others.Count > Constants.TargetThresholdScaling ? Math.Sqrt((double)Constants.TargetThresholdScaling / others.Count) : 1.0;
            foreach (var enemy in others)
            {
                ctx.ApplyDamage(hero, enemy, damage * Constants.AoeDamageScaler * falloff, id, HeartSplitter.ExsanguinateName, canCrit: false, 0.0, 1.0,
                    abilityGroup: heartSplitter.Name);
            }
        };
    }
}
