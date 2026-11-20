using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 760 / damage effect 3220: "Your Reaver's Edge collects all Blood Feathers within a radius. Each time you
/// Pick Up or Collect a Blood Feather, it triggers an explosion of blood emanating from you, dealing a share of Strength
/// as physical damage to nearby enemies (damage is reduced beyond a target threshold)." A sim picks up every feather anyway; the
/// explosion comes with every Owed in Blood gain. UNVERIFIED: no combat log has this talent.
/// </summary>
public partial class RavensPrecision : Talent
{
    public const int DamageEffectId = 3220;

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        var explosion = new Explosion(gunde);
        gunde.FeatherCollected += ctx =>
        {
            var targets = ctx.Outcomes.SelectTargets(explosion.DamageLogId, ctx.ActiveEnemies, ctx);
            if (targets.Count > 0) explosion.DealAoeDamage(ctx, targets);
        };
    }

    /// <summary>The explosion's damage: a share of Strength (<see cref="Constants.DotTransferTalentExplosionOnPickupStrengthCoefficient"/>), logged under effect 3220, falling off beyond <see cref="Constants.DotTransferTalentExplosionOnPickupTargetThresholdScaling"/> targets.</summary>
    private sealed class Explosion : Ability
    {
        public Explosion(Gunde owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = Constants.DotTransferTalentExplosionOnPickupStrengthCoefficient;
            AoeTargetThreshold = Constants.DotTransferTalentExplosionOnPickupTargetThresholdScaling;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
