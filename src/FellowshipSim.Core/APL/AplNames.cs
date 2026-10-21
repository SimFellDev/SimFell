namespace FellowshipSim.Core.APL;

/// <summary>Action-list names of abilities and auras: lower case, words joined by '_' ("winters_blessing").</summary>
public static class AplNames
{
    /// <summary>The action-list name for <paramref name="name"/>: "Winter's Blessing" is "winters_blessing".</summary>
    public static string Slug(string name) =>
        new string(name.ToLowerInvariant().Replace(' ', '_').Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

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
    public static Ability? FindAbility(Hero hero, string aplName) =>
        hero.Abilities.FirstOrDefault(a => NamesAbility(aplName, a));

    private static string Key(string name) => Slug(name).Replace("_", "");
}
