using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;

namespace FellowshipSim.Tests.Functional.GearRelic;

/// <summary>What the relic tests share: the relic items, and reading a hero's stats and unit hit.</summary>
internal static class RelicFights
{
    /// <summary>Game item IDs of the relic orbs.</summary>
    public const int OrbOfInnerSeas = 5926;       // Accursed Trophy
    public const int OrbOfAwakenedGaze = 5925;    // Dragonbone Dice
    public const int OrbOfGatheringStorms = 5924; // Undying Heart
    public const int OrbOfSanguineRestoration = 5919; // Gilded Broach

    public static GearLoadout With(int orb, double haste = 0, double crit = 0) => new() { Relics = [orb], Haste = haste, Crit = crit };

    /// <summary>The ability whose hits can crit: the unit ability, except for Tariq (Wild Swing never crits; his Core ability, Heavy Strike, does).</summary>
    public static Type CritAbility(GearHero hero) => hero.Name == "Tariq" ? hero.Core : hero.Unit;

    /// <summary>The hero's Main Stat now.</summary>
    public static double MainStat(this StepDriver f) => f.Hero.PrimaryAttribute(f.Now);

    /// <summary>The damage of the first hit of the hero's unit ability on a fresh fight with <paramref name="loadout"/>.</summary>
    public static double UnitHit(GearHero hero, GearLoadout loadout)
    {
        var f = hero.Fight(loadout);
        hero.CastProbe(f, hero.Unit);
        return f.Events.First(e => e.Kind == StepEventKind.Damage && e.Value > 0).Value;
    }

    /// <summary>When an aura was first gained, in seconds.</summary>
    public static double GainedAt(this StepDriver f, string aura) =>
        f.Events.First(e => e.Kind == StepEventKind.AuraGained && e.Name == aura).Time.TotalSeconds;

    public static bool Has(this StepDriver f, AuraDef aura) => f.Hero.Auras.HasAura(aura, f.Now);
}
