using FellowshipSim.Core.APL;
using FellowshipSim.Engine;
using FellowshipSim.UI.Services;

namespace FellowshipSim.UI.Apl;

/// <summary>
/// A visitor's saved action lists of the engine's heroes, the shared ones and the ones built into the engine: listing,
/// reading with the engine's parse, and saving the engine's text as one of the visitor's own lists.
/// </summary>
public sealed class SavedActionLists(ActionListStore store, ISimEngine engine, Visitor visitor)
{
    /// <summary>Every saved action list the visitor reads.</summary>
    public async Task<IReadOnlyList<ActionListEntry>> ListAsync(CancellationToken ct = default)
    {
        var heroes = await engine.GetHeroesAsync(ct);
        var saved = await store.ListAsync(visitor, heroes, ct);
        return saved.Concat(heroes.SelectMany(hero => DefaultActionLists.Names(hero).Select(name => new ActionListEntry(hero, name, BuiltIn: true))))
            .OrderBy(e => e.Hero).ThenBy(e => e.BuiltIn ? 0 : 1).ToList();
    }

    /// <summary>A saved action list's text and actions. An unknown hero or list throws <see cref="KeyNotFoundException"/>.</summary>
    public async Task<SavedActionList> ReadAsync(ActionListEntry entry, CancellationToken ct = default)
    {
        string known = await HeroNamed(entry.Hero, ct) ?? throw new KeyNotFoundException($"No hero named {entry.Hero}.");
        string text = (entry.BuiltIn ? DefaultActionLists.Read(known, entry.Name) : await store.ReadAsync(visitor, entry with { Hero = known }, ct))
                      ?? throw new KeyNotFoundException($"No action list named {entry.Name}.");
        try
        {
            return new SavedActionList(text, (await engine.ParseActionListAsync(text, ct)).Actions, null);
        }
        catch (SimEngineException e)
        {
            return new SavedActionList(text, null, e.Message);
        }
    }

    /// <summary>
    /// Saves the actions as text in one of the visitor's own lists and returns its entry. A name that cannot be a file
    /// name, or a field that does not parse, throws <see cref="ActionListNotSavedException"/>; an unknown hero throws
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    public async Task<ActionListEntry> SaveAsync(string hero, string name, IReadOnlyList<Core.APL.AplDocumentAction> actions, CancellationToken ct = default)
    {
        string known = await HeroNamed(hero, ct) ?? throw new KeyNotFoundException($"No hero named {hero}.");
        if (!ActionListStore.IsValidName(name))
        {
            throw new ActionListNotSavedException("Name the list with letters, digits, spaces, '_' or '-' (up to 64 characters).", []);
        }
        var written = await engine.WriteActionListAsync(actions, ct);
        if (written.Text is not { } text) throw new ActionListNotSavedException("Fix the marked fields first.", written.Errors);
        await store.SaveAsync(visitor, known, name, text, ct);
        return new ActionListEntry(known, name);
    }

    /// <summary>Removes one of the visitor's own lists. A shared or built-in list throws <see cref="InvalidOperationException"/>.</summary>
    public Task DeleteAsync(ActionListEntry entry, CancellationToken ct = default)
    {
        if (entry.Shared || entry.BuiltIn) throw new InvalidOperationException($"{entry.Name} is not one of your lists, so it stays.");
        return store.DeleteAsync(visitor, entry.Hero, entry.Name, ct);
    }

    /// <summary>The engine's name for <paramref name="hero"/>, or null when the engine has no such hero.</summary>
    private async Task<string?> HeroNamed(string hero, CancellationToken ct) =>
        (await engine.GetHeroesAsync(ct)).FirstOrDefault(h => h.Equals(hero, StringComparison.OrdinalIgnoreCase));
}
