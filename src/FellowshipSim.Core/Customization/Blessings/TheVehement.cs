namespace FellowshipSim.Core.Customization.Blessings;

/// <summary>
/// Blessing 712: every 6 / 5 / 4 / 3 BASIC abilities, the target erupts, dealing that hit's damage again (effect 3193,
/// cannot crit) to all enemies around it, increased by your Critical Strike chance up to 50%. Counter: effect 3194.
/// Beyond 5 targets the eruption loses damage as sqrt(5/N) (TargetCountScaling 5, fs_tc_uploads s3/gear_data.json;
/// combat logs: e.g. a 7,818 Cold Snap crit -> 7,818 x 1.5 x sqrt(5/20) = 5,864 on 20 enemies, exactly as logged).
/// </summary>
public class TheVehement : Blessing
{
    public const int BlessingId = 712;
    public const string BlessingName = "The Vehement";
    public const int DamageEffectId = 3193;
    public const string DamageName = "The Vehement's Disdain";
    public const int CounterEffectId = 3194;
    public const double TargetCountScaling = 5.0;
    /// <summary>The eruption's bonus from Critical Strike chance is capped at this.</summary>
    public const double CritChanceCap = 0.5;
    /// <summary>BASIC hits per eruption.</summary>
    public static readonly double[] HitsPerEruptionByRank = [6, 5, 4, 3];

    public override int Id => BlessingId;
    public override string Name => BlessingName;

    public override void Bind(Hero hero)
    {
        int threshold = (int)ForRank(HitsPerEruptionByRank);
        const double critCap = CritChanceCap;
        var counter = hero.Register(AuraDef.Effect(BlessingName, CounterEffectId, maxStacks: threshold - 1));
        int damageId = LogIds.Effect(DamageEffectId);
        const string damageName = DamageName;
        // The counter advances when a BASIC ability's hit lands (combat logs: Frost Bolt counts when the bolt hits, not when
        // its cast completes), and the hit that reaches the threshold erupts.
        // OPEN: gpfHLmTBrqcd3Zn4 fight 37 erupts one hit early 12 times, each on a Cold Snap that consumed Frostwyrm's Spite
        // with the counter at 2; PCaMVTFbZzNLrfx3 fight 5 never does in the same situation (10 times). Not modeled.
        var basicIds = hero.Abilities.Where(a => a.Category == AbilityCategory.Basic).Select(a => a.DamageLogId).ToHashSet();
        int hits = 0;
        // The fight the hits belong to (each sim iteration and replay has its own).
        SimulationContext? fight = null;
        hero.OnDamageDealt += (in dmg) =>
        {
            if (dmg.SimContext == null || dmg.Target == null || !basicIds.Contains(dmg.AbilityId)) return;
            if (fight != dmg.SimContext)
            {
                fight = dmg.SimContext;
                hits = 0;
            }
            hits++;
            if (hits < threshold)
            {
                hero.Auras.Apply(counter, dmg.Timestamp, setStacks: hits);
                return;
            }
            hits = 0;
            hero.Auras.Remove(counter, dmg.Timestamp);
            var ctx = dmg.SimContext;
            var enemies = ctx.Outcomes.SelectTargets(damageId, ctx.ActiveEnemies, ctx);
            if (enemies.Count == 0) return;
            double falloff = enemies.Count > TargetCountScaling ? Math.Sqrt(TargetCountScaling / enemies.Count) : 1.0;
            double damage = dmg.Amount * (1.0 + Math.Min(hero.CritChance(dmg.Timestamp), critCap)) * falloff;
            foreach (var enemy in enemies)
            {
                ctx.ApplyDamage(hero, enemy, damage, damageId, damageName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
