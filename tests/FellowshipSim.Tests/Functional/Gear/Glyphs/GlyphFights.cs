using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
using FellowshipSim.Core.Heroes.Ardeos.Abilities;

namespace FellowshipSim.Tests.Functional.GearGlyph;

/// <summary>What the glyph tests share: the loadouts, the probes (one cast of a category's ability and its first hit) and the rank sets.</summary>
public static class GlyphFights
{
    // The glyphs' game IDs, as literals (the glyphs' tooltips and the combat logs).
    public const int Trickster = 700, Sinister = 701, Wayfarer = 703, Subduer = 704, Monarch = 705, Mystic = 706, Philosopher = 709, Vehement = 712;

    public static GearHero Hero(string name) => GearFights.Heroes.First(h => h.Name == name);

    /// <summary>Every hero's name (a theory takes names: the hero records are not serializable).</summary>
    public static IEnumerable<object[]> HeroNames => GearFights.Heroes.Select(h => new object[] { h.Name });

    /// <summary>Every hero's name with every glyph rank (1 to 4).</summary>
    public static IEnumerable<object[]> HeroNamesAndRanks => GearFights.Heroes.SelectMany(h => Enumerable.Range(1, 4).Select(r => new object[] { h.Name, r }));

    public static IEnumerable<object[]> Ranks => Enumerable.Range(1, 4).Select(r => new object[] { r });

    /// <summary>A hero with one glyph of the given rank (none when <paramref name="glyph"/> is 0), on <paramref name="enemies"/> enemies.</summary>
    public static StepDriver Fight(string hero, int glyph = 0, int rank = 1, double haste = 0, double crit = 0, double spirit = 0, int enemies = 1) =>
        Hero(hero).Fight(new GearLoadout
        {
            Glyphs = glyph == 0 ? [] : [(glyph, rank)],
            Haste = haste,
            Crit = crit,
            Spirit = spirit,
            Enemies = enemies,
        }).NoDice();

    /// <summary>A hero with several glyphs.</summary>
    public static StepDriver FightWith(string hero, (int Id, int Rank)[] glyphs, double haste = 0, double crit = 0, double spirit = 0, int enemies = 1) =>
        Hero(hero).Fight(new GearLoadout { Glyphs = glyphs, Haste = haste, Crit = crit, Spirit = spirit, Enemies = enemies }).NoDice();

    /// <summary>
    /// The dice decide nothing that a glyph test reads: Spirit Refund never fires and Tariq's Wild Swing is never avoided,
    /// so a test with Spirit reads the same fight every time.
    /// </summary>
    public static StepDriver NoDice(this StepDriver f)
    {
        f.ForceProcs(key => key is "spirit_refund" or "wild_swing_avoided" ? false : null);
        return f;
    }

    /// <summary>
    /// Casts <paramref name="ability"/> (after a second, not at the fight's first instant) and returns how long its cooldown lasts in real seconds: from
    /// the cast's completion to the end the engine predicts a second later, at the rate the hero has then.
    /// Dev: a cooldown started at time 0 reads its recovery rate only after its first progress, so its first segment runs at rate 1; that edge of the
    /// fight's first instant is left out by waiting.
    /// </summary>
    public static double CooldownLength(StepDriver f, Ability ability)
    {
        f.Wait(1);
        GearFights.Fill(f);
        f.Cast(ability);
        double started = f.Events.Last(e => e.Kind == StepEventKind.CastCompleted).Time.TotalSeconds;
        f.Wait(1);
        return f.Now.TotalSeconds - started + f.Hero.GetCooldownRemains(ability.Name, f.Now);
    }

    /// <summary>Gives the enemy this much health out of 100,000,000 (whole numbers, so 30,000,000 is exactly 30%).</summary>
    public static void SetEnemyHealth(this StepDriver f, double health, EnemyTarget? enemy = null) => (enemy ?? f.Enemy).CurrentHealth = health;

    /// <summary>
    /// Casts the hero's ability of a category and returns its first hit (the damage event named after the ability), after letting
    /// the cast's delayed hits land. Fills the resources of Power, Major and Spirit abilities; Ardeos's Detonate gets a burning enemy first.
    /// </summary>
    public static StepEvent? Hit(string hero, StepDriver f, Category category)
    {
        var h = Hero(hero);
        var type = category switch
        {
            Category.Basic => h.Basic,
            Category.Core => h.Core,
            Category.Power => h.Power,
            Category.Major => h.Major,
            Category.Spirit => h.Spirit,
            _ => h.Control!,
        };
        if (category is Category.Power or Category.Major or Category.Spirit) GearFights.Fill(f);
        if (type == typeof(Detonate)) f.Cast<SearingBlaze>();
        int before = f.Events.Count;
        f.Cast(h.Get(f, type));
        f.Wait(3);
        string name = h.Get(f, type).Name;
        foreach (var e in f.Events.Skip(before))
        {
            if (e.Kind == StepEventKind.Damage && e.Name == name) return e;
        }
        return null;
    }

    public enum Category { Basic, Core, Power, Major, Spirit, Control }
}
