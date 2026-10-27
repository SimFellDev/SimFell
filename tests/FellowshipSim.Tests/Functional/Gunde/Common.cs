using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Gunde;
using FellowshipSim.Core.Heroes.Gunde.Abilities;

namespace FellowshipSim.Tests.Functional;

/// <summary>What the Gunde tests share: reading his Rend and his Owed in Blood stacks, and the fight's events.</summary>
internal static class GundeFights
{
    /// <summary>The damage the enemy's Rend still has to deal.</summary>
    public static double RendLeft(this StepDriver f, EnemyTarget? enemy = null) => f.GundeOf().Rend.Remaining(enemy ?? f.Enemy);

    /// <summary>Sets Gunde's stacks of Owed in Blood (no Blood Feather is picked up for them). The engine starts a fight with one stack, see the resource tests.</summary>
    public static void SetOwedInBlood(this StepDriver f, int stacks)
    {
        if (stacks == 0) f.Hero.Auras.Remove(OwedInBlood.Buff, f.Now);
        else f.Hero.Auras.Apply(OwedInBlood.Buff, f.Now, setStacks: stacks);
    }

    public static int OwedInBloodStacks(this StepDriver f) => f.Hero.Auras.GetStacks(OwedInBlood.Buff, f.Now);

    /// <summary>Gives Gunde Spirit Points for his Spirit ability, Bloodbound Spirit.</summary>
    public static void GiveGundeSpirit(this StepDriver f) => f.Hero.Resources.Get(Hero.SpiritPoints).Current = 100;

    /// <summary>The damage events of an effect (a tick of Rend or Slaughter) by effect ID.</summary>
    public static List<StepEvent> DamageOfEffect(this StepDriver f, int effectId) =>
        f.Events.Where(e => e.Kind == StepEventKind.Damage && e.AbilityId == LogIds.Effect(effectId)).ToList();

    /// <summary>The first damage event of an ability: its value is the unit abilities are compared in.</summary>
    public static double FirstHitOf(this StepDriver f, string ability) => f.Damage(ability).First().Value;

    /// <summary>A swing of the auto attack on a single enemy with nothing else on: 30% of Strength, the unit hits are compared in.</summary>
    public static double StrengthUnit()
    {
        var f = StepFights.Gunde();
        f.Cast<Attack>();
        return f.FirstHitOf("Attack") / 0.3;
    }

    /// <summary>Lets time pass in 10ms steps, so rates that change are read again.</summary>
    public static void WaitInSteps(this StepDriver f, double seconds)
    {
        var end = f.Now + SimTime.FromSeconds(seconds);
        while (f.Now < end) f.Wait(0.01);
    }
}
