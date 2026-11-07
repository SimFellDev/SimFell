using FellowshipSim.Core.Heroes.Helena;
using FellowshipSim.Core.Heroes.Helena.Abilities;

namespace FellowshipSim.Core.Customization.Legendaries;

/// <summary>
/// Legendary power 389 (Helena's Warmaster Breastplate, item 5898): "The first hit of your Shield Throw triggers explosion of
/// metal, dealing damage equal to 40% of the hit that triggered it to the target and up to 11 additional nearby enemies.
/// Razor Shrapnel applies Agonizing Wound to targets, reducing the damage they deal to you by 20% for 5 seconds." The hits
/// are effect 2357 and the debuff effect 2361. Combat logs: no Razor Shrapnel hit crits.
/// </summary>
public class RazorCrash : Legendary
{
    public const int PowerId = 389;
    /// <summary>Warmaster Breastplate.</summary>
    public const int BreastplateItemId = 5898;
    public const double ShareOfHit = 0.4;
    public const int ExtraTargets = 11;
    public const int ShrapnelEffectId = 2357;
    public const int AgonizingWoundEffectId = 2361;
    public const double AgonizingWoundSeconds = 5.0;

    /// <summary>Agonizing Wound on every enemy hit: it deals less damage to Helena.</summary>
    public static readonly AuraDef AgonizingWound = AuraDef.Effect("Agonizing Wound", AgonizingWoundEffectId, AgonizingWoundSeconds);

    public override int LegendaryPowerId => PowerId;
    public override string Name => "Razor Crash";
    public override string HeroName => "Helena";
    public override IReadOnlyList<int> ItemIds => [BreastplateItemId];

    public override void Bind(Hero hero)
    {
        if (hero is not Helena helena || hero.GetAbility<ShieldThrow>() is not { } shieldThrow) return;
        var shrapnel = new RazorShrapnel(helena);
        shieldThrow.FirstHitLanded += (ctx, target, dealt) =>
        {
            if (dealt <= 0) return;
            shrapnel.Amount = dealt * ShareOfHit;
            var targets = new List<EnemyTarget>();
            if (!target.IsSleeping) targets.Add(target);
            foreach (var other in ctx.ActiveEnemiesExcept(target))
            {
                if (targets.Count > ExtraTargets) break;
                targets.Add(other);
            }
            foreach (var enemy in ctx.Outcomes.SelectTargets(shrapnel.DamageLogId, targets, ctx))
            {
                shrapnel.DealDamage(ctx, enemy, canCrit: false);
                enemy.Auras.Apply(AgonizingWound, ctx.CurrentTime);
            }
        };
    }

    /// <summary>
    /// Razor Shrapnel's hits: the share of the first hit's damage, times Helena's multipliers against each enemy hit
    /// (Aftershock). UNVERIFIED: in the 18 fights of patch 0.5.0.0, log hits are 1.15 times the engine's with the
    /// multipliers, and 1.19 without them.
    /// </summary>
    private sealed class RazorShrapnel : Ability
    {
        public RazorShrapnel(Helena owner) : base(owner, LogIds.Effect(ShrapnelEffectId), "Razor Shrapnel") =>
            DamageLogId = LogIds.Effect(ShrapnelEffectId);

        /// <summary>Damage of each hit of the explosion now going off.</summary>
        public double Amount { get; set; }

        public override double BaseDamage(SimulationContext ctx, EnemyTarget? target, double damageMultiplier = 1.0) =>
            Amount * Owner.DamageMultiplierAgainst(target, ctx.CurrentTime) * damageMultiplier;

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
