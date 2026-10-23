using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 762: "Your Heart Splitter has +30% increased Critical Strike chance. When the direct damage portion of Heart
/// Splitter Critically Strikes, it causes Exsanguinate to explode, dealing 50% of its damage to all enemies near your
/// target. (Damage reduced beyond 10 targets)" Combat logs (n7zxK8harZT2wjDB fight 9 @ 16.403: 1640 on 15 others of a
/// 4492 Exsanguinate; Y4wpb2f3gc8LqJDX fight 25: 0.2773 of it on 26 others, four casts alike): each other enemy takes
/// 50% x sqrt(8/N) beyond 8 others, not the tooltip's 10. The explosion is logged as Exsanguinate.
/// </summary>
public class Oathshatter : Talent
{
    public const int TalentId = 762;
    public const string TalentName = "Oathshatter";
    public const double CritChance = 0.3;
    public const double ExplosionShare = 0.5;
    public const double ExplosionTargetThreshold = 8;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var heartSplitter = hero.GetAbility<HeartSplitter>();
        if (heartSplitter == null) return;
        heartSplitter.CritChanceBonus += CritChance;
        int id = LogIds.Effect(HeartSplitter.ExsanguinateEffectId);
        heartSplitter.Exsanguinated += (ctx, target, damage, crit) =>
        {
            if (!crit) return;
            var others = ctx.Outcomes.SelectTargets(id, ctx.ActiveEnemiesExcept(target), ctx).Where(e => e != target).ToList();
            if (others.Count == 0) return;
            double falloff = others.Count > ExplosionTargetThreshold ? Math.Sqrt(ExplosionTargetThreshold / others.Count) : 1.0;
            foreach (var enemy in others)
            {
                ctx.ApplyDamage(hero, enemy, damage * ExplosionShare * falloff, id, HeartSplitter.ExsanguinateName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
