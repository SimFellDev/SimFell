namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The auras the engine models for a hero, which a replay compares with the logged buffs: every aura declared as a
/// <c>public static readonly AuraDef</c> on the hero and on everything it has (abilities, talents, gems, glyphs,
/// weapon, augmentations, legendaries, relic passive), plus the auras it built at runtime (<see cref="Hero.RuntimeAuras"/>).
/// </summary>
public static class ModeledAuras
{
    public static HashSet<int> LogIds(Hero hero) => Defs(hero).Select(a => a.LogId).ToHashSet();

    /// <summary>The modeled auras with a log ID, but the passive ones (on all fight in the engine, never logged as buffs).</summary>
    public static IEnumerable<AuraDef> Defs(Hero hero)
    {
        var owners = new List<object> { hero };
        owners.AddRange(hero.Abilities);
        owners.AddRange(hero.Talents);
        owners.AddRange(hero.Gems);
        owners.AddRange(hero.Glyphs);
        if (hero.EquippedWeapon != null) owners.Add(hero.EquippedWeapon);
        owners.AddRange(hero.Augmentations);
        owners.AddRange(hero.Legendaries);
        owners.AddRange(hero.RelicPassives);
        var declared = AuraDef.DeclaredOn(owners.Select(o => o.GetType()));
        var passive = hero.PassiveAuras.Select(a => a.LogId).ToHashSet();
        return declared.Concat(hero.RuntimeAuras).Where(a => a.LogId > 0 && !passive.Contains(a.LogId));
    }
}
