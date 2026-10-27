using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Mara;

namespace FellowshipSim.Tests.Functional;

/// <summary>What the Mara tests share: setting her resources and reading her fight's events.</summary>
internal static class MaraFights
{
    public static void SetEnergy(this StepDriver f, double energy) => f.Hero.Resources.Get(Mara.Energy).Current = energy;

    public static void SetComboPoints(this StepDriver f, double points) => f.Hero.Resources.Get(Mara.ComboPoints).Current = points;

    public static double ComboPoints(this StepDriver f) => f.Hero.ResourceAmount(Mara.ComboPoints);

    /// <summary>Gives Mara Spirit Points for her Spirit ability, Matriarch Macabre.</summary>
    public static void GiveMaraSpirit(this StepDriver f) => f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;

    /// <summary>The amounts of a resource spent, in order.</summary>
    public static List<double> Spent(this StepDriver f, string resource) =>
        f.Events.Where(e => e.Kind == StepEventKind.ResourceSpent && e.Name == resource).Select(e => e.Value).ToList();

    /// <summary>The amounts of a resource gained, in order.</summary>
    public static List<double> Gained(this StepDriver f, string resource) =>
        f.Events.Where(e => e.Kind == StepEventKind.ResourceGained && e.Name == resource).Select(e => e.Value).ToList();

    /// <summary>The damage events of an effect (a tick of a poison or a bleed) by effect ID.</summary>
    public static List<StepEvent> EffectTicks(this StepDriver f, int effectId) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId == LogIds.Effect(effectId)).ToList();

    /// <summary>The first damage event of an ability: its value is the unit abilities are compared in.</summary>
    public static double FirstHit(this StepDriver f, string ability) => f.Damage(ability).First().Value;

    /// <summary>Lets time pass in 10ms steps, so rates that change are read again.</summary>
    public static void WaitReading(this StepDriver f, double seconds)
    {
        var end = f.Now + SimTime.FromSeconds(seconds);
        while (f.Now < end) f.Wait(0.01);
    }
}
