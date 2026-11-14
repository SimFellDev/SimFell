namespace FellowshipSim.Core.Customization.Glyphs;

/// <summary>
/// Glyph 712: every few BASIC abilities (<see cref="Constants.CounterThresholdByRank"/>), the target erupts, dealing that hit's damage
/// again (effect 3193, cannot crit) to all enemies around it, increased by your Critical Strike chance up to
/// <see cref="Constants.CritChanceThreshold"/>. Counter: effect 3194. Beyond <see cref="Constants.TargetCountScaling"/> targets the eruption loses
/// damage as sqrt(TargetCountScaling/N) (fs_tc_uploads s3/gear_data.json; combat logs: a Cold Snap crit on many enemies
/// matches the formula exactly).
/// </summary>
public partial class TheVehement : Glyph
{
    public const int DamageEffectId = 3193;
    public const string DamageName = "The Vehement's Disdain";
    public const int CounterEffectId = 3194;

    public override int Id => GlyphId;
    public override string Name => GlyphName;

    public override void Bind(Hero hero)
    {
        int threshold = (int)ForRank(Constants.CounterThresholdByRank);
        const double critCap = Constants.CritChanceThreshold;
        var counter = hero.Register(AuraDef.Effect(GlyphName, CounterEffectId, maxStacks: threshold - 1));
        int damageId = LogIds.Effect(DamageEffectId);
        const string damageName = DamageName;
        // The counter advances when a BASIC ability's hit lands (combat logs: Frost Bolt counts when the bolt hits, not when
        // its cast completes), and the hit that reaches the threshold erupts.
        // OPEN: gpfHLmTBrqcd3Zn4 fight 37 erupts one hit early, each time on a Cold Snap that consumed Frostwyrm's Spite
        // with the counter near its threshold; PCaMVTFbZzNLrfx3 fight 5 never does in the same situation. Not modeled.
        var basicIds = hero.Abilities.Where(a => a.Category == AbilityCategory.Basic).Select(a => a.DamageLogId).ToHashSet();
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.SimContext == null || dmg.Target == null || !basicIds.Contains(dmg.AbilityId)) return;
            // The count is the counter's stacks: the hero's death removes the counter and the count starts again (combat
            // logs: GWChJ8P7tn6fHgV3 fight 20 @ 551.968 and 824.381, the counter removed right after each death, the next
            // BASIC hit at the first stack).
            int hits = hero.Auras.GetStacks(counter, dmg.Timestamp) + 1;
            if (hits < threshold)
            {
                hero.Auras.Apply(counter, dmg.Timestamp, setStacks: hits);
                return;
            }
            hero.Auras.Remove(counter, dmg.Timestamp);
            var ctx = dmg.SimContext;
            var enemies = ctx.Outcomes.SelectTargets(damageId, ctx.ActiveEnemies, ctx);
            if (enemies.Count == 0) return;
            double falloff = enemies.Count > Constants.TargetCountScaling ? Math.Sqrt((double)Constants.TargetCountScaling / enemies.Count) : 1.0;
            double damage = dmg.Amount * (1.0 + Math.Min(hero.CritChance(dmg.Timestamp), critCap)) * falloff;
            foreach (var enemy in enemies)
            {
                ctx.ApplyDamage(hero, enemy, damage, damageId, damageName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
