using System.Text.RegularExpressions;
using FellowshipSim.Core;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.APL.Editing;
using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Augmentations;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Customization.Sigils;
using FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Dungeons;
using FellowshipSim.Core.Import;
using FellowshipSim.Engine;

namespace FellowshipSim.LanguageServer;

/// <summary>
/// The completion list of a .simc file's profile lines. The line's part at the caret picks the items: a line's key, a pull's
/// name after <c>pull.</c>, a list key's names, a gear line's item and its options, <c>true</c> or <c>false</c>, and a pull
/// line's options, with the creatures after <c>name=</c> and the file's other pulls after <c>pull_with=</c> and
/// <c>chain_with=</c>.
/// </summary>
public static partial class SimcProfileEdits
{
    private const string StatsFromOldProfile = "stats_from_old_profile";
    private const string Talents = "talents";
    private const string Legendary = "legendary";
    private const string CreatureKey = "name";
    private const string Id = "id", Tempers = "tempers";

    private static readonly string[] Bools = ["true", "false"];

    [GeneratedRegex(@"[A-Za-z0-9_]*\z")]
    private static partial Regex PartBefore();

    /// <summary>
    /// The completion list at <paramref name="at"/>, or <paramref name="actions"/> on an action line, the list of the action
    /// list at the caret.
    /// </summary>
    public static CompletionList? Open(SimcFile file, string text, int at, bool always, CompletionList? actions)
    {
        at = Math.Min(at, text.Length);
        int lineStart = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
        string before = text[lineStart..at];
        string trimmed = before.TrimStart();
        if (trimmed.StartsWith('#') || (SimcProfile.IsActionLine(trimmed) && trimmed.Contains('='))) return actions;

        int line = text.AsSpan(0, lineStart).Count('\n');
        int eq = trimmed.IndexOf('=');
        if (eq < 0) return KeyList(file, text, at, trimmed, always, actions);
        if (!file.ProfileLines.Contains(line)) return actions;

        string key = trimmed[..eq].TrimEnd().TrimEnd('+');
        string value = trimmed[(eq + 1)..];
        int valueStart = at - value.Length;
        if (key.StartsWith(DungeonProfile.PullPrefix, StringComparison.Ordinal)) return PullList(file, text, at, valueStart, key, value, always);

        var items = ValueItems(file, key, value, out string partial);
        return items == null ? null : List(items, partial, text, at, always);
    }

    private static CompletionList? List(IReadOnlyList<Completion> items, string partial, string text, int at, bool always)
    {
        var found = ListEdits.Filter(items, partial, always);
        if (found == null || found.Count == 0) return null;
        var (from, to) = CodeEdits.PartAt(text, at);
        return new CompletionList(found.Take(CodeEdits.MaxItems).ToList(), from, to);
    }

    // Every key the file can take here, without those it has. Below a hero line an action is an action line, so of the
    // action list's items only its prefix stays.
    private static CompletionList? KeyList(SimcFile file, string text, int at, string part, bool always, CompletionList? actions)
    {
        if (part.StartsWith(DungeonProfile.PullPrefix, StringComparison.Ordinal))
        {
            return List(PullNames(file).Select(p => Item(p, AplTokenKind.List, $"{p}+=/")).ToList(), part[DungeonProfile.PullPrefix.Length..], text, at, always);
        }
        if (part.Contains('.')) return actions;

        var used = file.Profile.Split('\n').Select(l => SimProfile.KeyOf(l.Trim())).ToHashSet();
        bool endsAtEquals = text.AsSpan(at).TrimStart(" \t").StartsWith("=") || text.AsSpan(at).TrimStart(" \t").StartsWith("+=");
        Completion Key(string key, AplTokenKind kind) => Item(key, kind, endsAtEquals ? null : $"{key}=");

        var items = new List<Completion>();
        if (file.ProfileHero == null) items.AddRange(BuildHeroes.Names.Select(h => Item(AplNames.Slug(h), AplTokenKind.List, endsAtEquals ? null : $"{AplNames.Slug(h)}=\"", h)));
        else items.AddRange(SimcProfile.Keys.Where(k => !used.Contains(k)).Select(k => Key(k, AplTokenKind.Keyword)));
        items.AddRange(SimProfile.SettingNames.Where(k => !used.Contains(k)).Select(k => Key(k, AplTokenKind.Keyword)));
        items.AddRange(DungeonProfile.Keys.Where(k => !used.Contains(k)).Select(k => Key(k, AplTokenKind.Keyword)));
        items.Add(Item(DungeonProfile.PullPrefix[..^1], AplTokenKind.List, DungeonProfile.PullPrefix));

        var list = List(items, part, text, at, always);
        var actionItems = actions?.Items.Where(i => file.ProfileHero == null || i.Text == AplActionLine.ListPrefix).ToList() ?? [];
        if (list == null) return actionItems.Count > 0 ? actions! with { Items = actionItems } : null;
        return list with { Items = [.. actionItems, .. list.Items.Where(i => actionItems.All(a => a.Text != i.Text))] };
    }

