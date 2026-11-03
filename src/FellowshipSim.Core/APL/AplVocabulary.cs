using System.Text;
using System.Text.Json.Serialization;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Customization.Weapons;
using FellowshipSim.Core.Import;
using Superpower;
using Superpower.Model;

namespace FellowshipSim.Core.APL;

/// <summary>A name an action list can use: its action-list form (<see cref="Slug"/>), the game's name, and its meaning where the condition reference has one.</summary>
public sealed record AplName(string Slug, string Name, string? Description = null);

/// <summary>A <c>target_if</c> selection: its value, its prefix in action lists ("min"), and its label in the editor.</summary>
public sealed record AplTargetSelectionName(AplTargetSelection Selection, string Prefix, string Label);

/// <summary>Kind of a token in an expression. The editor colours each kind.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AplTokenKind>))]
public enum AplTokenKind
{
    Number,
    Operator,
    Parenthesis,
    /// <summary>A value in <see cref="PropertyExpression.Named"/>.</summary>
    Named,
    Resource,
    Buff,
    Debuff,
    Cooldown,
    /// <summary>A math function's name (<see cref="AplFunction"/>).</summary>
    Function,
    /// <summary>A path that reads nothing the hero has: it evaluates to 0, or to a scope's first property.</summary>
    Unknown,
    /// <summary>A '#' line of an action list.</summary>
    Comment,
    /// <summary>A list's prefix (<c>actions.aoe</c>), or the list a run_action_list or call_action_list names.</summary>
    List,
    /// <summary>An argument's key, a target selection, a variable op, or an action that controls the list (run_action_list).</summary>
    Keyword,
}

/// <summary>One token of an expression: its place in the text, its kind, and why it is <see cref="AplTokenKind.Unknown"/>.</summary>
public sealed record AplTokenSpan(int Start, int Length, AplTokenKind Kind, string? Problem = null);

/// <summary>
/// An expression's tokens, its parse error (with the error's offset in the text) when it does not parse, the text with
/// <c>&amp;</c>, <c>|</c> and <c>!</c> written <c>and</c>, <c>or</c> and <c>not</c> (<see cref="InWords"/>, null when it does not tokenize),
/// and its builder rows (<see cref="Condition"/>, null when it does not parse).
/// </summary>
public sealed record AplExpressionCheck(
    IReadOnlyList<AplTokenSpan> Tokens, string? Error, int? ErrorStart, string? InWords = null,
    IReadOnlyList<AplConditionItem>? Condition = null);

/// <summary>
/// The names an action list for one hero can use: the hero's abilities (with every weapon ability), resources and auras
/// (its own, its abilities', every talent in its tree, and all gear it can wear), with the named values and scoped
/// properties <see cref="PropertyExpression"/> reads.
/// </summary>
public sealed class AplVocabulary
{
    /// <summary>The hero <see cref="Classify"/> finds abilities on; null in a vocabulary read from JSON.</summary>
    private readonly Hero? _hero;

    public string Hero { get; }
    public IReadOnlyList<AplName> Abilities { get; }
    public IReadOnlyList<AplName> Resources { get; }
    public IReadOnlyList<AplName> Auras { get; }
    public IReadOnlyCollection<string> Named => PropertyExpression.Named.Keys.ToList();
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Scopes => PropertyExpression.Scopes;
    /// <summary>The names each scope in <see cref="Scopes"/> reads: auras for buff and debuff, abilities for cooldown and action.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<AplName>> ScopeNames => new Dictionary<string, IReadOnlyList<AplName>>
    {
        [PropertyExpression.BuffScope] = Auras,
        [PropertyExpression.DebuffScope] = Auras,
        [PropertyExpression.DotScope] = Auras,
        [PropertyExpression.CooldownScope] = Abilities,
        [PropertyExpression.ActionScope] = Abilities,
    };
    /// <summary>
    /// The names each scope in <see cref="PropertyExpression.BuildScopes"/> reads: the talents of the hero's tree, every
    /// blessing, the hero's legendaries, every weapon trait, and every weapon and weapon ability.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<AplName>> BuildNames { get; }
    /// <summary>Values an action reads about its own ability, unscoped ("full_recharge_time").</summary>
    public IReadOnlyCollection<string> ActionValues => PropertyExpression.ActionValues.Keys.ToList();
    public string ResourcePrefix => PropertyExpression.ResourcePrefix;
    public string DeficitSuffix => PropertyExpression.DeficitSuffix;
    public IReadOnlyList<AplTargetSelectionName> TargetSelections { get; } = Enum.GetValues<AplTargetSelection>()
        .Select(s => new AplTargetSelectionName(s, AplTargetIf.Prefix(s), AplTargetIf.Label(s))).ToList();
    /// <summary><c>run_action_list</c> and <c>call_action_list</c>.</summary>
    public IReadOnlyList<string> ListActions { get; } = Enum.GetValues<AplListAction>().Select(a => a.Name()).ToList();
    public string DefaultList => AplActionLine.DefaultList;
    /// <summary>
    /// Abilities with a category (<see cref="AbilityCategory.None"/> marks an attack other abilities trigger), no cooldown and
    /// no cast requirement of their own (<see cref="Ability.CanCast"/> not overridden), ready on every decision.
    /// </summary>
    public IReadOnlyList<string> RepeatableAbilities { get; }

