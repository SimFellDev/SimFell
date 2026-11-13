using FellowshipSim.Core.Engine;

namespace FellowshipSim.Core.Dungeons.Affixes;

/// <summary>
/// One dungeon fight as its affixes see it: the hero, the dungeon, whether the party is in combat and in a boss fight, each
/// enemy entering combat, and enemies the affix adds to the run in progress. <see cref="DungeonFight"/> makes it.
/// </summary>
internal sealed class AffixRun
{
    private readonly Func<string, double, double, bool, double, EnemyTarget?> _spawn;
    private readonly Func<bool> _inCombat, _bossFight;

    internal AffixRun(SimulationContext context, Hero hero, DungeonProfile dungeon, Func<bool> inCombat, Func<bool> bossFight,
        Func<string, double, double, bool, double, EnemyTarget?> spawn)
    {
        Context = context;
        Hero = hero;
        Dungeon = dungeon;
        _inCombat = inCombat;
        _bossFight = bossFight;
        _spawn = spawn;
    }

    public SimulationContext Context { get; }

    public Hero Hero { get; }

    public DungeonProfile Dungeon { get; }

    /// <summary>True while an enemy is in combat.</summary>
    public bool InCombat => _inCombat();

    /// <summary>True while a boss is in combat.</summary>
    public bool BossFight => _bossFight();

    /// <summary>Raised as the party enters combat or leaves it (<see cref="InCombat"/>).</summary>
    public event Action<SimulationContext>? CombatChanged;

    /// <summary>Raised as a boss fight begins or ends (<see cref="BossFight"/>).</summary>
    public event Action<SimulationContext>? BossChanged;

    internal void RaiseBossChanged() => BossChanged?.Invoke(Context);

    /// <summary>Raised as an enemy enters combat, an enemy of the dungeon's text or one an affix spawned.</summary>
    public event Action<SimulationContext, EnemyTarget>? EnemyEntered;

    /// <summary>
    /// An enemy <paramref name="name"/> enters combat now in the run in progress, with <paramref name="hitPoints"/> before the
    /// difficulty's scale and <paramref name="spirit"/> Spirit Points, and dies <paramref name="seconds"/> later. Null when no
    /// run is in progress.
    /// </summary>
    public EnemyTarget? Spawn(string name, double hitPoints, double seconds, bool priority, double spirit) =>
        _spawn(name, hitPoints, seconds, priority, spirit);

    /// <summary>Raised as the fight ends: an affix undoes what it did to the hero, who fights the next fight.</summary>
    public event Action<SimulationContext>? Ended;

    internal void RaiseEnded() => Ended?.Invoke(Context);

    internal void RaiseCombatChanged() => CombatChanged?.Invoke(Context);

    internal void RaiseEnemyEntered(EnemyTarget enemy) => EnemyEntered?.Invoke(Context, enemy);
}
