using System.Globalization;
using FellowshipSim.Core.APL.Expressions;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace FellowshipSim.Core.APL;

/// <summary>
/// Parses SimulationCraft-style action lists: one action per line, written <c>actions+=/name,if=expr</c>,
/// <c>actions=name</c> or <c>name,if=expr</c>. Lines starting with '#' are comments.
/// </summary>
public static class AplParser
{
    // Actions that control the list rather than cast an ability.
    private static readonly HashSet<string> SkippedActions = ["run_action_list", "snapshot_stats"];

    private static readonly TextParser<TextSpan> Word = Span.WithAll(c => char.IsLetterOrDigit(c) || c is '_' or '.');

    private static bool IsNumber(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static readonly Tokenizer<AplToken> Tokenizer = new TokenizerBuilder<AplToken>()
        .Ignore(Span.WhiteSpace)
        .Match(Character.EqualTo('('), AplToken.LParen)
        .Match(Character.EqualTo(')'), AplToken.RParen)
        .Match(Character.EqualTo('&'), AplToken.And)
        .Match(Character.EqualTo('|'), AplToken.Or)
        .Match(Span.EqualTo("!="), AplToken.NotEqual)
        .Match(Character.EqualTo('!'), AplToken.Not)
        .Match(Character.EqualTo('-'), AplToken.Minus)
        .Match(Span.EqualTo("=="), AplToken.Equal)
        .Match(Character.EqualTo('='), AplToken.Equal)
        .Match(Span.EqualTo("<="), AplToken.LessEqual)
        .Match(Character.EqualTo('<'), AplToken.Less)
        .Match(Span.EqualTo(">="), AplToken.GreaterEqual)
        .Match(Character.EqualTo('>'), AplToken.Greater)
        .Match(Span.EqualTo("+="), AplToken.PlusEqual)
        .Match(Character.EqualTo('/'), AplToken.Slash)
        .Match(Character.EqualTo(','), AplToken.Comma)
        .Match(Word.Where(w => IsNumber(w.ToStringValue()), "number"), AplToken.Number)
        .Match(Word, AplToken.Identifier)
        .Build();

    private static BinaryOperator? ComparisonOperator(AplToken kind) => kind switch
    {
        AplToken.Equal => BinaryOperator.Equal,
        AplToken.NotEqual => BinaryOperator.NotEqual,
        AplToken.Less => BinaryOperator.Less,
        AplToken.LessEqual => BinaryOperator.LessEqual,
        AplToken.Greater => BinaryOperator.Greater,
        AplToken.GreaterEqual => BinaryOperator.GreaterEqual,
        _ => null
    };

    private static UnaryOperator? PrefixOperator(AplToken kind) => kind switch
    {
        AplToken.Not => UnaryOperator.Not,
        AplToken.Minus => UnaryOperator.Negate,
        _ => null
    };

    private static IAplExpression Binary(BinaryOperator op, IAplExpression left, IAplExpression right) =>
        new BinaryOpExpression(left, op, right);

    private static readonly TokenListParser<AplToken, IAplExpression> Constant =
        Token.EqualTo(AplToken.Number)
            .Select(t => (IAplExpression)new ConstantExpression(double.Parse(t.ToStringValue(), NumberStyles.Float, CultureInfo.InvariantCulture)));

    private static readonly TokenListParser<AplToken, IAplExpression> Property =
        Token.EqualTo(AplToken.Identifier).Select(t => (IAplExpression)new PropertyExpression(t.ToStringValue()));

    private static readonly TokenListParser<AplToken, IAplExpression> Group =
        Parse.Ref(() => Expression!).Between(Token.EqualTo(AplToken.LParen), Token.EqualTo(AplToken.RParen));

    private static readonly TokenListParser<AplToken, IAplExpression> Prefixed =
        from op in Token.Matching<AplToken>(kind => PrefixOperator(kind) is not null, "prefix operator").Select(t => PrefixOperator(t.Kind)!.Value)
        from operand in Parse.Ref(() => Primary!)
        select (IAplExpression)new UnaryOpExpression(op, operand);

    // Primary: '!' Primary, '-' Primary, '(' Expression ')', Number, Property
    private static readonly TokenListParser<AplToken, IAplExpression> Primary =
        Prefixed.Or(Group).Or(Constant).Or(Property);

    // Comparison: '=', '==', '!=', '<', '<=', '>', '>='
    private static readonly TokenListParser<AplToken, IAplExpression> Comparison = Parse.Chain(
        Token.Matching<AplToken>(kind => ComparisonOperator(kind) is not null, "comparison operator").Select(t => ComparisonOperator(t.Kind)!.Value),
        Primary,
        Binary);

    // Logical AND: '&'
    private static readonly TokenListParser<AplToken, IAplExpression> Conjunction =
        Parse.Chain(Token.EqualTo(AplToken.And).Value(BinaryOperator.And), Comparison, Binary);

    // Logical OR: '|'
    private static readonly TokenListParser<AplToken, IAplExpression> Expression =
        Parse.Chain(Token.EqualTo(AplToken.Or).Value(BinaryOperator.Or), Conjunction, Binary);

    // List prefix: 'actions' or 'actions.<list>', then '=' or '+=', then an optional '/'.
    private static readonly TokenListParser<AplToken, Unit> ListPrefix =
        (from list in Token.EqualTo(AplToken.Identifier)
         from assign in Token.EqualTo(AplToken.Equal).Or(Token.EqualTo(AplToken.PlusEqual))
         from slash in Token.EqualTo(AplToken.Slash).OptionalOrDefault()
         select Unit.Value).Try();

    // Argument: ',' key '=' Expression. Only 'if' has an effect.
    private static readonly TokenListParser<AplToken, (string Key, IAplExpression Value)> Argument =
        from comma in Token.EqualTo(AplToken.Comma)
        from key in Token.EqualTo(AplToken.Identifier)
        from equal in Token.EqualTo(AplToken.Equal)
        from value in Expression
        select (key.ToStringValue(), value);

    private static readonly TokenListParser<AplToken, (string Name, (string Key, IAplExpression Value)[] Arguments)> ActionLine =
        (from prefix in ListPrefix.OptionalOrDefault()
         from name in Token.EqualTo(AplToken.Identifier)
         from arguments in Argument.Many()
         select (name.ToStringValue(), arguments)).AtEnd();

    public static List<AplAction> ParseScript(string script)
    {
        var actions = new List<AplAction>();
        using var reader = new StringReader(script);
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            AplAction? action;
            try
            {
                action = ParseActionLine(trimmed);
            }
            catch (ParseException e)
            {
                throw new ParseException($"APL line {lineNumber}: {e.Message}", e.ErrorPosition, e);
            }

            if (action != null)
            {
                actions.Add(action);
            }
        }

        return actions;
    }

    /// <summary>The action on one line, or null for an action in <see cref="SkippedActions"/>.</summary>
    public static AplAction? ParseActionLine(string line)
    {
        var (name, arguments) = ActionLine.Parse(Tokenizer.Tokenize(line));
        if (SkippedActions.Contains(name))
            return null;

        return new AplAction
        {
            AbilityName = name,
            Condition = arguments.LastOrDefault(a => a.Key.Equals("if", StringComparison.OrdinalIgnoreCase)).Value,
            RawLine = line
        };
    }

    public static IAplExpression ParseExpression(string text) => Expression.AtEnd().Parse(Tokenizer.Tokenize(text));
}
