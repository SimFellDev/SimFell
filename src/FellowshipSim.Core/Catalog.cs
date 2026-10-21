using System.Reflection;
using FellowshipSim.Core.Customization.Blessings;
using FellowshipSim.Core.Customization.Legendaries;
using FellowshipSim.Core.Customization.Sets;
using FellowshipSim.Core.Customization.Traits;
using FellowshipSim.Core.Customization.Weapons;

namespace FellowshipSim.Core;

/// <summary>
/// Every hero, talent, trait, blessing, set, legendary and weapon the engine has, found by their class: adding a class
/// with its ID constant is all it takes for builds and combat logs to use it. Classes must have a constructor without
/// parameters.
/// </summary>
public static class Catalog
{
    /// <summary>Hero classes by name (the class name, e.g. "Rime").</summary>
    public static IReadOnlyDictionary<string, Type> Heroes { get; } = Types<Hero>().ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<int, Type> TalentTypes = ById<Talent>(t => t.Id);
    private static readonly Dictionary<int, Type> TraitTypes = ById<Trait>(t => t.Id);
    private static readonly Dictionary<int, Type> BlessingTypes = ById<Blessing>(b => b.Id);
    private static readonly Dictionary<int, Type> WeaponTypes = ById<Weapon>(w => w.WeaponAbilityId);
    private static readonly Dictionary<int, Type> SetTypes = Types<SetBonus>()
        .SelectMany(t => Instance<SetBonus>(t).SetBonusIds.Select(id => (id, t)))
        .ToDictionary(x => x.id, x => x.t);
    private static readonly Dictionary<(string Hero, int ItemId), Type> LegendaryTypes = Types<Legendary>()
        .SelectMany(t => { var l = Instance<Legendary>(t); return l.ItemIds.Select(id => ((l.HeroName.ToLowerInvariant(), id), t)); })
        .ToDictionary(x => x.Item1, x => x.t);

    public static bool HasHero(string name) => Heroes.ContainsKey(name);

    public static Hero CreateHero(string name) =>
        Heroes.TryGetValue(name, out var type)
            ? (Hero)Activator.CreateInstance(type)!
            : throw new NotSupportedException($"{name} is not supported yet (supported: {string.Join(", ", Heroes.Keys)}).");

    public static Talent? CreateTalent(int id) => Create<Talent>(TalentTypes, id);
    public static Trait? CreateTrait(int id, int rank) => WithRank(Create<Trait>(TraitTypes, id), rank);
    public static Blessing? CreateBlessing(int id, int rank) => WithRank(Create<Blessing>(BlessingTypes, id), rank);
    public static Weapon? CreateWeapon(int weaponAbilityId) => Create<Weapon>(WeaponTypes, weaponAbilityId);

    /// <summary>
    /// A blessing's game ID from its name (0 if unknown). Combat logs give blessings per-item IDs, so they are matched by name.
    /// </summary>
    public static int BlessingIdByName(string name) =>
        BlessingTypes.FirstOrDefault(kv => Instance<Blessing>(kv.Value).Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Key;

    /// <summary>The set a gear piece with this set-bonus ID belongs to (its class), or null.</summary>
    public static Type? SetOf(int setBonusId) => SetTypes.GetValueOrDefault(setBonusId);

    public static SetBonus CreateSet(Type setType) => (SetBonus)Activator.CreateInstance(setType)!;

    public static Legendary? CreateLegendary(string hero, int itemId) =>
        LegendaryTypes.TryGetValue((hero.ToLowerInvariant(), itemId), out var type) ? (Legendary)Activator.CreateInstance(type)! : null;

    /// <summary>The trait, blessing, weapon, set and legendary classes <paramref name="hero"/> can have.</summary>
    public static IEnumerable<Type> GearTypes(string hero) =>
        TraitTypes.Values.Concat(BlessingTypes.Values).Concat(WeaponTypes.Values).Concat(SetTypes.Values)
            .Concat(LegendaryTypes.Where(kv => kv.Key.Hero == hero.ToLowerInvariant()).Select(kv => kv.Value))
            .Distinct();

    private static IEnumerable<Type> Types<T>() =>
        typeof(Catalog).Assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);

    private static T Instance<T>(Type type) => (T)Activator.CreateInstance(type)!;

    /// <summary>Classes of <typeparamref name="T"/> by their ID (an ID of 0 means unknown and is skipped).</summary>
    private static Dictionary<int, Type> ById<T>(Func<T, int> id)
    {
        var map = new Dictionary<int, Type>();
        foreach (var type in Types<T>())
        {
            int key = id(Instance<T>(type));
            if (key == 0) continue;
            if (!map.TryAdd(key, type)) throw new InvalidOperationException($"{type.Name} and {map[key].Name} share the ID {key}.");
        }
        return map;
    }

    private static T? Create<T>(Dictionary<int, Type> map, int id) where T : class =>
        map.TryGetValue(id, out var type) ? (T)Activator.CreateInstance(type)! : null;

    /// <summary>Sets the init-only Rank of a trait or blessing.</summary>
    private static T? WithRank<T>(T? item, int rank) where T : class
    {
        if (item != null) typeof(T).GetProperty("Rank", BindingFlags.Public | BindingFlags.Instance)?.SetValue(item, rank);
        return item;
    }
}
