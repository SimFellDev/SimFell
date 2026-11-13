using FellowshipSim.Core.Domain;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// How the Gunde tests read Rend: from its ticks, once they are all out, never from what the enemy still has to deal. "The Rend applied"
/// is what the ticks of a fight add up to, and the Rend one ability applies is the difference of two fights that differ by that cast.
/// </summary>
public static class GundeRend
{
    /// <summary>Lets every Rend tick out, and returns all the Rend the enemy (the fight's, or <paramref name="enemy"/>) took.</summary>
    public static double Dealt(StepDriver f, EnemyTarget? enemy = null)
    {
        f.Wait(40); // every tick
        return f.Damage("Rend").Where(tick => tick.Target == (enemy ?? f.Enemy).Name).Sum(tick => tick.Value);
    }

    /// <summary>All the Rend a Gunde's fight applies (without damage spread, with the <paramref name="talents"/>), after <paramref name="casts"/>.</summary>
    public static double Of(Action<StepDriver> casts, int[]? talents = null)
    {
        var f = StepFights.Gunde(talents: talents).WithoutDamageSpread();
        casts(f);
        return Dealt(f);
    }

    /// <summary>
    /// The Rend applied by <paramref name="ability"/> alone: a fight in which <paramref name="before"/> runs, then once more with the
    /// ability cast, and the difference of their Rend. The fight with the ability is returned in <paramref name="withAbility"/>.
    /// </summary>
    public static double Added(Action<StepDriver> before, Action<StepDriver> ability, out StepDriver withAbility, int[]? talents = null)
    {
        double rendBefore = Of(before, talents);

        withAbility = StepFights.Gunde(talents: talents).WithoutDamageSpread();
        before(withAbility);
        ability(withAbility);
        return Dealt(withAbility) - rendBefore;
    }
}