    // The items of a key's value, and the part of it the items complete; null for a value with no list.
    private static IReadOnlyList<Completion>? ValueItems(SimcFile file, string key, string value, out string partial)
    {
        partial = PartBefore().Match(value).Value;
        if (SimProfile.BoolSettingNames.Contains(key) || key == StatsFromOldProfile) return Words(Bools);
        if (key == SimProfile.Dungeon) return Words([.. DefaultDungeons.Names, .. GameDungeons.All.Select(d => d.Name)]);

        if (NamesOf(key, file.ProfileHero) is { } named)
        {
            // A list's entries are in '/'s; a ':' in the entry at the caret starts its number.
            int slash = value.LastIndexOf('/');
            string entry = value[(slash + 1)..];
            if (entry.Contains(':')) return null;
            var listed = value.Split('/').Select(e => e.Split(':')[0].Trim()).ToHashSet();
            return named.Where(n => n.Text == entry || !listed.Contains(n.Text)).ToList();
        }

        if (GearLineItems(key, file.ProfileHero) is not { } gear) return null;
        return value.Contains(',') ? value[(value.LastIndexOf(',') + 1)..].Contains('=') ? null : [Option(Id), Option(Tempers)] : gear;
    }

    private static Completion Option(string key) => Item(key, AplTokenKind.Keyword, $"{key}=");

    private static List<Completion> Words(IEnumerable<string> words) => words.Select(w => Item(w, AplTokenKind.Keyword)).ToList();

    // The names of a list key: "name/name", with ":<number>" after each name of a ranked key.
    private static List<Completion>? NamesOf(string key, string? hero)
    {
        List<Completion> Ranked(IEnumerable<string> names) => names.Distinct().Select(n => Item(n, AplTokenKind.Named, $"{n}:")).ToList();
        IEnumerable<string> Slugs(IEnumerable<Augmentation> all) => all.Select(a => AplNames.Slug(a.Name));
        var colours = Enum.GetValues<GemColor>().Select(c => AplNames.Slug(c.ToString()));

        switch (key)
        {
            case Talents when hero != null:
                var tree = BuildHeroes.Create(hero).TalentTreeSlots.Select(s => BuildHeroes.TalentName(hero, s.TalentId)).OfType<string>();
                return tree.Select(n => Item(AplNames.Slug(n), AplTokenKind.Named, null, n)).DistinctBy(i => i.Text).ToList();
            case Talents:
                return null;
            case "aspects": return Ranked(Slugs(Catalog.AllAugmentations.OfType<Aspect>()));
            case "sigils": return Ranked(Slugs(Catalog.AllAugmentations.OfType<Sigil>()));
            case "protective_enchant": return Ranked(Slugs(Catalog.AllAugmentations.OfType<ProtectiveEnchant>()));
            case "glyphs": return Ranked(Catalog.AllGlyphs.Select(g => AplNames.Slug(g.Name)));
            case "gems" or "gear_gem_bonuses": return Ranked(colours);
            case "gem_sockets": return GemItem.All.Select(g => Item(AplNames.Slug(g.Name), AplTokenKind.Named, null, g.Name)).ToList();
            case "stat_enchants" or "stat_enchant_counts": return Ranked(StatEnchant.All.Select(e => e.Key));
            default: return null;
        }
    }

