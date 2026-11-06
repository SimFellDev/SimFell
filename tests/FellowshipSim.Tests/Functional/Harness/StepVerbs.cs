using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Read and change the hero's state at the fight's current time, and act: <c>f.Has(IceBlitz.Buff)</c>, <c>f.WaitUntil(19.9)</c>,
/// <c>f.Set(Hero.SpiritPoints, 100)</c>, <c>f.Cast("Frost Bolt")</c>.
///
/// They wrap the engine calls that need the current time or a definition object (<c>Hero.Auras.HasAura(aura, now)</c>), so tests do not
/// have to. Abilities are named by type when a test is about one ability (<c>f.Cast&lt;FrostBolt&gt;()</c>), and by game name when a rule
/// covers a list of them (<c>f.Cast("Frost Bolt")</c>). An unknown game name throws, listing the hero's abilities or resources.
///
/// <see cref="Set"/>, <see cref="Apply"/>, <see cref="Remove"/>, <see cref="FillResources"/> and <see cref="ResetCooldown{T}"/> change state directly, without the
/// cast or proc that would cause it in the game. Use them only to set up the starting state, never for the action under test.
/// </summary>
public static class StepVerbs
{
    // ---- Time

    /// <summary>The fight clock, in seconds.</summary>
    public static double Seconds(this StepDriver f) => f.Now.TotalSeconds;

    /// <summary>Lets time pass until <paramref name="seconds"/> on the fight clock. Throws if that time is already past.</summary>
    public static void WaitUntil(this StepDriver f, double seconds)
    {
        if (seconds < f.Seconds()) throw new InvalidOperationException($"WaitUntil({seconds:F3}s): the fight is already at {f.Seconds():F3}s");
        f.Wait(seconds - f.Seconds());
    }

    // ---- Abilities by game name

    /// <summary>The hero's ability called <paramref name="name"/> in game. Throws, listing the hero's abilities, if there is none.</summary>
    public static Ability Ability(this StepDriver f, string name) =>
        f.Hero.GetAbility(name) ?? throw new ArgumentException(
            $"{f.Hero.Name} has no ability '{name}'. Abilities: {string.Join(", ", f.Hero.Abilities.Select(a => a.Name))}");

    /// <summary>Casts the ability called <paramref name="name"/> in game; see <see cref="StepForcing"/> for <paramref name="crit"/>.</summary>
    public static void Cast(this StepDriver f, string name, bool? crit = null, EnemyTarget? target = null) =>
        f.Cast(f.Ability(name), crit, target);

    public static CastAttempt TryCast(this StepDriver f, string name, EnemyTarget? target = null) => f.TryCast(f.Ability(name), target);

    // ---- Enemies

    /// <summary>The fight's enemies, in order: <c>f.Enemies()[0]</c> is <c>f.Enemy</c>, the default target.</summary>
    public static IReadOnlyList<EnemyTarget> Enemies(this StepDriver f) => f.Context.AllEnemies;

    // ---- Auras

    /// <summary>Whether <paramref name="aura"/> is up on the hero, or on <paramref name="on"/> (e.g. <c>f.Enemy</c>).</summary>
    public static bool Has(this StepDriver f, AuraDef aura, Actor? on = null) => (on ?? f.Hero).Auras.HasAura(aura, f.Now);

    /// <summary>Whether the enemy <paramref name="on"/> (the main target by default) has a debuff of the hero's called <paramref name="debuff"/>
    /// in game ("Searing Blaze", "Shimmer"...): for the debuffs no <see cref="AuraDef"/> is declared for, such as Ardeos's damage over
    /// time effects. As with hit names, a misspelled name reads as false, so a test that expects it gone also sees it there.</summary>
    public static bool HasDebuff(this StepDriver f, string debuff, Actor? on = null) =>
        (on ?? f.Enemy).Auras.Active(f.Now).Any(aura => aura.Name == debuff);

