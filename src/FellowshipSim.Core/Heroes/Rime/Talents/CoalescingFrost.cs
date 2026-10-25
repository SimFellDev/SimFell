using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 36: each Freezing Torrent hit adds a stack of Coalescing Frost (effect 2306) to its target for 3s (refreshed);
/// a crit has a 50% chance to add 2. When it expires, or the enemy dies (per the user), it erupts for 56% Intellect per
/// stack on enemies around it (damage effect 2300). Values: talent text; hero_data Freezing Torrent "DelayedAoe"
/// (DamageDividerToInt 0.56, TargetCountThreshold 5). UNVERIFIED: no combat log has this talent.
/// An example of a talent that deals its own damage: the eruption is a small ability of the talent (<see cref="Eruption"/>).
/// </summary>
public class CoalescingFrost : Talent
{
    public const int TalentId = 36;
    public const string TalentName = "Coalescing Frost";
    public const int DebuffEffectId = 2306;
    public const int DamageEffectId = 2300;
    public const double DurationSeconds = 3.0;
    public const double CoefficientPerStack = 0.56;
    public const double CritDoubleStackChance = 0.5;
    /// <summary>
    /// Damage falls off as sqrt(T/N) beyond T enemies: game data 5 (hero_data "DelayedAoe" TargetCountThreshold; the
    /// datamine's targetCountFalloff on effect 2300). One log fit 6 better (PCaMVTFbZzNLrfx3 fight 5: 1.10 with 5, 1.02
    /// with 6); the game data is used (per the user).
    /// </summary>
    public const double TargetThreshold = 5;
    public const string DoubleStackProc = "coalescing_frost_double";

    public override string Name => TalentName;
    public override int Id => TalentId;

    /// <summary>Stacks on each enemy, and a version that tells a pending expiry whether the stacks were refreshed since.</summary>
    private readonly Dictionary<EnemyTarget, (int Stacks, int Version)> _stacks = new();
    /// <summary>The fight the stacks belong to (each sim iteration and replay has its own).</summary>
    private SimulationContext? _fight;
    private Eruption? _eruption;

    public override void Bind(Hero hero)
    {
        var rime = (Rime)hero;
        var torrent = rime.GetAbility<FreezingTorrent>();
        if (torrent == null) return;
        _eruption = new Eruption(rime);
        hero.OnDamageDealtBy(torrent.DamageLogId, (in dmg) =>
        {
            if (dmg.Amount > 0 && dmg.Target != null && dmg.SimContext != null)
            {
                AddStacks(dmg.SimContext, dmg.Target, dmg.IsCrit);
            }
        });
    }

    private void AddStacks(SimulationContext ctx, EnemyTarget target, bool crit)
    {
        if (_fight != ctx)
        {
            // A new fight: forget the last one's stacks and erupt on enemies that die in this one.
            _stacks.Clear();
            ctx.EnemyDemising += (c, enemy) => Erupt(c, enemy, dying: true);
            _fight = ctx;
        }

        // "Freezing Torrent critical strikes have a 50% chance to apply 2 stacks."
        int added = crit && ctx.Outcomes.RollProc(DoubleStackProc, CritDoubleStackChance, ctx, target) ? 2 : 1;
        var (stacks, version) = _stacks.GetValueOrDefault(target);
        int expected = version + 1;
        _stacks[target] = (stacks + added, expected);
        ctx.Schedule(SimTime.FromSeconds(DurationSeconds), c =>
        {
            if (_stacks.TryGetValue(target, out var current) && current.Version == expected) Erupt(c, target, dying: false);
        }, "Coalescing Frost expires");
    }

    /// <summary>
    /// The eruption strikes its enemy for the full damage and the enemies around it with the target falloff over their
    /// number. Combat logs (10 fights of patch 0.5.0.0, 300 eruptions): the erupting enemy's hit over the others' is
    /// sqrt((N - 1) / 5) with N enemies struck (N = 8: 1.20, 10: 1.33, 12: 1.53, 16: 1.80, 19: 1.89), and 1 up to N = 6.
    /// </summary>
    private void Erupt(SimulationContext ctx, EnemyTarget target, bool dying)
    {
        if (!_stacks.Remove(target, out var state) || state.Stacks <= 0) return;
        var enemies = dying ? ctx.ActiveEnemiesExcept(target) : ctx.ActiveEnemies;
        if (enemies.Count == 0) return;
        var eruption = _eruption!;
        var struck = ctx.Outcomes.SelectTargets(eruption.DamageLogId, enemies, ctx);
        int around = 0;
        for (int i = 0; i < struck.Count; i++)
        {
            if (struck[i] != target) around++;
        }
        double falloff = around > TargetThreshold ? Math.Sqrt(TargetThreshold / around) : 1.0;
        for (int i = 0; i < struck.Count; i++)
        {
            eruption.DealDamage(ctx, struck[i], damageMultiplier: state.Stacks * (struck[i] == target ? 1.0 : falloff));
        }
    }

    /// <summary>The eruption's damage: 56% Intellect per stack, logged under effect 2300, sqrt(5/N) beyond 5 targets.</summary>
    private sealed class Eruption : Ability
    {
        public Eruption(Rime owner) : base(owner, LogIds.Effect(DamageEffectId), TalentName)
        {
            BaseCoefficient = CoefficientPerStack;
            AoeTargetThreshold = TargetThreshold;
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
