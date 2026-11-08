namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 35: "Your targeted, offensive abilities have a chance to spawn a Ruby Storm that travels forward, dealing damage
/// to all enemies it touches equal to a percentage by rank of your Current Health." (damage effect 90; it
/// scales with nothing else). UNVERIFIED: no log has this aspect; the hero is taken to be at full health and the storm to
/// touch every enemy in combat.
/// </summary>
public partial class RubyStorm : Aspect
{
    public const int DamageEffectId = 90;
    public const string ProcKey = "ruby_storm";

    public override int Id => AspectId;
    public override string Name => AspectName;

    public override void Bind(Hero hero)
    {
        double share = ForRank(HealthPercentageAsDamageByRank);
        int logId = LogIds.Effect(DamageEffectId);
        var rppm = new RealPpm(ProcKey, AspectProcsPerMinute);
        hero.OnCastCompleted += (ability, target, _) =>
        {
            if (target == null || ability.BaseCoefficient <= 0 || hero.SimContext is not { } ctx || !rppm.Roll(hero, ctx)) return;
            double damage = share * hero.Stats.MaxHealth;
            foreach (var enemy in ctx.Outcomes.SelectTargets(logId, ctx.ActiveEnemies, ctx))
            {
                ctx.ApplyDamage(hero, enemy, damage, logId, AspectName, canCrit: false, 0.0, 1.0);
            }
        };
    }
}
