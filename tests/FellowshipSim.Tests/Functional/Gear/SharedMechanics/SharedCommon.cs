using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearShared;

/// <summary>What the shared-mechanics tests have in common: Spirit Points, the abilities that roll a Spirit Refund, reading auras.</summary>
internal static class SharedFights
{
    /// <summary>Spirit Points one Spirit Refund grants: 2 for Rime, 1 for the others (hero data SpiritProc FlatSpiritPointGain).</summary>
    public static double SpiritPointsPerRefund(GearHero hero) => hero.Name == "Rime" ? 2 : 1;

    public static void SharedSetSpirit(this StepDriver f, double points) => f.Hero.Resources.Get(Hero.SpiritPoints).Current = points;

    public static double SharedSpirit(this StepDriver f) => f.Hero.ResourceAmount(Hero.SpiritPoints);

    public static int SharedStacks(this StepDriver f, AuraDef aura) => f.Hero.Auras.GetStacks(aura, f.Now);

    /// <summary>
    /// A hero with every resource full and no Spirit Points. Its Power ability is the one that rolls the Spirit Refund: it spends the
    /// hero's refund resource (Winter Orbs, Cinders, Focus, Combo Points, Fury), and for Gunde it applies Rend.
    /// </summary>
    public static StepDriver Ready(GearHero hero, GearLoadout? loadout = null)
    {
        var f = hero.Fight(loadout);
        GearFights.Fill(f);
        f.SharedSetSpirit(0);
        return f;
    }

    public static void SharedCastRefund(this StepDriver f, GearHero hero) => f.Cast(hero.Get(f, hero.Power));

    public static IEnumerable<object[]> Heroes => GearHero.All;

    /// <summary>The heroes whose Spirit Refund gives back a spent resource (Gunde's comes from applying Rend).</summary>
    public static IEnumerable<object[]> HeroesWithASpender => GearHero.All.Where(h => ((GearHero)h[0]).Name != "Gunde");
}
