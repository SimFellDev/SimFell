namespace FellowshipSim.Core.Engine;

public enum StepEventKind
{
    CastStarted,
    CastCompleted,
    CastCancelled,
    Damage,
    ResourceGained,
    ResourceSpent,
    ResourceOverflow,
    AuraGained,
    AuraRemoved,
}

/// <summary>One thing that happened in a <see cref="StepDriver"/> fight. <see cref="Value"/> is the damage dealt, the
/// resource amount, or the aura's stacks; 0 for casts. <see cref="Target"/> is the name of the enemy a damage event hit and <see cref="CritChance"/> the hit's chance to crit (0 for other events).</summary>
public readonly record struct StepEvent(SimTime Time, StepEventKind Kind, string Name, double Value = 0, string? Target = null, double CritChance = 0, int AbilityId = 0);

/// <summary>
/// Plays a fight with the actor controlled from code instead of an action list: <c>Cast</c> starts an ability and returns
/// once the hero is free to act again (cast and channel finished, global cooldown over), <c>Wait</c> lets time pass.
/// Built on the same engine commands as the Monte Carlo loop and <c>ReplayDriver</c>; it does not touch either.
/// </summary>
public sealed class StepDriver
{
    // Time moves in steps no smaller than this when waiting for the hero to be free (a channel's end event can lag its end time).
    private static readonly SimTime MinStep = new(1);

    private readonly List<StepEvent> _events = new();

    /// <param name="hero">A hero that is not in another fight; its state is reset.</param>
    public StepDriver(Hero hero, int seed = 1, int enemyCount = 1)
    {
        Hero = hero;
        Context = new SimulationContext(hero);
        Context.Rng.Seed(seed);
        hero.ResetForIteration();
        hero.BeginFight(Context);
        for (int i = 1; i <= enemyCount; i++) AddEnemy();
        Subscribe();
    }

    public Hero Hero { get; }
    public SimulationContext Context { get; }
    public SimTime Now => Context.CurrentTime;

    /// <summary>The enemy casts go to unless another is given: the first one added.</summary>
    public EnemyTarget Enemy => Context.AllEnemies[0];

    /// <summary>What happened so far, in order.</summary>
    public IReadOnlyList<StepEvent> Events => _events;

    /// <summary>True when the hero can start a cast now.</summary>
    public bool IsFree =>
        Hero.CurrentCast == null && Hero.CurrentChannel == null && Now >= Hero.GlobalCooldownEndTime;

    public EnemyTarget AddEnemy(double maxHealth = 100_000_000)
    {
        int n = Context.AllEnemies.Count + 1;
        return Context.AddEnemy(100 + n, n, $"Training Mob {n}", maxHealth);
    }

    public T Ability<T>() where T : Ability =>
        Hero.GetAbility<T>() ?? throw new InvalidOperationException($"{Hero.GetType().Name} has no ability {typeof(T).Name}");

    /// <summary>Casts <typeparamref name="T"/> and returns once the hero is free. Throws if the cast is not allowed.</summary>
    public void Cast<T>(EnemyTarget? target = null) where T : Ability => Cast(Ability<T>(), target);

    /// <summary>Casts <paramref name="ability"/> and returns once the hero is free. Throws if the cast is not allowed.</summary>
    public void Cast(Ability ability, EnemyTarget? target = null)
    {
        var attempt = TryCast(ability, target);
        if (!attempt.Succeeded) throw new InvalidOperationException($"Cannot cast {ability.Name} at {Now.TotalSeconds:F3}s: {attempt.Reason}");
    }

    public CastAttempt TryCast<T>(EnemyTarget? target = null) where T : Ability => TryCast(Ability<T>(), target);

    /// <summary>As <see cref="Cast(Ability, EnemyTarget?)"/>, but returns the engine's answer when the cast is not allowed
    /// (cooldown, resources, global cooldown...); time does not move then.</summary>
    public CastAttempt TryCast(Ability ability, EnemyTarget? target = null)
    {
        // TODO: raise a warning when the hero is not free here (a cast in step-by-step mode starts with a free hero).
        target ??= Enemy;
        Hero.CurrentTarget = target;
        var attempt = Context.BeginCast(Hero, ability, target);
        if (attempt.Succeeded) WaitUntilFree();
        return attempt;
    }

    /// <summary>Lets <paramref name="seconds"/> pass (cast and channel included).</summary>
    public void Wait(double seconds) => Context.RunUntil(Now + SimTime.FromSeconds(seconds));

    private void WaitUntilFree()
    {
        while (!IsFree)
        {
            var next = Hero.GlobalCooldownEndTime;
            if (Hero.CurrentCast is { } cast && cast.EndTime > next) next = cast.EndTime;
            if (Hero.CurrentChannel is { } channel && channel.Ticks.End > next) next = channel.Ticks.End;
            Context.RunUntil(next > Now ? next : Now + MinStep);
        }
    }

    private void Subscribe()
    {
        void Add(StepEventKind kind, string name, double value = 0, string? target = null, double critChance = 0, int abilityId = 0) =>
            _events.Add(new StepEvent(Now, kind, name, value, target, critChance, abilityId));

        Hero.OnCastStarted += (a, _, _) => Add(StepEventKind.CastStarted, a.Name);
        Hero.OnCastCompleted += (a, _, _) => Add(StepEventKind.CastCompleted, a.Name);
        Hero.OnCastCancelled += (a, _, _) => Add(StepEventKind.CastCancelled, a.Name);
        Hero.OnDamageDealt += (in DamageContext hit) => Add(StepEventKind.Damage, hit.AbilityName, hit.Amount, hit.Target?.Name, hit.CritChance, hit.AbilityId);
        Hero.OnResourceGained += (r, amount, _, _) => Add(StepEventKind.ResourceGained, r.Name, amount);
        Hero.OnResourceSpent += (r, amount, _) => Add(StepEventKind.ResourceSpent, r.Name, amount);
        Hero.OnResourceOverflow += (r, amount, _) => Add(StepEventKind.ResourceOverflow, r.Name, amount);
        Hero.Auras.OnGained += (aura, _, _) => Add(StepEventKind.AuraGained, aura.Def.Name, aura.Stacks);
        Hero.Auras.OnRemoved += (aura, _) => Add(StepEventKind.AuraRemoved, aura.Def.Name, aura.Stacks);
    }
}
