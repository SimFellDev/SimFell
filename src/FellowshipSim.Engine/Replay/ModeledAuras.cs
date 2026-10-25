namespace FellowshipSim.Engine.Replay;

/// <summary>
/// The auras the engine models for a hero, which a replay compares with the logged buffs: every aura declared as a
/// <c>public static readonly AuraDef</c> on the hero and on everything it has (abilities, talents, gems, glyphs,
/// weapon, augmentations, legendaries, relic passive), plus the auras it built at runtime (<see cref="Hero.RuntimeAuras"/>).
/// </summary>
public static class ModeledAuras
{
    public static HashSet<int> LogIds(Hero hero) => Defs(hero).Keys.ToHashSet();

    /// <summary>The first modeled aura with each log ID.</summary>
    public static Dictionary<int, AuraDef> Defs(Hero hero)
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
        // Passive auras are left out: they are on all fight in the engine and logs do not report them as buffs.
        var passive = hero.PassiveAuras.Select(a => a.LogId).ToHashSet();
        var defs = new Dictionary<int, AuraDef>();
        foreach (var def in declared.Concat(hero.RuntimeAuras))
        {
            if (def.LogId > 0 && !passive.Contains(def.LogId)) defs.TryAdd(def.LogId, def);
        }
        return defs;
    }
}
