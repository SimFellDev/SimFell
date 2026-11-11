using FellowshipSim.Core.Customization.Gems;
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

    /// <summary>Casts <paramref name="name"/> as soon as it is ready, waiting out exactly its cooldown: for tests that cast one ability again
    /// and again (twenty critical strikes of Tariq's Heavy Strike). The wait is read again until the ability is ready, since a cooldown
    /// can speed up meanwhile.</summary>
    public static void CastWhenReady(this StepDriver f, string name, bool? crit = null, EnemyTarget? target = null)
    {
        while (!f.IsReady(name)) f.Wait(Math.Max(f.Hero.GetCooldownRemains(name, f.Now), 0.001));
        f.Cast(name, crit, target);
    }

    /// <summary>
    /// Deals one hit of <paramref name="ability"/> to <paramref name="target"/> now, through the damage call the game's own source of that
    /// hit makes, with nothing cast: the hit and everything it triggers (a burn it leaves), and nothing else. Artificial, so only for a
    /// hit the game deals to an enemy it picks at random (a Fire Frog's leap), when a test needs exactly one such hit on one enemy.
    /// </summary>
    public static void HitWith(this StepDriver f, Ability ability, EnemyTarget target) => ability.DealDamage(f.Context, target);

    // ---- Enemies

    /// <summary>The fight's enemies, in order: <c>f.Enemies()[0]</c> is <c>f.Enemy</c>, the default target.</summary>
    public static IReadOnlyList<EnemyTarget> Enemies(this StepDriver f) => f.Context.AllEnemies;

    /// <summary>Defeats <paramref name="enemy"/>: it dies and leaves the fight. A shortcut for a kill, since the engine's enemies die on a
    /// schedule, never from damage.</summary>
    public static void Defeat(this StepDriver f, EnemyTarget enemy) => f.Context.RemoveEnemy(enemy, defeated: true);

    /// <summary>Sets <paramref name="on"/>'s health (the main target by default) to <paramref name="fraction"/> of its maximum: a set-up
    /// shortcut for a wounded enemy (e.g. <c>f.SetHealth(0.26)</c> for an execute range at 30%).</summary>
    public static void SetHealth(this StepDriver f, double fraction, EnemyTarget? on = null)
    {
        var enemy = on ?? f.Enemy;
        enemy.CurrentHealth = enemy.MaxHealth * fraction;
    }

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

    /// <summary>The amount of <paramref name="resource"/> now. Elarion's Focus is counted with what regenerated since the engine's last
    /// 0.1 second regeneration tick, as a cast would read it.</summary>
    public static double Amount(this StepDriver f, ResourceDef resource)
    {
        SettleRegeneration(f, resource);
        return f.Hero.ResourceAmount(resource);
    }

    /// <summary>Sets a resource directly: a set-up shortcut (e.g. 100 Spirit Points so Wrath of Winter can be cast). Elarion's Focus
    /// regenerated so far is counted first, so it is not added on top later.</summary>
    public static void Set(this StepDriver f, ResourceDef resource, double amount)
    {
        SettleRegeneration(f, resource);
        f.Hero.Resources.Get(resource).Current = amount;
    }

    /// <summary>Adds the regeneration the engine holds back between its ticks: Elarion's Focus, added every 0.1 seconds.</summary>
    private static void SettleRegeneration(StepDriver f, ResourceDef resource)
    {
        if (f.Hero is Core.Heroes.Elarion.Elarion elarion && resource == Core.Heroes.Elarion.Elarion.Focus) elarion.RegenerateFocus(f.Now);
    }

    /// <summary>The amount of the hero's resource called <paramref name="resource"/> in game ("Winter Orbs", "Fury"...), for tests over several heroes.</summary>
    public static double Amount(this StepDriver f, string resource) => f.Amount(f.Resource(resource));

    /// <summary>The hero's resource called <paramref name="name"/> in game. Throws, listing the hero's resources, if there is none.</summary>
    public static ResourceDef Resource(this StepDriver f, string name) =>
        f.Hero.ResourceDefs.FirstOrDefault(r => r.Name == name) ?? throw new ArgumentException(
            $"{f.Hero.Name} has no resource '{name}'. Resources: {string.Join(", ", f.Hero.ResourceDefs.Select(r => r.Name))}");

    /// <summary>Fills every resource of the hero, up to what the hero can hold (a gem can raise it): a set-up shortcut so any ability can
    /// be cast, in tests over several heroes.</summary>
    public static void FillResources(this StepDriver f)
    {
        foreach (var resource in f.Hero.ResourceDefs) f.Set(resource, f.Hero.Resources.Get(resource).Max);
    }

    public static double Haste(this StepDriver f) => f.Hero.GetHaste(f.Now);

    public static double Spirit(this StepDriver f) => f.Hero.SpiritPercent(f.Now);

    public static double Expertise(this StepDriver f) => f.Hero.Expertise(f.Now);

    /// <summary>The hero's main stat now, after every bonus (Rising Spirit, relics...).</summary>
    public static double MainStat(this StepDriver f) => f.Hero.PrimaryAttribute(f.Now);

    /// <summary>The hero's health: the sim keeps the hero at full health, from Stamina when no combat log gives it.</summary>
    public static double Health(this StepDriver f) => f.Hero.MaxHealth(f.Now);

    /// <summary>The power of the hero's gem of <paramref name="color"/>, after the relics that raise it (Covetous Greed).</summary>
    public static double GemPower(this StepDriver f, GemColor color) => f.Hero.Gems.Single(gem => gem.Color == color).Power;

    /// <summary>The hero's crit chance against the target now, as a fraction: what a hit would roll. Above 1, hits are grievous crits.</summary>
    public static double CritChance(this StepDriver f) => f.Hero.CritChanceAgainst(f.Enemy, f.Now);

    // ---- Cooldowns

    public static bool IsReady(this StepDriver f, string ability) => f.Hero.IsCooldownReady(ability, f.Now);

    public static bool IsReady<T>(this StepDriver f) where T : Ability => f.IsReady(f.Ability<T>().Name);

    public static int Charges(this StepDriver f, string ability) => f.Hero.CooldownOf(f.Ability(ability)).ChargesAt(f.Now);

    public static int Charges<T>(this StepDriver f) where T : Ability => f.Hero.CooldownOf(f.Ability<T>()).ChargesAt(f.Now);

    /// <summary>How fast <typeparamref name="T"/>'s cooldown accelerates now: 1 is real time, 2 twice as fast (every Cooldown
    /// Acceleration and hasted charges together).</summary>
    public static double AccelerationRate<T>(this StepDriver f) where T : Ability => f.Hero.CooldownRate(f.Ability<T>(), f.Now);

    /// <inheritdoc cref="AccelerationRate{T}"/>
    public static double AccelerationRate(this StepDriver f, string ability) => f.Hero.CooldownRate(f.Ability(ability), f.Now);

    /// <summary>Seconds of real time until the charge of <typeparamref name="T"/> now recharging comes back, at its current recovery rate; 0
    /// when no charge is recharging. Read between two moments to see a cooldown reduction as a step.</summary>
    public static double RechargeRemaining<T>(this StepDriver f) where T : Ability
    {
        var cooldown = f.Hero.CooldownOf(f.Ability<T>());
        return cooldown.ChargesAt(f.Now) >= cooldown.MaxCharges ? 0.0 : (cooldown.NextChargeAt - f.Now).TotalSeconds;
    }

    /// <summary>Makes every charge of <typeparamref name="T"/> ready now: a set-up shortcut (e.g. a second Starfall Volley while the first
    /// falls, as cooldown reductions would allow in the game).</summary>
    public static void ResetCooldown<T>(this StepDriver f) where T : Ability => f.Hero.CooldownOf(f.Ability<T>()).ResetAll();
}
