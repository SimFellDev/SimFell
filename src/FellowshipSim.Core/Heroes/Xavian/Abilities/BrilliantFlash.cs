namespace FellowshipSim.Core.Heroes.Xavian.Abilities;

/// <summary>
/// Ability 1855: "Instantly heal target ally ... or deal damage to target enemy." It needs and consumes a stack of Swift
/// Reprieval, or of Omega Reprieval first when Xavian has one (effect 2574: "Your next Brilliant Flash is Enhanced, does
/// not consume Swift Reprievals and replenishes 12% of your maximum Mana"). Enhanced: "50% increased power on your Main
/// Target ... When cast on an enemy, Brilliant Flash is also casted on all enemies within a large range of that enemy."
/// BRILLIANT SPIRIT: "a chance equal to your Spirit % to trigger a Spirit Refund. When it does, you gain 50 Mana".
/// </summary>
public partial class BrilliantFlash : XavianAbility
{
    /// <summary>Game data targetCountFalloff: effect 2565 (Brilliant Flash), threshold 3.</summary>
    public const double TargetThreshold = 3;
    /// <summary>Ability 1861's text: 50% increased power on the main target.</summary>
    public const double EnhancedMainTargetMultiplier = 1.5;
    /// <summary>Sune.BuffedTargetedCastedHealOrDamage.ProcConsumed.MaxManaFractionGain: 12% of the maximum Mana.</summary>
    public const double EnhancedManaShare = 0.12;
    /// <summary>The hero's Spirit text: 50 Mana when Brilliant Flash triggers a Spirit Refund.</summary>
    public const double BrilliantSpiritMana = 50;

    /// <summary>Raised after a cast's hits, with the main target (Celestial Favor, Shining Halo's Brilliance, Sun's Verdict).</summary>
    public event Action<SimulationContext, EnemyTarget>? Flashed;

    /// <summary>Raised when a cast consumes a Swift Reprieval (Grossly Incandescent).</summary>
    public event Action<SimulationContext>? SwiftReprievalConsumed;

    /// <summary>Raised for each hit's damage, with its enemy (Sun's Touch).</summary>
    public event Action<SimulationContext, EnemyTarget, double>? Dealt;

    public BrilliantFlash(Xavian owner) : base(owner, AbilityId, AbilityName, AbilityCategory)
    {
        BaseCoefficient = DamageStrengthCoefficient;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        var now = ctx.CurrentTime;
        if (Xavian.SwiftReprievals(now) == 0 && !Xavian.Auras.HasAura(OmegaReprieval.Buff, now))
        {
            reason = explain ? $"{Name} needs Swift Reprieval or Omega Reprieval" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var now = ctx.CurrentTime;
        var hero = Xavian;
        bool enhanced = hero.Auras.HasAura(OmegaReprieval.Buff, now);
        if (enhanced)
        {
            hero.Auras.ConsumeStack(OmegaReprieval.Buff, now);
            var enemies = Area(ctx, target);
            var struck = ctx.Outcomes.SelectTargets(DamageLogId, enemies, ctx);
            // Combat logs (16 Xavian players, 1,932 enhanced casts): the main target takes the full 150%, and the other N
            // enemies lose damage as sqrt(3/N) over N alone (log/engine 1.14 at N = 3, 1.05 at N = 12 when the main target
            // was counted).
            int others = 0;
            for (int i = 0; i < struck.Count; i++) if (struck[i] != target) others++;
            double falloff = Falloff(others, TargetThreshold);
            for (int i = 0; i < struck.Count; i++)
            {
                var enemy = struck[i];
                double multiplier = enemy == target ? EnhancedMainTargetMultiplier : falloff;
                DealDamage(ctx, enemy, multiplier, onLanded: (c, dealt) => Dealt?.Invoke(c, enemy, dealt));
            }
            hero.GainMana(Xavian.HeroMaxMana * EnhancedManaShare, ctx, target);
        }
        else
        {
            hero.Auras.ConsumeStack(Xavian.SwiftReprieval, now);
            SwiftReprievalConsumed?.Invoke(ctx);
            if (target is { IsSleeping: false }) DealDamage(ctx, target, onLanded: (c, dealt) => Dealt?.Invoke(c, target, dealt));
        }
        if (hero.RollBrilliantSpirit(ctx)) hero.GainMana(BrilliantSpiritMana, ctx);
        Flashed?.Invoke(ctx, target);
        hero.RaiseOffensiveAbilityUsed(ctx, this);
    }
}