    /// <summary>
    /// A vocabulary read from JSON: the hero's names without the hero. <see cref="Check"/> and <see cref="Classify"/> need
    /// the hero, so only <see cref="For"/> gives a vocabulary that checks expressions.
    /// </summary>
    [JsonConstructor]
    public AplVocabulary(string hero, IReadOnlyList<AplName> abilities, IReadOnlyList<AplName> resources, IReadOnlyList<AplName> auras,
        IReadOnlyList<string> repeatableAbilities, IReadOnlyDictionary<string, IReadOnlyList<AplName>> buildNames)
    {
        Hero = hero;
        Abilities = abilities;
        Resources = resources;
        Auras = auras;
        RepeatableAbilities = repeatableAbilities;
        BuildNames = buildNames;
    }

    private AplVocabulary(string heroName, Hero hero, IReadOnlyList<Weapon> weapons, IEnumerable<AuraDef> gearAuras)
    {
        _hero = hero;
        Hero = heroName;
        Abilities = hero.Abilities.Select(a => new AplName(AplNames.Slug(a.Name), a.Name)).DistinctBy(n => n.Slug).ToList();
        RepeatableAbilities = hero.Abilities
            .Where(a => a.Category != AbilityCategory.None && a.Cooldown == SimTime.Zero
                        && a.GetType().GetMethod(nameof(Ability.CanCast), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                            [typeof(SimulationContext), typeof(bool), typeof(string).MakeByRefType()])!.DeclaringType == typeof(Ability))
            .Select(a => AplNames.Slug(a.Name)).Distinct().ToList();
        Resources = hero.ResourceDefs.Append(Domain.Hero.SpiritPoints)
            .SelectMany(r => r.Aliases.Prepend(r.AplName).Select(slug => new AplName(slug, r.Name)))
            .DistinctBy(n => n.Slug).ToList();
        var owners = new[] { hero.GetType() }
            .Concat(hero.Abilities.Select(a => a.GetType()))
            .Concat(hero.Talents.Select(t => t.GetType()))
            .Concat(Catalog.GearTypes(heroName));
        Auras = AuraDef.DeclaredOn(owners).Concat(hero.RuntimeAuras).Concat(gearAuras).Select(a => a.Name)
            .Concat(hero is ITargetEffects effects ? effects.TargetEffectNames : [])
            .Select(name => new AplName(AplNames.Slug(name), name))
            .DistinctBy(n => n.Slug).OrderBy(n => n.Slug, StringComparer.Ordinal).ToList();
        BuildNames = PropertyExpression.BuildScopes.Keys.ToDictionary(scope => scope, scope => Names(scope switch
        {
            PropertyExpression.TalentScope => hero.Talents.Select(t => t.Name),
            PropertyExpression.BlessingScope => Catalog.AllGlyphs.Select(b => b.Name),
            PropertyExpression.LegendaryScope => Catalog.Legendaries(heroName).Select(l => l.Name),
            PropertyExpression.WeaponTraitScope => Catalog.AllAugmentations.Select(t => t.Name),
            PropertyExpression.WeaponScope => weapons.SelectMany(w => w.Ability is { } ability ? [w.Name, ability.Name] : new[] { w.Name }),
            _ => throw new InvalidOperationException($"No names for the build scope `{scope}`."),
        }));
    }

    private static IReadOnlyList<AplName> Names(IEnumerable<string> names) =>
        names.Select(name => new AplName(AplNames.Slug(name), name)).DistinctBy(n => n.Slug).OrderBy(n => n.Slug, StringComparer.Ordinal).ToList();