    /// <summary>Puts <paramref name="aura"/> on the hero, or on <paramref name="on"/>, with <paramref name="stacks"/> stacks: a set-up shortcut
    /// (e.g. 4 Glacial Assault stacks without casting 4 Cold Snaps).</summary>
    public static void Apply(this StepDriver f, AuraDef aura, int stacks = 1, Actor? on = null) =>
        (on ?? f.Hero).Auras.Apply(aura, f.Now, setStacks: stacks);

    /// <summary>Takes <paramref name="aura"/> off the hero, or off <paramref name="on"/>: a set-up shortcut for an aura that would end with
    /// what gave it (e.g. Chronoshift's barrier as its channel ends).</summary>
    public static void Remove(this StepDriver f, AuraDef aura, Actor? on = null) => (on ?? f.Hero).Auras.Remove(aura, f.Now);

    /// <summary>The stacks of <paramref name="aura"/> on the hero, or on <paramref name="on"/>; 0 when it is not up.</summary>
    public static int Stacks(this StepDriver f, AuraDef aura, Actor? on = null) => (on ?? f.Hero).Auras.GetStacks(aura, f.Now);

    // ---- Resources and stats

    public static double Amount(this StepDriver f, ResourceDef resource) => f.Hero.ResourceAmount(resource);

    /// <summary>Sets a resource directly: a set-up shortcut (e.g. 100 Spirit Points so Wrath of Winter can be cast).</summary>
    public static void Set(this StepDriver f, ResourceDef resource, double amount) => f.Hero.Resources.Get(resource).Current = amount;

    /// <summary>The amount of the hero's resource called <paramref name="resource"/> in game ("Winter Orbs", "Fury"...), for tests over several heroes.</summary>
    public static double Amount(this StepDriver f, string resource) => f.Amount(f.Resource(resource));

    /// <summary>The hero's resource called <paramref name="name"/> in game. Throws, listing the hero's resources, if there is none.</summary>
    public static ResourceDef Resource(this StepDriver f, string name) =>
        f.Hero.ResourceDefs.FirstOrDefault(r => r.Name == name) ?? throw new ArgumentException(
            $"{f.Hero.Name} has no resource '{name}'. Resources: {string.Join(", ", f.Hero.ResourceDefs.Select(r => r.Name))}");

    /// <summary>Fills every resource of the hero: a set-up shortcut so any ability can be cast, in tests over several heroes.</summary>
    public static void FillResources(this StepDriver f)
    {
        foreach (var resource in f.Hero.ResourceDefs) f.Set(resource, resource.Max);
    }

    public static double Haste(this StepDriver f) => f.Hero.GetHaste(f.Now);

    public static double Spirit(this StepDriver f) => f.Hero.SpiritPercent(f.Now);

    /// <summary>The hero's crit chance against the target now, as a fraction: what a hit would roll. Above 1, hits are grievous crits.</summary>
    public static double CritChance(this StepDriver f) => f.Hero.CritChanceAgainst(f.Enemy, f.Now);

    // ---- Cooldowns

    public static bool IsReady(this StepDriver f, string ability) => f.Hero.IsCooldownReady(ability, f.Now);

    public static bool IsReady<T>(this StepDriver f) where T : Ability => f.IsReady(f.Ability<T>().Name);

    public static int Charges(this StepDriver f, string ability) => f.Hero.CooldownOf(f.Ability(ability)).ChargesAt(f.Now);

    public static int Charges<T>(this StepDriver f) where T : Ability => f.Hero.CooldownOf(f.Ability<T>()).ChargesAt(f.Now);

    /// <summary>How fast <typeparamref name="T"/>'s cooldown recovers now: 1 is real time, 2 twice as fast (cooldown recovery,
    /// acceleration and hasted charges together).</summary>
    public static double RecoveryRate<T>(this StepDriver f) where T : Ability => f.Hero.CooldownRate(f.Ability<T>(), f.Now);

    /// <summary>Makes every charge of <typeparamref name="T"/> ready now: a set-up shortcut (e.g. a second Starfall Volley while the first
    /// falls, as cooldown reductions would allow in the game).</summary>
    public static void ResetCooldown<T>(this StepDriver f) where T : Ability => f.Hero.CooldownOf(f.Ability<T>()).ResetAll();
}
