using System.Globalization;
using System.Text;
using FellowshipSim.Core.APL;
using FellowshipSim.Core.Customization.Aspects;
using FellowshipSim.Core.Customization.Augmentations;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Customization.StatEnchants;
using FellowshipSim.Core.Customization.ProtectiveEnchants;
using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Core.Import;

/// <summary>
/// A character profile in simc style: one <c>key=value</c> per line, names as action-list names ("winters_blessing").
/// Stats are the gear's raw ones: Main Stat, Stamina and ratings as the items list them, without the hero's base stats,
/// stat enchants (<c>stat_enchants</c> lists those), gem nodes, sigils or relics, which the engine adds; <c>gems</c> is
/// the sockets' and gear bonuses' gem power, without Covetous Greed. A profile written before gives the ratings as the
/// character sheet's percentages (<c>crit=30%</c>), and its Main Stat and gem power with their bonuses: it reads as it
/// is, each percentage as the rating that gives it, and is marked (<c>stats_from_old_profile=true</c>) as likely wrong
/// until its stats are edited.
/// <code>
/// rime="sww"
/// primary_stat=161
/// stamina=306
/// crit_rating=89
/// haste_rating=71
/// talents=avalanche/coalescing_frost/...
/// aspects=martial_initiative:2/...
/// sigils=hunters_focus:1/...
/// protective_enchant=the_oak:1
/// glyphs=the_monarch:1/...
/// gems=sapphire:1500/...
/// gem_sockets=splendid_amethyst/large_emerald/...
/// gear_gem_bonuses=sapphire:6/amethyst:5/...
/// relic1=orb_of_inner_seas,id=5926,tempers=8
/// relic2=orb_of_gathering_storms,id=5924
/// legendary=frostwyrms_spite,id=5899
/// weapon=ashas_chronoshift_spire,id=5984,tempers=8
/// head=harbinger_of_winter_helm,id=5899,tempers=0
/// ring1=beguiling_ring,id=5932,tempers=8
/// actions+=/frost_bolt
/// </code>
/// Lines starting with '#' are comments. <c>actions</c> lines are the profile's action list, with the comments directly
/// above them, and the comments right after an <c>actions</c> line that end at a blank line or at the end of the text. A
/// gear slot's line (<see cref="GearSlots.Key"/>) names the item worn there, its ID and its tempers; a relic or weapon
/// line with an <c>id</c> and <c>tempers</c> is its slot's item too (a weapon line's name is its weapon's).
/// </summary>
public sealed record SimcProfile(CharacterBuild Build, string ActionList)
{
    public static SimcProfile Load(string path) => Parse(File.ReadAllText(path));

