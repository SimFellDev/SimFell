using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>
/// Talent 36: each Freezing Torrent hit adds a stack of Coalescing Frost (effect 2306) to its target (refreshed);
/// a crit can add extra stacks. When it expires, or the enemy dies (per the user), it erupts for an Intellect fraction per
/// stack on enemies around it (damage effect 2300). Values: talent text and game data. UNVERIFIED: no combat log has this talent.
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
    /// Damage falls off as sqrt(T/N) beyond T enemies: T is the game data value (game data's targetCountFalloff on effect 2300). One log fit a higher threshold better (PCaMVTFbZzNLrfx3 fight 5);
    /// the game data is used (per the user).
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

        // Tooltip: Freezing Torrent critical strikes have a chance to apply extra stacks.
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
    /// number. Combat logs (10 fights of patch 0.5.0.0, 300 eruptions): the erupting enemy's hit over the others' follows
    /// the target falloff with N enemies struck, and is even up to the threshold plus one.
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

    /// <summary>The eruption's damage: <see cref="CoefficientPerStack"/> Intellect per stack, logged under effect 2300, with the falloff beyond <see cref="TargetThreshold"/> targets.</summary>
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
