namespace FellowshipSim.Logs.Replay;

/// <summary>
/// The auras the engine models for a hero, which a replay compares with the logged buffs: every aura declared as a
/// <c>public static readonly AuraDef</c> on the hero and on everything it has (abilities, talents, gems, blessings,
/// weapon, traits, legendaries, sets), plus the auras it built at runtime (<see cref="Hero.RuntimeAuras"/>).
/// </summary>
public static class ModeledAuras
{
    public static HashSet<int> LogIds(Hero hero)
    {
        var owners = new List<object> { hero };
        owners.AddRange(hero.Abilities);
        owners.AddRange(hero.Talents);
        owners.AddRange(hero.Gems);
        owners.AddRange(hero.Blessings);
        if (hero.EquippedWeapon != null) owners.Add(hero.EquippedWeapon);
        owners.AddRange(hero.Traits);
        owners.AddRange(hero.Legendaries);
        owners.AddRange(hero.Sets);
        var declared = AuraDef.DeclaredOn(owners.Select(o => o.GetType()));
        // Passive auras are left out: they are on all fight in the engine and logs do not report them as buffs.
        return declared.Concat(hero.RuntimeAuras)
            .Select(a => a.LogId)
            .Where(id => id > 0)
            .Except(hero.PassiveAuras.Select(a => a.LogId))
            .ToHashSet();
    }
}
