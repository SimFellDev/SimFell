using System.Text;
using System.Text.Json.Serialization;
using FellowshipSim.Core.APL.Expressions;
using FellowshipSim.Core.Import;
using Superpower;
using Superpower.Model;

namespace FellowshipSim.Core.APL;

/// <summary>A name an action list can use: its action-list form (<see cref="Slug"/>) and the game's name.</summary>
public sealed record AplName(string Slug, string Name);

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
    /// <summary>A path that reads nothing the hero has: it evaluates to 0, or to a scope's first property.</summary>
    Unknown,
}

/// <summary>One token of an expression: its place in the text, its kind, and why it is <see cref="AplTokenKind.Unknown"/>.</summary>
public sealed record AplTokenSpan(int Start, int Length, AplTokenKind Kind, string? Problem = null);

/// <summary>
/// An expression's tokens, its parse error (with the error's offset in the text) when it does not parse, the text with
/// <c>&amp;</c>, <c>|</c> and <c>!</c> written <c>and</c>, <c>or</c> and <c>not</c> (<see cref="InWords"/>, null when it does not tokenize),
/// and its builder rows (<see cref="Condition"/>, null when it does not parse or has a part with no row).
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
        [PropertyExpression.CooldownScope] = Abilities,
        [PropertyExpression.ActionScope] = Abilities,
    };
    public string ResourcePrefix => PropertyExpression.ResourcePrefix;
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
        IReadOnlyList<string> repeatableAbilities)
    {
        Hero = hero;
        Abilities = abilities;
        Resources = resources;
        Auras = auras;
        RepeatableAbilities = repeatableAbilities;
    }

    private AplVocabulary(string heroName, Hero hero)
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
        Auras = AuraDef.DeclaredOn(owners).Concat(hero.RuntimeAuras)
            .Select(a => new AplName(AplNames.Slug(a.Name), a.Name))
            .DistinctBy(n => n.Slug).OrderBy(n => n.Slug, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The vocabulary of <paramref name="heroName"/> (a name in <see cref="Catalog.Heroes"/>), with every talent of its tree
    /// and every weapon ability.
    /// </summary>
    public static AplVocabulary For(string heroName)
    {
        var tree = Catalog.CreateHero(heroName).TalentTreeSlots;
        var hero = HeroImporter.Import(new CharacterBuild { Hero = heroName, Talents = tree.Select(s => s.TalentId).ToList() });
        foreach (var weapon in Catalog.AllWeapons) weapon.Equip(hero);
        return new AplVocabulary(Catalog.Heroes.Keys.First(k => k.Equals(heroName, StringComparison.OrdinalIgnoreCase)), hero);
    }

    /// <summary>Token kind of <paramref name="property"/> for this hero, and the problem when the hero has nothing by that name.</summary>
    public (AplTokenKind Kind, string? Problem) Classify(PropertyExpression property)
    {
        if (PropertyExpression.Named.ContainsKey(property.PropertyPath)) return (AplTokenKind.Named, null);
        if (Resources.Any(r => r.Slug == property.ResourceName)) return (AplTokenKind.Resource, null);
        // Build parts and variables read 0 when the build or the list has none of the name.
        if (property.Scope != null && (PropertyExpression.BuildScopes.ContainsKey(property.Scope) || property.Scope == PropertyExpression.VariableScope))
        {
            return (AplTokenKind.Named, null);
        }
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

    /// <summary>Tokens of <paramref name="text"/> with their kinds, and the parse error of text outside the grammar.</summary>
    public AplExpressionCheck Check(string text)
    {
        var tokenized = AplParser.Tokenize(text);
        if (!tokenized.HasValue)
        {
            return new AplExpressionCheck([], tokenized.FormatErrorMessageFragment(), tokenized.ErrorPosition.Absolute);
        }

        var tokens = tokenized.Value.Select(t =>
        {
            var (kind, problem) = t.Kind switch
            {
                AplToken.Number => (AplTokenKind.Number, null),
                AplToken.LParen or AplToken.RParen => (AplTokenKind.Parenthesis, null),
                AplToken.Identifier => Classify(new PropertyExpression(t.ToStringValue())),
                _ => (AplTokenKind.Operator, (string?)null),
            };
            return new AplTokenSpan(t.Position.Absolute, t.Span.Length, kind, problem);
        }).ToList();
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
