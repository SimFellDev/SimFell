namespace FellowshipSim.Core.Customization.Aspects;

/// <summary>
/// Aspect 35: "Your targeted, offensive abilities have a chance to spawn a Ruby Storm that travels forward, dealing damage
/// to all enemies it touches equal to a percentage by rank of your Current Health." (damage effect 90). The storm is a hit
/// like an ability's that scales from health in place of the main stat: Expertise, crit and every damage bonus apply to it
/// (per the user). Only abilities with <see cref="Ability.TargetedOffensive"/> roll for it. UNVERIFIED: no log has this
/// aspect; the hero is taken to be at full health and the storm to touch every enemy in combat.
/// </summary>
public partial class RubyStorm : Aspect
{
    public const int DamageEffectId = 90;
    public const string ProcKey = "ruby_storm";

    public override int Id => AspectId;
    public override string Name => AspectName;

    public override void Bind(Hero hero)
    {
        var storm = new Storm(hero, ForRank(Constants.HealthPercentageAsDamageByRank));
        var rppm = new RealPpm(ProcKey, AspectProcsPerMinute);
        hero.OnCastCompleted += (ability, target, _) =>
        {
            if (target == null || !ability.TargetedOffensive || hero.SimContext is not { } ctx || !rppm.Roll(hero, ctx)) return;
            var enemies = ctx.Outcomes.SelectTargets(storm.DamageLogId, ctx.ActiveEnemies, ctx);
            for (int i = 0; i < enemies.Count; i++) storm.DealDamage(ctx, enemies[i]);
        };
    }

    /// <summary>The storm's hit: <see cref="Ability.BaseCoefficient"/> is the share of the hero's health.</summary>
    private sealed class Storm : Ability
    {
        public Storm(Hero owner, double healthShare) : base(owner, LogIds.Effect(DamageEffectId), AspectName)
        {
            BaseCoefficient = healthShare;
        }

        protected override double ScalingStat(SimTime now) => Owner.MaxHealth(now);

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
