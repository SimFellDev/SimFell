using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Core.Heroes.Ardeos.Talents;

/// <summary>
/// Talent 159 / damage effect 1810: Infernal Wave hits cause all enemies near the target with Searing Blaze active to
/// flare up, taking a share of the damage Infernal Wave dealt to the initial target. Damage falls off with the target
/// count (game data targetCountFalloff). Ability 1257 has a different share in its text; the talent's share is used.
/// UNVERIFIED: no combat log has this talent.
/// </summary>
public class FlareUp : Talent
{
    public const int TalentId = 159;
    public const string TalentName = "Flare up";
    public const int DamageEffectId = 1810;
    public const string DamageName = "Flare Up";
    public const double FractionOfHit = 0.50;
    public const double TargetThreshold = 3;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var ardeos = (Ardeos)hero;
        var wave = ardeos.GetAbility<InfernalWave>();
        var searing = ardeos.GetAbility<SearingBlaze>()?.Dot;
        if (wave == null || searing == null) return;
        wave.Hit += (in DamageContext hit) =>
        {
            if (hit.SimContext is not { } ctx || hit.Amount <= 0) return;
            var burning = ctx.ActiveEnemies.Where(e => ardeos.Dots.Has(e, searing)).ToList();
            var struck = ctx.Outcomes.SelectTargets(LogIds.Effect(DamageEffectId), burning, ctx);
            if (struck.Count == 0) return;
            double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
            double damage = hit.Amount * FractionOfHit * falloff;
            foreach (var enemy in struck)
            {
                ctx.ApplyDamage(hero, enemy, damage, LogIds.Effect(DamageEffectId), DamageName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
