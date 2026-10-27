using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>What the Ardeos tests share: reading the ticks of his damage over time effects off a fight's events.</summary>
internal static class ArdeosFights
{
    /// <summary>The ticks (damage events) of the effect with this effect ID, in order.</summary>
    public static List<StepEvent> Ticks(this StepDriver f, int effectId) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId == LogIds.Effect(effectId)).ToList();

    /// <summary>The direct hits of an ability (its damage events that are not a tick of an effect).</summary>
    public static List<StepEvent> Hits(this StepDriver f, string ability) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.Name == ability && e.AbilityId < 1_000_000).ToList();

    /// <summary>Gives Ardeos Spirit Points for his Spirit ability, Incinerate.</summary>
    public static void GiveSpiritAbility(this StepDriver f) => f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;
}
