using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Tariq;
using FellowshipSim.Core.Heroes.Tariq.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>What the Tariq tests share: setting and reading his Fury, putting an enemy in execute range, and the fight's events.</summary>
internal static class TariqFights
{
    public static void SetFury(this StepDriver f, double fury) => f.Hero.Resources.Get(Tariq.Fury).Current = fury;

    public static double Fury(this StepDriver f) => f.Hero.ResourceAmount(Tariq.Fury);

    /// <summary>Gives Tariq Spirit Points for his Spirit ability, Raging Tempest.</summary>
    public static void GiveTariqSpirit(this StepDriver f) => f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;

    /// <summary>Puts the enemy at <paramref name="fraction"/> of its health (Culling Strike's Execution works at 30% or less).</summary>
    public static void SetHealth(this StepDriver f, double fraction, EnemyTarget? enemy = null)
    {
        enemy ??= f.Enemy;
        enemy.CurrentHealth = enemy.MaxHealth * fraction;
    }

    /// <summary>The amounts of a resource gained, in order.</summary>
    public static List<double> FuryGained(this StepDriver f, string resource) =>
        f.Events.Where(e => e.Kind == StepEventKind.ResourceGained && e.Name == resource).Select(e => e.Value).ToList();

    /// <summary>The amounts of a resource spent, in order.</summary>
    public static List<double> FurySpent(this StepDriver f, string resource) =>
        f.Events.Where(e => e.Kind == StepEventKind.ResourceSpent && e.Name == resource).Select(e => e.Value).ToList();

    /// <summary>The damage events of an effect by effect ID.</summary>
    public static List<StepEvent> TariqEffectHits(this StepDriver f, int effectId) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId == LogIds.Effect(effectId)).ToList();

    /// <summary>The first damage event of an ability: its value is the unit abilities are compared in.</summary>
    public static double FirstDamageOf(this StepDriver f, string ability) => f.Damage(ability).First().Value;

    /// <summary>Runs <paramref name="act"/> on the fight and returns it, to build a reference fight in one expression.</summary>
    public static StepDriver Do(this StepDriver f, Action<StepDriver> act)
    {
        act(f);
        return f;
    }

    public static int Stacks(this StepDriver f, AuraDef aura) => f.Hero.Auras.GetStacks(aura, f.Now);

    /// <summary>A Wild Swing hit on a single enemy with nothing else on: 129.14% of Strength, the unit hits are compared in.</summary>
    public static double StrengthUnit()
    {
        var f = StepFights.Tariq();
        f.Cast<WildSwing>();
        return f.FirstDamageOf("Wild Swing") / 1.2914;
    }

    /// <summary>
    /// The crit damage multiplier the engine uses: a crit hit is worth this many normal ones. A hit that has a chance to crit is worth
    /// 1 + chance x (multiplier - 1) in the sim's expected-value mode, so hits with their own crit chance are divided by that.
    /// </summary>
    public static double CritMultiplier()
    {
        double Hit(double crit)
        {
            var f = StepFights.Tariq(crit: crit);
            f.Cast<FaceBreaker>();
            return f.FirstDamageOf("Face Breaker");
        }
        return 1 + (Hit(0.5) / Hit(0) - 1) / 0.5;
    }

    /// <summary>The value of a hit divided by what its crit chance is worth in expected value (so that a hit with its own crit chance compares as a plain one).</summary>
    public static double WithoutCrit(this StepEvent hit, double critMultiplier) => hit.Value / (1 + hit.CritChance * (critMultiplier - 1));

    /// <summary>Lets time pass in 10ms steps, so rates that change are read again.</summary>
    public static void WaitTicking(this StepDriver f, double seconds)
    {
        var end = f.Now + SimTime.FromSeconds(seconds);
        while (f.Now < end) f.Wait(0.01);
    }
}