    /// <summary>
    /// The vocabulary of <paramref name="heroName"/> (a name in <see cref="Catalog.Heroes"/>), with every talent of its tree
    /// and every weapon ability, and the auras of all gear it can wear.
    /// </summary>
    public static AplVocabulary For(string heroName)
    {
        var tree = Catalog.CreateHero(heroName).TalentTreeSlots;
        var hero = HeroImporter.Import(new CharacterBuild { Hero = heroName, Talents = tree.Select(s => s.TalentId).ToList() });
        var weapons = Catalog.AllWeapons.ToList();
        foreach (var weapon in weapons) weapon.Equip(hero);
        // Gear that builds its auras in Bind (a rank's stacks or modifiers) has them only once bound, on a hero of its own.
        var gear = HeroImporter.Import(new CharacterBuild { Hero = heroName });
        foreach (var glyph in Catalog.AllGlyphs) glyph.Bind(gear);
        foreach (var augmentation in Catalog.AllAugmentations) augmentation.Bind(gear);
        foreach (var relic in Catalog.AllRelicPassives) relic.Bind(gear);
        foreach (var legendary in Catalog.Legendaries(heroName)) legendary.Bind(gear);
        return new AplVocabulary(Catalog.Heroes.Keys.First(k => k.Equals(heroName, StringComparison.OrdinalIgnoreCase)), hero, weapons, gear.RuntimeAuras);
    }

    /// <summary>Token kind of <paramref name="property"/> for this hero, and the problem when the hero has nothing by that name.</summary>
    // The path after an aggregate's ':': a debuff's remaining time, with no oldest or newest pick.
    private (AplTokenKind Kind, string? Problem) ClassifyAggregated(PropertyExpression property) =>
        property.IsEnemyAggregate
            ? Classify(property)
            : (AplTokenKind.Unknown, $"`{property.Aggregate!.Value.Prefix()}:` reads `{PropertyExpression.DotScope}.<name>.{AplEnemyAggregates.Property}` or `{PropertyExpression.DebuffScope}.<name>.{AplEnemyAggregates.Property}`; it reads as 0 here.");

    public (AplTokenKind Kind, string? Problem) Classify(PropertyExpression property)
    {
        if (PropertyExpression.Named.ContainsKey(property.PropertyPath)) return (AplTokenKind.Named, null);
        if (Resources.Any(r => r.Slug == property.ResourceName)) return (AplTokenKind.Resource, null);
        // Build parts and variables read 0 when the build or the list has none of the name.
        if (property.Scope != null && (PropertyExpression.BuildScopes.ContainsKey(property.Scope) || property.Scope == PropertyExpression.VariableScope))
        {
            return (AplTokenKind.Named, null);
        }
        if (property.Scope is PropertyExpression.PrevScope or PropertyExpression.PrevGcdScope)
        {
            var hero = _hero ?? throw new InvalidOperationException($"This vocabulary of {Hero} was read from JSON and has no hero to check against.");
            return AplNames.FindAbility(hero, property.Name!) != null
                ? (AplTokenKind.Cooldown, null)
                : (AplTokenKind.Unknown, $"{Hero} has no ability named `{property.Name}`.");
        }
        if (property.Scope == PropertyExpression.ActiveDotScope)
        {
            return Auras.Any(a => a.Slug == property.Name) ? (AplTokenKind.Debuff, null) : (AplTokenKind.Unknown, $"{Hero} has no debuff named `{property.Name}`.");
        }
        // An action's own values ("full_recharge_time", "refreshable") read its ability (AplParser binds them so).
        if (property.Scope == null && PropertyExpression.OfAction(property.PropertyPath, "") != null) return (AplTokenKind.Named, null);
        if (property.Scope == null) return (AplTokenKind.Unknown, $"{Hero} has no value or resource named `{property.PropertyPath}`; it reads as 0.");

        var properties = PropertyExpression.Scopes[property.Scope];
        if (!properties.Contains(property.Property))
        {
            return (AplTokenKind.Unknown, $"`{property.Scope}` has no property `{property.Property}` (it has {string.Join(", ", properties)}).");
        }
        if (property.Scope is PropertyExpression.CooldownScope or PropertyExpression.ActionScope)
        {
            var hero = _hero ?? throw new InvalidOperationException($"This vocabulary of {Hero} was read from JSON and has no hero to check against.");
            return AplNames.FindAbility(hero, property.Name!) != null
                ? (AplTokenKind.Cooldown, null)
                : (AplTokenKind.Unknown, $"{Hero} has no ability named `{property.Name}`.");
        }
        if (Auras.All(a => a.Slug != property.Name)) return (AplTokenKind.Unknown, $"{Hero} has no aura named `{property.Name}`.");
        return property.Scope == PropertyExpression.BuffScope ? (AplTokenKind.Buff, null) : (AplTokenKind.Debuff, null);
    }