    // The items of a gear line: each writes its name and the options the line needs. Null for a key of no gear line.
    private static List<Completion>? GearLineItems(string key, string? hero)
    {
        if (key is "relic1" or "relic2")
        {
            return Catalog.AllRelicItems.Select(r => Item(AplNames.Slug(r.Name), AplTokenKind.Named, $"{AplNames.Slug(r.Name)},{Id}={r.Id}", r.Name)).ToList();
        }
        if (hero == null) return key == Legendary || GearSlots.All.Any(s => GearSlots.Key(s) == key) ? [] : null;
        if (key == Legendary)
        {
            return (GameHeroData.Legendaries.GetValueOrDefault(hero) ?? new Dictionary<int, string>())
                .Select(l => Item(AplNames.Slug(BuildHeroes.LegendaryName(hero, l.Key) ?? l.Value), AplTokenKind.Named, $"{AplNames.Slug(BuildHeroes.LegendaryName(hero, l.Key) ?? l.Value)},{Id}={l.Key}", l.Value))
                .ToList();
        }
        if (!GearSlots.All.Any(s => GearSlots.Key(s) == key)) return null;

        var slot = GearSlots.All.First(s => GearSlots.Key(s) == key);
        return GearItem.Of(hero).Where(i => i.Kind == GearSlots.Kind(slot)).Select(i =>
        {
            string name = slot == GearSlot.Weapon ? WeaponName(i) : AplNames.Slug(i.Name);
            return Item(name, AplTokenKind.Named, $"{name},{Id}={i.Id},{Tempers}=0", $"{i.Name}, item level {i.ItemLevel}");
        }).ToList();
    }

    // A weapon line has its weapon's name, or its ability's for a weapon the engine does not model.
    private static string WeaponName(GearItem item) =>
        AplNames.Slug(Catalog.CreateWeapon(item.WeaponAbilityId)?.Name ?? GameHeroData.WeaponAbilityNames.GetValueOrDefault(item.WeaponAbilityId) ?? item.Name);

    private static CompletionList? PullList(SimcFile file, string text, int at, int valueStart, string key, string value, bool always)
    {
        // The option at the caret starts after the line's '/' or its last ',' outside quotes.
        int start = value.StartsWith('/') ? 1 : 0;
        bool quoted = false;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '"') quoted = !quoted;
            else if (value[i] == ',' && !quoted) start = i + 1;
        }
        string option = value[start..];
        string[] options = value.TrimStart('/').Split(',');
        bool boss = options[0].TrimStart().StartsWith(DungeonProfile.BossPrefix, StringComparison.Ordinal);
        int eq = option.IndexOf('=');

        if (eq < 0)
        {
            var used = options.Select(o => o.Split('=')[0].Trim()).ToHashSet();
            var keys = DungeonProfile.GroupKeys.Where(k => !(boss && k == DungeonProfile.CountKey)).Concat(DungeonProfile.PullKeys)
                .Where(k => k == option.Trim() || !used.Contains(k)).Select(Option).ToList();
            if (start <= 1) keys.Add(Item($"{DungeonProfile.BossPrefix}1", AplTokenKind.Keyword));
            return List(keys, option.Trim(), text, at, always);
        }

        string name = option[..eq].Trim();
        string partial = option[(eq + 1)..];
        if (name is DungeonProfile.ChainWithKey or DungeonProfile.PullWithKey)
        {
            string self = key[DungeonProfile.PullPrefix.Length..];
            return List(PullNames(file).Where(p => p != self).Select(p => Item(p, AplTokenKind.List)).ToList(), partial, text, at, always);
        }
        return name == CreatureKey ? Creatures(partial, at, valueStart + start + eq + 1, always) : null;
    }

    // The creatures with the part in their names: quoted, as a name with a space is in the file's dungeons.
    private static CompletionList? Creatures(string partial, int at, int from, bool always)
    {
        string name = partial.TrimStart('"');
        if (name.Length == 0 && !always) return null;
        var found = GameCreatures.All.Select(c => c.Name).Where(n => n.Length > 0).Distinct()
            .Where(n => n.Contains(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.StartsWith(name, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(n => n, StringComparer.Ordinal)
            .Take(CodeEdits.MaxItems)
            .Select(n => Item(n, AplTokenKind.Named, n.Contains(' ') || n.Contains(',') ? $"\"{n}\"" : n))
            .ToList();
        return found.Count == 0 ? null : new CompletionList(found, from, at);
    }

    private static List<string> PullNames(SimcFile file) => file.Profile.Split('\n').Select(l => SimProfile.KeyOf(l.Trim()))
        .Where(k => k.StartsWith(DungeonProfile.PullPrefix, StringComparison.Ordinal) && k.Length > DungeonProfile.PullPrefix.Length)
        .Select(k => k[DungeonProfile.PullPrefix.Length..]).Distinct().ToList();

    private static Completion Item(string text, AplTokenKind kind, string? insert = null, string? detail = null)
    {
        string shown = detail is { } d && d != text ? d : "";
        return new Completion(text, new CompletionNode(kind) { Leaf = true, Detail = shown }, insert);
    }
}
