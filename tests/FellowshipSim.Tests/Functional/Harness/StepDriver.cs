using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

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

/// <summary>
/// One of the hero's events in a <see cref="StepDriver"/> fight, with the time it happened. <see cref="Name"/> is the game name of the
/// ability, hit, resource or aura.
///
/// <see cref="Value"/> is the damage dealt, the amount of resource gained, spent or lost, or the aura's stacks; 0 for casts. Damage events
/// also have <see cref="Target"/> (the enemy hit), <see cref="CritChance"/> (1 or 0 when the engine forces the crit) and <see cref="IsCrit"/>.
/// </summary>
public readonly record struct StepEvent(SimTime Time, StepEventKind Kind, string Name, double Value = 0, string? Target = null, double CritChance = 0, bool IsCrit = false);

/// <summary>
/// Runs a fight where the test, not an action list, decides what the hero casts.
///
/// <see cref="Cast(Ability, EnemyTarget?)"/> starts a cast through the engine call a sim uses (<see cref="SimulationContext.BeginCast"/>),
/// then runs the fight until the hero is free again (cast or channel over, global cooldown over). <see cref="Wait"/> runs the fight for a
/// given time with the hero idle. Between calls the fight is paused, so a test reads the state at an exact time.
///
/// Everything else is the real engine: abilities, talents, auras and periodic effects schedule their own events, and random outcomes go
/// through <see cref="SimulationContext.Outcomes"/>, which <see cref="StepForcing"/> can replace. The driver adds no game rule.
///
/// It records the hero's events in <see cref="Events"/>: casts, damage dealt, resources, and the hero's own auras. Auras on enemies are not
/// recorded; read them as state with <c>f.Has(aura, on: f.Enemy)</c>. Tests build a fight with <see cref="StepFights"/>, and use <see
/// cref="StepVerbs"/>, <see cref="StepQueries"/> and <see cref="StepForcing"/> rather than these members directly.
/// </summary>
public sealed class StepDriver
{
    // Time moves in steps no smaller than this when waiting for the hero to be free (a channel's end event can lag its end time).
    private static readonly SimTime MinStep = new(1);

    private readonly List<StepEvent> _events = new();

    /// <summary>Starts a fight at 0s, with the hero reset as at the start of a sim iteration, against <paramref name="enemyCount"/> training dummies.</summary>
    /// <param name="hero">A hero that is not in another fight; its state is reset.</param>
    /// <param name="seed">The seed of the fight's dice, for the outcomes a test does not force.</param>
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

    /// <summary>The default target of casts: the first enemy added.</summary>
    public EnemyTarget Enemy => Context.AllEnemies[0];

    /// <summary>The fight's forced crits, once <see cref="StepForcing"/> has installed them; null before.</summary>
    internal ForcedCrits? Crits { get; set; }

    /// <summary>The hero's events so far, in order.</summary>
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

    /// <summary>Runs the fight for <paramref name="seconds"/> with the hero idle: every event due in that time happens (ticks, landing
    /// hits, auras running out, resource regeneration).</summary>
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
        void Add(StepEventKind kind, string name, double value = 0, string? target = null, double critChance = 0, bool isCrit = false) =>
            _events.Add(new StepEvent(Now, kind, name, value, target, critChance, isCrit));

        Hero.OnCastStarted += (a, _, _) => Add(StepEventKind.CastStarted, a.Name);
        Hero.OnCastCompleted += (a, _, _) => Add(StepEventKind.CastCompleted, a.Name);
        Hero.OnCastCancelled += (a, _, _) => Add(StepEventKind.CastCancelled, a.Name);
        Hero.OnDamageDealt += (in DamageContext hit) => Add(StepEventKind.Damage, hit.AbilityName, hit.Amount, hit.Target?.Name, hit.CritChance, hit.IsCrit);
        Hero.OnResourceGained += (r, amount, _, _) => Add(StepEventKind.ResourceGained, r.Name, amount);
        Hero.OnResourceSpent += (r, amount, _) => Add(StepEventKind.ResourceSpent, r.Name, amount);
        Hero.OnResourceOverflow += (r, amount, _) => Add(StepEventKind.ResourceOverflow, r.Name, amount);
        Hero.Auras.OnGained += (aura, _, _) => Add(StepEventKind.AuraGained, aura.Def.Name, aura.Stacks);
        Hero.Auras.OnRemoved += (aura, _) => Add(StepEventKind.AuraRemoved, aura.Def.Name, aura.Stacks);
    }
}
