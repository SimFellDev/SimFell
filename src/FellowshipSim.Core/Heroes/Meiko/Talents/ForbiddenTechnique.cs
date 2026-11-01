using FellowshipSim.Core.Heroes.Meiko.Abilities;

namespace FellowshipSim.Core.Heroes.Meiko.Talents;

/// <summary>
/// Talent 104: "Your Double Palm Strike finisher applies Forbidden Technique to the target for 30 seconds. Forbidden
/// Technique accumulates 18% of all damage you deal to the target. The accumulated damage erupts onto the target and nearby
/// enemies every 6 seconds, or if the enemy dies while the effect is active. Each strike from your Earthfist Barrage to a
/// target afflicted by Forbidden Technique adds 1 stack to the effect, up to 50 stacks. Each time Forbidden Technique
/// erupts, all additional stacks are consumed and you are granted 1.2% Damage Reduction for each stack consumed for 18
/// seconds. You can only have one Forbidden Technique active at any time." Effect 1508 on the enemy (30s, period 6,
/// maxStacks 50); the eruption is effect 1509, logged as such, with damage falloff beyond 5 targets (talent 104
/// targetCountFalloff). Combat logs (4 casts): eruptions 6.02, 12.01, 18.02, 24.01 and 30.02 seconds after the Double Palm
/// Strike. The eruption is taken to deal the amount accumulated to every enemy it strikes, with no crit: UNVERIFIED.
/// </summary>
public class ForbiddenTechnique : Talent
{
    public const int TalentId = 104;
    public const string TalentName = "Forbidden Technique";
    public const int DebuffEffectId = 1508;
    public const int EruptionEffectId = 1509;
    public const double DebuffSeconds = 30.0;
    public const double EruptionPeriodSeconds = 6.0;
    public const int MaxStacks = 50;
    public const double DamageAccumulated = 0.18;
    public const double TargetThreshold = 5;

    public static readonly AuraDef Debuff = AuraDef.Effect(TalentName, DebuffEffectId, DebuffSeconds, MaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    private EnemyTarget? _marked;
    private double _accumulated;
    private PeriodicEffect? _eruptions;
    private SimulationContext? _fight;
    private SimTime _lastSeen;

    /// <summary>A new fight (or a new iteration of one, its clock back at 0) starts with no Forbidden Technique.</summary>
    private void Follow(SimulationContext ctx)
    {
        if (_fight != ctx || ctx.CurrentTime < _lastSeen)
        {
            _marked = null;
            _accumulated = 0.0;
            _eruptions = null;
            _fight = ctx;
        }
        _lastSeen = ctx.CurrentTime;
    }

    public override void Bind(Hero hero)
    {
        if (hero.GetAbility<DoublePalmStrike>() is not { } doublePalm) return;
        int eruptionId = LogIds.Effect(EruptionEffectId);
        doublePalm.Struck += (ctx, target) => Mark(ctx, hero, target, eruptionId);
        hero.OnDamageDealt += (in hit) =>
        {
            if (hit.SimContext is not { } ctx) return;
            Follow(ctx);
            if (hit.Target != null && hit.Target == _marked && hit.AbilityId != eruptionId) _accumulated += hit.Amount * DamageAccumulated;
        };
        if (hero.GetAbility<EarthfistBarrage>() is { } barrage)
        {
            barrage.Punched += (ctx, target) =>
            {
                if (target == _marked) target.Auras.Apply(Debuff, ctx.CurrentTime);
            };
        }
        hero.OnEnemyDefeated += (ctx, enemy) =>
        {
            Follow(ctx);
            if (enemy != _marked) return;
            Erupt(ctx, hero, enemy, eruptionId);
            _eruptions?.Stop(ctx);
        };
    }

    /// <summary>Forbidden Technique is on <paramref name="target"/> alone: one at a time.</summary>
    private void Mark(SimulationContext ctx, Hero hero, EnemyTarget target, int eruptionId)
    {
        Follow(ctx);
        _eruptions?.Stop(ctx);
        _marked?.Auras.Remove(Debuff, ctx.CurrentTime);
        _marked = target;
        _accumulated = 0.0;
        target.Auras.Apply(Debuff, ctx.CurrentTime);
        _eruptions = PeriodicEffect.Begin(ctx, TalentName, DebuffSeconds, EruptionPeriodSeconds, tickOnStart: false, partialFinalTick: false,
            onTick: (c, _) => Erupt(c, hero, target, eruptionId),
            onEnd: (c, _) =>
            {
                if (_marked != target) return;
                target.Auras.Remove(Debuff, c.CurrentTime);
                _marked = null;
            });
    }

    /// <summary>The damage accumulated erupts onto <paramref name="target"/> and the enemies near it.</summary>
    private void Erupt(SimulationContext ctx, Hero hero, EnemyTarget target, int eruptionId)
    {
        double amount = _accumulated;
        _accumulated = 0.0;
        if (amount <= 0) return;
        IReadOnlyList<EnemyTarget> nearby = ctx.ActiveEnemies.Count == 0 ? [target] : ctx.ActiveEnemies;
        var struck = ctx.Outcomes.SelectTargets(eruptionId, nearby, ctx);
        double falloff = struck.Count > TargetThreshold ? Math.Sqrt(TargetThreshold / struck.Count) : 1.0;
        for (int i = 0; i < struck.Count; i++)
        {
            ctx.ApplyDamage(hero, struck[i], amount * falloff, eruptionId, TalentName, canCrit: false, 0.0, 1.0);
        }
    }
}
