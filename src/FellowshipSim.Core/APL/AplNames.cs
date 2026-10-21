namespace FellowshipSim.Core.APL;

/// <summary>Action-list names of abilities and auras: lower case, words joined by '_' ("winters_blessing").</summary>
public static class AplNames
{
    /// <summary>The action-list name for <paramref name="name"/>: "Winter's Blessing" is "winters_blessing".</summary>
    public static string Slug(string name) => Slugs.GetOrAdd(name, n =>
        new string(n.ToLowerInvariant().Replace(' ', '_').Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()));

    // Speed: action lists look names up at every decision, and the set of names is small.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Slugs = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Keys = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Hero, Dictionary<string, Ability?>> Found = new();

    /// <summary>
    /// True when <paramref name="aplName"/> names <paramref name="ability"/>: its name or its class name, ignoring case,
    /// spaces, '_' and punctuation ("winters_blessing", "WintersBlessing" and "Winter's Blessing" are the same ability).
    /// </summary>
    public static bool NamesAbility(string aplName, Ability ability)
    {
        string key = Key(aplName);
        return Key(ability.Name) == key || Key(ability.GetType().Name) == key;
    }

    /// <summary>The ability of <paramref name="hero"/> that <paramref name="aplName"/> names, or null.</summary>
    public static Ability? FindAbility(Hero hero, string aplName)
    {
        // A hero's abilities do not change once it is built; each hero runs on one thread.
        var found = Found.GetValue(hero, _ => new Dictionary<string, Ability?>());
        if (!found.TryGetValue(aplName, out var ability))
        {
            ability = hero.Abilities.FirstOrDefault(a => NamesAbility(aplName, a));
            found[aplName] = ability;
        }
        return ability;
    }

    private static string Key(string name) => Keys.GetOrAdd(name, n => Slug(n).Replace("_", ""));
}
