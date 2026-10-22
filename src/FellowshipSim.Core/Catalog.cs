using FellowshipSim.Core.Customization.Blessings;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.Sets;
using FellowshipSim.Core.Customization.Traits;
using FellowshipSim.Core.Customization.Weapons;

namespace FellowshipSim.Core;

/// <summary>
/// Every hero, talent, trait, blessing, set, legendary and weapon the engine has, found by their class: adding a class
/// with its ID constant is all it takes for builds and combat logs to use it. Classes must have a constructor without
/// parameters. The constructors by ID are written at build time by FellowshipSim.Core.Generators' CatalogGenerator.
/// </summary>
public static partial class Catalog
{
    /// <summary>Hero classes by name (the class name, e.g. "Rime").</summary>
    public static IReadOnlyDictionary<string, Type> Heroes { get; } = HeroClasses.ToDictionary(h => h.Name, h => h.Type, StringComparer.OrdinalIgnoreCase);

    public static bool HasHero(string name) => Heroes.ContainsKey(name);

    public static Hero CreateHero(string name) =>
        NewHero(name) ?? throw new NotSupportedException($"{name} is not supported yet (supported: {string.Join(", ", Heroes.Keys)}).");

    public static Talent? CreateTalent(int id) => NewTalent(id);
    public static Trait? CreateTrait(int id, int rank) => NewTrait(id, rank);
    public static Blessing? CreateBlessing(int id, int rank) => NewBlessing(id, rank);
    public static Weapon? CreateWeapon(int weaponAbilityId) => NewWeapon(weaponAbilityId);

    /// <summary>
    /// A blessing's game ID from its name (0 if unknown). Combat logs give blessings per-item IDs, so they are matched by name.
    /// </summary>
    public static int BlessingIdByName(string name) => BlessingIdOf(name);

    /// <summary>The set a gear piece with this set-bonus ID belongs to (its class), or null.</summary>
    public static Type? SetOf(int setBonusId) => SetClass(setBonusId);

    public static SetBonus CreateSet(Type setType) =>
        NewSet(setType) ?? throw new ArgumentException($"{setType.Name} is not a set.", nameof(setType));

    public static Legendary? CreateLegendary(string hero, int itemId) => NewLegendary(hero.ToLowerInvariant(), itemId);

    /// <summary>The trait, blessing, weapon, set and legendary classes <paramref name="hero"/> can have.</summary>
    public static IEnumerable<Type> GearTypes(string hero) =>
        TraitClasses.Concat(BlessingClasses).Concat(WeaponClasses).Concat(SetClasses)
            .Concat(LegendaryClasses.Where(l => l.Hero == hero.ToLowerInvariant()).Select(l => l.Type))
            .Distinct();
}