    public static SimcProfile Parse(string text)
    {
        var build = new CharacterBuild();
        var actions = new StringBuilder();
        var percents = new HashSet<string>();
        // '#' lines right above an actions line, or after the last line when it is an actions line, are the action list's.
        var comments = new List<string>();
        bool afterActions = false;
        Hero? hero = null;
        int lineNumber = 0;
        foreach (string raw in text.Split('\n'))
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0)
            {
                // Comments after an actions line with a blank line under them are the action list's.
                if (afterActions)
                {
                    foreach (string comment in comments) actions.AppendLine(comment);
                    comments.Clear();
                }
                continue;
            }
            if (line.StartsWith('#'))
            {
                comments.Add(line);
                continue;
            }
            if (line.StartsWith("actions", StringComparison.Ordinal))
            {
                foreach (string comment in comments) actions.AppendLine(comment);
                comments.Clear();
                actions.AppendLine(line);
                afterActions = true;
                continue;
            }
            comments.Clear();
            afterActions = false;
            int eq = line.IndexOf('=');
            if (eq <= 0) throw Error(lineNumber, line, "expected key=value");
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            try
            {
                if (hero == null)
                {
                    var heroName = BuildHeroes.Names.FirstOrDefault(h => AplNames.Slug(h) == key)
                                   ?? throw new FormatException("the first line names the hero: <hero>=\"<name>\"");
                    build.Hero = heroName;
                    build.Name = value.Trim('"');
                    hero = BuildHeroes.Create(heroName);
                    continue;
                }
                Read(build, hero, key, value, percents);
            }
            catch (FormatException e)
            {
                throw Error(lineNumber, line, e.Message);
            }
        }
        if (afterActions)
        {
            foreach (string comment in comments) actions.AppendLine(comment);
        }
        if (hero == null) throw new FormatException("The profile has no hero line.");
        // The ratings read from percentages count the stat enchants; a build's ratings are without them.
        if (percents.Contains("crit")) build.CritRating = Math.Max(0, build.CritRating - build.EnchantRating(StatType.CritRating));
        if (percents.Contains("haste")) build.HasteRating = Math.Max(0, build.HasteRating - build.EnchantRating(StatType.HasteRating));
        if (percents.Contains("expertise")) build.ExpertiseRating = Math.Max(0, build.ExpertiseRating - build.EnchantRating(StatType.ExpertiseRating));
        if (percents.Contains("spirit")) build.SpiritRating = Math.Max(0, build.SpiritRating - build.EnchantRating(StatType.SpiritRating));
        if (percents.Count > 0) build.StatsFromOldProfile = true;
        return new SimcProfile(build, actions.ToString());
    }

    private static void Read(CharacterBuild build, Hero hero, string key, string value, HashSet<string> percents)
    {
        switch (key)
        {
            case "primary_stat": build.PrimaryStat = Number(value); break;
            case "stamina": build.Stamina = Number(value); break;
            case "armor": build.Armor = Number(value); break;
            case "crit_rating": build.CritRating = Number(value); break;
            case "haste_rating": build.HasteRating = Number(value); break;
            case "expertise_rating": build.ExpertiseRating = Number(value); break;
            case "spirit_rating": build.SpiritRating = Number(value); break;
            case "stats_from_old_profile": build.StatsFromOldProfile = value == "true"; break;
            case "crit":
                percents.Add("crit");
                build.CritRating = StatDiminishingReturns.ToRating(Percent(value) - StatBlock.BaseCritChance);
                break;
            case "haste":
                percents.Add("haste");
                build.HasteRating = StatDiminishingReturns.ToRating(Percent(value));
                break;
            case "expertise":
                percents.Add("expertise");
                build.ExpertiseRating = StatDiminishingReturns.ToRating(Percent(value));
                break;
            case "spirit":
                percents.Add("spirit");
                build.SpiritRating = StatDiminishingReturns.ToRating(Percent(value) - StatBlock.BaseSpirit);
                break;
            case "talents":
                var tree = hero.TalentTreeSlots.Select(s => (Name: BuildHeroes.TalentName(build.Hero, s.TalentId), Id: s.TalentId))
                    .Where(t => t.Name != null).Select(t => (t.Name!, t.Id));
                foreach (string name in List(value)) build.Talents.Add(IdOf(name, tree, "talent"));
                break;
            case "aspects": AddAugmentations<Aspect>(build, value, "aspect"); break;
            case "sigils": AddAugmentations<Sigil>(build, value, "sigil"); break;
            case "protective_enchant": AddAugmentations<ProtectiveEnchant>(build, value, "protective enchant"); break;
            case "glyphs":
                foreach (var (name, rank) in Ranked(value))
                {
                    build.Glyphs.Add(new BuildEntry(IdOf(name, Catalog.AllGlyphs.Select(g => (g.Name, g.Id)), "glyph"), rank));
                }
                break;
            case "gems":
                foreach (string entry in List(value))
                {
                    var (name, power) = Pair(entry);
                    var colors = Enum.GetValues<GemColor>().Where(c => AplNames.Slug(c.ToString()) == name).ToList();
                    if (colors.Count == 0) throw new FormatException($"no gem '{name}'");
                    build.GemPower[colors[0]] = power;
                }
                break;
            case "gem_sockets":
                foreach (string name in List(value))
                {
                    build.SocketedGemIds.Add((GemItem.All.FirstOrDefault(g => AplNames.Slug(g.Name) == name) ?? throw new FormatException($"no gem '{name}'")).Id);
                }
                break;
            case "gear_gem_bonuses":
                foreach (var (name, count) in Ranked(value))
                {
                    var colors = Enum.GetValues<GemColor>().Where(c => AplNames.Slug(c.ToString()) == name).ToList();
                    if (colors.Count == 0) throw new FormatException($"no gem colour '{name}'");
                    build.GearGemBonuses[colors[0]] = count;
                }
                break;
            case "stat_enchants":
                foreach (var (name, rating) in Ranked(value))
                {
                    var enchant = StatEnchant.All.FirstOrDefault(e => e.Key == name) ?? throw new FormatException($"no stat enchant '{name}'");
                    build.StatEnchants.Add(new BuildEntry(enchant.Id, rating));
                }
                break;
            case "stat_enchant_counts":
                foreach (var (name, count) in Ranked(value))
                {
                    var enchant = StatEnchant.All.FirstOrDefault(e => e.Key == name) ?? throw new FormatException($"no stat enchant '{name}'");
                    build.StatEnchantCounts[enchant.Id] = count;
                }
                break;
            case "relic1":
                build.Relic1ItemId = RelicItemId(value);
                AddWornWithTempers(build, GearSlot.Relic1, value);
                break;
            case "relic2":
                build.Relic2ItemId = RelicItemId(value);
                AddWornWithTempers(build, GearSlot.Relic2, value);
                break;
            case "legendary":
            {
                var options = Options(value);
                int itemId = (int)Number(options.GetValueOrDefault("id") ?? throw new FormatException("a legendary is <power>,id=<item id>"));
                string legendary = BuildHeroes.LegendaryName(build.Hero, itemId) ?? throw new FormatException($"no legendary item {itemId} for {build.Hero}");
                if (AplNames.Slug(legendary) != options[""]) throw new FormatException($"item {itemId} grants {AplNames.Slug(legendary)}");
                build.LegendaryItemIds.Add(itemId);
                break;
            }
            case "weapon":
            {
                // A weapon the engine does not model is named by its ability.
                string weapon = Options(value)[""];
                build.WeaponAbilityId = Catalog.AllWeapons.FirstOrDefault(w => AplNames.Slug(w.Name) == weapon)?.WeaponAbilityId
                                        ?? GameHeroData.WeaponAbilityNames.Where(w => AplNames.Slug(w.Value) == weapon).Select(w => (int?)w.Key).FirstOrDefault()
                                        ?? throw new FormatException($"no weapon '{weapon}'");
                AddWornWithTempers(build, GearSlot.Weapon, value);
                break;
            }
            default:
                if (GearSlots.All.FirstOrDefault(s => GearSlots.Key(s) == key) is var slot && GearSlots.Key(slot) == key)
                {
                    build.Gear.Add(Worn(build.Hero, slot, value));
                    break;
                }
                throw new FormatException($"unknown key '{key}'");
        }
    }

    /// <summary>The profile text of <see cref="Build"/>, then <see cref="ActionList"/>.</summary>
    public string Format()
    {
        var build = Build;
        var s = new StringBuilder();
        s.AppendLine($"{AplNames.Slug(build.Hero)}=\"{build.Name}\"");
        s.AppendLine($"primary_stat={Text(build.PrimaryStat)}");
        if (build.Stamina > 0) s.AppendLine($"stamina={Text(build.Stamina)}");
        if (build.Armor > 0) s.AppendLine($"armor={Text(build.Armor)}");
        s.AppendLine($"crit_rating={Text(build.CritRating)}");
        s.AppendLine($"haste_rating={Text(build.HasteRating)}");
        s.AppendLine($"expertise_rating={Text(build.ExpertiseRating)}");
        s.AppendLine($"spirit_rating={Text(build.SpiritRating)}");
        if (build.StatsFromOldProfile) s.AppendLine("stats_from_old_profile=true");
        if (build.Talents.Count > 0)
        {
            s.AppendLine($"talents={string.Join('/', build.Talents.Select(id => AplNames.Slug(BuildHeroes.TalentName(build.Hero, id) ?? throw new InvalidOperationException($"Talent {id} is not modeled"))))}");
        }
        var augmentations = build.Augmentations
            .Select(a => (Entry: a, Augmentation: Catalog.CreateAugmentation(a.Id, a.Rank) ?? throw new InvalidOperationException($"Augmentation {a.Id} is not in the catalog")))
            .ToList();
        void AppendAugmentations<T>(string key) where T : Augmentation
        {
            var ofKind = augmentations.Where(a => a.Augmentation is T).ToList();
            if (ofKind.Count > 0) s.AppendLine($"{key}={string.Join('/', ofKind.Select(a => $"{AplNames.Slug(a.Augmentation.Name)}:{a.Entry.Rank}"))}");
        }
        AppendAugmentations<Aspect>("aspects");
        AppendAugmentations<Sigil>("sigils");
        AppendAugmentations<ProtectiveEnchant>("protective_enchant");
        if (build.Glyphs.Count > 0)
        {
            s.AppendLine($"glyphs={string.Join('/', build.Glyphs.Select(g => $"{AplNames.Slug(Catalog.CreateGlyph(g.Id, g.Rank)?.Name ?? throw new InvalidOperationException($"Glyph {g.Id} is not in the catalog"))}:{g.Rank}"))}");
        }
        if (build.GemPower.Count > 0)
        {
            s.AppendLine($"gems={string.Join('/', build.GemPower.Select(g => $"{AplNames.Slug(g.Key.ToString())}:{Text(g.Value)}"))}");
        }
        if (build.SocketedGemIds.Count > 0)
        {
            s.AppendLine($"gem_sockets={string.Join('/', build.SocketedGemIds.Select(id => AplNames.Slug((GemItem.Find(id) ?? throw new InvalidOperationException($"Gem {id} is not known")).Name)))}");
        }
        if (build.GearGemBonuses.Count > 0)
        {
            s.AppendLine($"gear_gem_bonuses={string.Join('/', build.GearGemBonuses.Select(g => $"{AplNames.Slug(g.Key.ToString())}:{g.Value}"))}");
        }
        if (build.StatEnchants.Count > 0)
        {
            s.AppendLine($"stat_enchants={string.Join('/', build.StatEnchants.Select(e => $"{(StatEnchant.Find(e.Id) ?? throw new InvalidOperationException($"Stat enchant {e.Id} is not known")).Key}:{Text(e.Rank)}"))}");
        }
        if (build.StatEnchantCounts.Count > 0)
        {
            s.AppendLine($"stat_enchant_counts={string.Join('/', build.StatEnchantCounts.Select(e => $"{(StatEnchant.Find(e.Key) ?? throw new InvalidOperationException($"Stat enchant {e.Key} is not known")).Key}:{e.Value}"))}");
        }
        RelicLine(s, build, GearSlot.Relic1, build.Relic1ItemId);
        RelicLine(s, build, GearSlot.Relic2, build.Relic2ItemId);
        foreach (int itemId in build.LegendaryItemIds)
        {
            string legendary = BuildHeroes.LegendaryName(build.Hero, itemId) ?? throw new InvalidOperationException($"Legendary item {itemId} is not in the catalog");
            s.AppendLine($"legendary={AplNames.Slug(legendary)},id={itemId}");
        }
        if (build.WeaponAbilityId != 0)
        {
            string weapon = Catalog.CreateWeapon(build.WeaponAbilityId)?.Name ?? GameHeroData.WeaponAbilityNames.GetValueOrDefault(build.WeaponAbilityId)
                            ?? throw new InvalidOperationException($"Weapon ability {build.WeaponAbilityId} is not in the catalog");
            s.AppendLine($"weapon={AplNames.Slug(weapon)}{TempersOptions(build, GearSlot.Weapon)}");
        }
        // The other slots' items; the relics and the weapon are on their own lines above.
        foreach (var piece in build.Gear.Where(p => GearSlots.Kind(p.Slot) is not (GearKind.Relic or GearKind.Weapon)).OrderBy(p => p.Slot))
        {
            var item = GearItem.Find(build.Hero, piece.ItemId) ?? throw new InvalidOperationException($"{build.Hero} has no item {piece.ItemId}");
            s.AppendLine($"{GearSlots.Key(piece.Slot)}={AplNames.Slug(item.Name)},id={piece.ItemId},tempers={piece.Tempers}");
        }
        if (ActionList.Length > 0)
        {
            s.AppendLine();
            s.Append(ActionList);
        }
        return s.ToString();
    }

    private static FormatException Error(int lineNumber, string line, string problem) =>
        new($"Profile line {lineNumber} '{line}': {problem}.");

    /// <summary>Ranked augmentations of one kind (<typeparamref name="T"/>): "name:rank/...".</summary>
    private static void AddAugmentations<T>(CharacterBuild build, string value, string kind) where T : Augmentation
    {
        var known = Catalog.AllAugmentations.OfType<T>().Select(a => (a.Name, a.Id)).ToList();
        foreach (var (name, rank) in Ranked(value)) build.Augmentations.Add(new BuildEntry(IdOf(name, known, kind), rank));
    }

    private static IEnumerable<string> List(string value) => value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<(string Name, int Rank)> Ranked(string value) =>
        List(value).Select(Pair).Select(p => (p.Name, (int)p.Value));

    /// <summary>"name:value" or "name=value".</summary>
    private static (string Name, double Value) Pair(string entry)
    {
        int at = entry.IndexOfAny([':', '=']);
        if (at <= 0) throw new FormatException($"expected name:value in '{entry}'");
        return (entry[..at].Trim(), Number(entry[(at + 1)..]));
    }

    /// <summary>The item ID of an "item,id=item id" relic slot, checked against the item's name.</summary>
    private static int RelicItemId(string value)
    {
        var options = Options(value);
        int itemId = (int)Number(options.GetValueOrDefault("id") ?? throw new FormatException("a relic is <item>,id=<item id>"));
        var relic = Catalog.RelicItemOf(itemId) ?? throw new FormatException($"no relic item {itemId}");
        if (AplNames.Slug(relic.Name) != options[""]) throw new FormatException($"item {itemId} is {AplNames.Slug(relic.Name)}");
        return itemId;
    }

    private static void RelicLine(StringBuilder s, CharacterBuild build, GearSlot slot, int itemId)
    {
        if (itemId == 0) return;
        var relic = Catalog.RelicItemOf(itemId) ?? throw new InvalidOperationException($"Relic item {itemId} is not in the catalog");
        string options = build.Gear.Any(p => p.Slot == slot && p.ItemId == itemId) ? $",tempers={build.Gear.First(p => p.Slot == slot).Tempers}" : "";
        s.AppendLine($"{GearSlots.Key(slot)}={AplNames.Slug(relic.Name)},id={itemId}{options}");
    }

    /// <summary>",id=...,tempers=..." for the item worn in <paramref name="slot"/>, or nothing for none.</summary>
    private static string TempersOptions(CharacterBuild build, GearSlot slot) =>
        build.Gear.FirstOrDefault(p => p.Slot == slot) is { } piece ? $",id={piece.ItemId},tempers={piece.Tempers}" : "";

    /// <summary>
    /// The item a gear slot's line names: "item,id=item id,tempers=count", checked against the hero's item of that ID.
    /// </summary>
    private static GearPiece Worn(string hero, GearSlot slot, string value)
    {
        var options = Options(value);
        int itemId = (int)Number(options.GetValueOrDefault("id") ?? throw new FormatException($"a {GearSlots.Key(slot)} line is <item>,id=<item id>,tempers=<count>"));
        var item = GearItem.Find(hero, itemId) ?? throw new FormatException($"{hero} has no item {itemId}");
        if (item.Kind != GearSlots.Kind(slot)) throw new FormatException($"item {itemId} is a {item.Kind.ToString().ToLowerInvariant()}, not a {GearSlots.Label(slot).ToLowerInvariant()}");
        if (AplNames.Slug(item.Name) != options[""]) throw new FormatException($"item {itemId} is {AplNames.Slug(item.Name)}");
        int tempers = (int)Number(options.GetValueOrDefault("tempers") ?? "0");
        if (tempers < 0 || tempers > item.MaxTempers) throw new FormatException($"{AplNames.Slug(item.Name)} takes 0 to {item.MaxTempers} tempers");
        return new GearPiece(slot, itemId, tempers);
    }

    /// <summary>
    /// The relic or weapon line's item as its slot's piece, when the line gives its <c>id</c> and <c>tempers</c> (a line
    /// written before builds held their gear has none). A relic the hero has no item of stays a relic alone.
    /// </summary>
    private static void AddWornWithTempers(CharacterBuild build, GearSlot slot, string value)
    {
        var options = Options(value);
        if (!options.ContainsKey("tempers") || !options.TryGetValue("id", out string? id)) return;
        var item = GearItem.Find(build.Hero, (int)Number(id));
        if (item == null || item.Kind != GearSlots.Kind(slot)) return;
        int tempers = (int)Number(options["tempers"]);
        if (tempers < 0 || tempers > item.MaxTempers) throw new FormatException($"{AplNames.Slug(item.Name)} takes 0 to {item.MaxTempers} tempers");
        build.Gear.Add(new GearPiece(slot, item.Id, tempers));
    }

    /// <summary>"name,key=value,...": the name under the empty key.</summary>
    private static Dictionary<string, string> Options(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        var options = new Dictionary<string, string> { [""] = parts[0] };
        foreach (string part in parts.Skip(1))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) throw new FormatException($"expected key=value in '{part}'");
            options[part[..eq]] = part[(eq + 1)..];
        }
        return options;
    }

    private static int IdOf(string name, IEnumerable<(string Name, int Id)> known, string kind) =>
        known.FirstOrDefault(k => AplNames.Slug(k.Name) == name) is { Id: > 0 } found
            ? found.Id
            : throw new FormatException($"no {kind} '{name}'");

    private static double Number(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new FormatException($"'{text}' is not a number");

    private static double Percent(string text)
    {
        string trimmed = text.Trim();
        if (!trimmed.EndsWith('%')) throw new FormatException($"'{text}' is not a percentage such as 24%");
        return Number(trimmed[..^1]) / 100.0;
    }

    private static string Text(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
