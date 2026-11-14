using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 762: "Your Heart Splitter has increased Critical Strike chance. When the direct damage portion of Heart
/// Splitter Critically Strikes, it causes Exsanguinate to explode, dealing a share of its damage to all enemies near your
/// target. (Damage reduced beyond a target threshold)" Combat logs (n7zxK8harZT2wjDB fight 9 @ 16.403, one Exsanguinate
/// on 15 others; Y4wpb2f3gc8LqJDX fight 25, four casts alike on 26 others): each other enemy takes
/// the share with a square-root falloff beyond a threshold of others lower than the tooltip's. The explosion is logged as Exsanguinate.
/// </summary>
public partial class Oathshatter : Talent
{
    public const double ExplosionTargetThreshold = 8;

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
            double falloff = others.Count > ExplosionTargetThreshold ? Math.Sqrt(ExplosionTargetThreshold / others.Count) : 1.0;
            foreach (var enemy in others)
            {
                ctx.ApplyDamage(hero, enemy, damage * Constants.AoeDamageScaler * falloff, id, HeartSplitter.ExsanguinateName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
