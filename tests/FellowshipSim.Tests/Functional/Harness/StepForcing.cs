using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Extension methods that control a fight's random outcomes. They are the only way a test does this:
///
/// <list type="bullet">
/// <item><c>f.ForceProcs(key => ...)</c>: procs.</item>
/// <item><c>f.Cast&lt;T&gt;(crit: true)</c>: crits.</item>
/// <item><c>f.WithoutDamageSpread()</c>: the +/-10% damage spread.</item>
/// </list>
///
/// Each one installs a decorator around <see cref="SimulationContext.Outcomes"/> (see <see cref="OutcomeSourceDecorator"/>), effective from
/// that moment to the end of the fight, so call it before the action it should affect. The order of installation does not matter: each
/// decorator controls a different call. Forced crits share one <see cref="ForcedCrits"/> per fight, created on first use.
/// </summary>
public static class StepForcing
{
    /// <summary>Decides the procs <paramref name="force"/> answers (true or false) for the rest of the fight; the dice decide the others.
    /// The returned <see cref="ForcedProcs.Rolls"/> records every proc's chance, which is how proc chances are tested.</summary>
    public static ForcedProcs ForceProcs(this StepDriver driver, Func<string, bool?> force)
    {
        var procs = new ForcedProcs(driver.Context.Outcomes, force);
        driver.Context.Outcomes = procs;
        return procs;
    }

    /// <summary>
    /// Every hit deals its damage with no +/-10% spread for the rest of the fight, unlike the game and the sims, so a damage test can check exact values.
    /// Crits still roll: a test of exact damage pins crit to 0 or forces it.
    /// </summary>
    public static StepDriver WithoutDamageSpread(this StepDriver driver)
    {
        driver.Context.Outcomes = new NoDamageSpread(driver.Context.Outcomes);
        return driver;
    }

    /// <summary>
    /// Casts <typeparamref name="T"/>, with its hits' crits decided by the test (see <see cref="ForcedCrits"/>):
    /// <c>f.Cast&lt;FrostBolt&gt;(crit: true)</c> crits every hit, <c>crit: false</c> none.
    /// </summary>
    public static void Cast<T>(this StepDriver driver, bool? crit = null, EnemyTarget? target = null) where T : Ability =>
        driver.Cast(driver.Ability<T>(), crit, target);

    /// <inheritdoc cref="Cast{T}(StepDriver, bool?, EnemyTarget?)"/>
    public static void Cast(this StepDriver driver, Ability ability, bool? crit, EnemyTarget? target = null)
    {
        if (crit is not { } forced)
        {
            driver.Cast(ability, target);
            return;
        }
        var crits = CritsOf(driver);
        crits.SetNext(ability, forced);
        try { driver.Cast(ability, target); }
        finally { crits.ClearNext(); }
    }

    /// <summary>The fight's <see cref="ForcedCrits"/>, installed on first use.</summary>
    private static ForcedCrits CritsOf(StepDriver driver)
    {
        if (driver.Crits is null)
        {
            driver.Crits = new ForcedCrits(driver.Context.Outcomes, driver.Hero);
            driver.Context.Outcomes = driver.Crits;
        }
        return driver.Crits;
    }
}