    /// <summary>Tokens of the action list <paramref name="text"/> with their kinds, and its first line outside the grammar (<see cref="AplListCheck"/>).</summary>
    public AplExpressionCheck CheckList(string text) => AplListCheck.Check(this, text);

    /// <summary>Tokens of <paramref name="text"/> with their kinds, and the parse error of text outside the grammar.</summary>
    public AplExpressionCheck Check(string text)
    {
        var tokenized = AplParser.Tokenize(text);
        if (!tokenized.HasValue)
        {
            return new AplExpressionCheck([], tokenized.FormatErrorMessageFragment(), tokenized.ErrorPosition.Absolute);
        }

        var tokens = Classified(tokenized.Value.ToArray());
        string inWords = InWords(text, tokenized.Value);

        try
        {
            var expression = AplParser.ParseExpression(text);
            return new AplExpressionCheck(tokens, null, null, inWords, AplCondition.Rows(expression));
        }
        catch (ParseException e)
        {
            return new AplExpressionCheck(tokens, e.Message, e.ErrorPosition.HasValue ? e.ErrorPosition.Absolute : text.Length, inWords);
        }
    }

    /// <summary>The kind of each of an expression's tokens, at the offsets the tokenizer gave them.</summary>
    internal List<AplTokenSpan> Classified(Token<AplToken>[] all) => all.Select((t, i) =>
    {
        var (kind, problem) = t.Kind switch
        {
            AplToken.Number => (AplTokenKind.Number, null),
            AplToken.LParen or AplToken.RParen => (AplTokenKind.Parenthesis, null),
            // A function's name, before its arguments in parentheses.
            AplToken.Identifier when i + 1 < all.Length && all[i + 1].Kind == AplToken.LParen
                                     && AplFunctions.ByName.ContainsKey(t.ToStringValue()) => (AplTokenKind.Function, null),
            // An aggregate over every enemy in combat, before its ':' and path.
            AplToken.Identifier when i + 1 < all.Length && all[i + 1].Kind == AplToken.Colon
                                     && AplEnemyAggregates.ByPrefix.ContainsKey(t.ToStringValue()) => (AplTokenKind.Keyword, null),
            AplToken.Identifier when i >= 2 && all[i - 1].Kind == AplToken.Colon && all[i - 2].Kind == AplToken.Identifier
                                     && AplEnemyAggregates.ByPrefix.ContainsKey(all[i - 2].ToStringValue()) =>
                ClassifyAggregated(new PropertyExpression($"{all[i - 2].ToStringValue()}:{t.ToStringValue()}")),
            AplToken.Identifier => Classify(new PropertyExpression(t.ToStringValue())),
            _ => (AplTokenKind.Operator, (string?)null),
        };
        return new AplTokenSpan(t.Position.Absolute, t.Span.Length, kind, problem);
    }).ToList();

    // Each '&', '|' and '!' becomes its word, with a space on each side unless the text starts or ends there or a
    // parenthesis or space is already next to it. Every other character stays as written.
    private static string InWords(string text, TokenList<AplToken> tokens)
    {
        var words = new StringBuilder();
        int at = 0;
        foreach (var token in tokens)
        {
            string? word = token.Kind switch
            {
                AplToken.And => "and",
                AplToken.Or => "or",
                AplToken.Not => "not",
                _ => null
            };
            if (word == null || char.IsLetter(text[token.Position.Absolute])) continue;

            words.Append(text, at, token.Position.Absolute - at);
            if (words.Length > 0 && !char.IsWhiteSpace(words[^1]) && words[^1] != '(') words.Append(' ');
            words.Append(word);
            at = token.Position.Absolute + token.Span.Length;
            if (at < text.Length && !char.IsWhiteSpace(text[at]) && text[at] != ')') words.Append(' ');
        }
        return words.Append(text, at, text.Length - at).ToString();
    }
}
