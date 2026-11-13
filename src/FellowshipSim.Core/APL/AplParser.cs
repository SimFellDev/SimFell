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
    internal static readonly HashSet<string> SkippedActions = ["snapshot_stats"];

    private static readonly TextParser<TextSpan> Word = Span.WithAll(c => char.IsLetterOrDigit(c) || c is '_' or '.');

    private static bool IsNumber(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    // The words 'and', 'or' and 'not' are the operators '&', '|' and '!'.
    private static TextParser<TextSpan> Keyword(string keyword) =>
        Word.Where(w => w.ToStringValue().Equals(keyword, StringComparison.OrdinalIgnoreCase), keyword);

    private static readonly Tokenizer<AplToken> Tokenizer = new TokenizerBuilder<AplToken>()
        .Ignore(Span.WhiteSpace)
        .Match(Character.EqualTo('('), AplToken.LParen)
        .Match(Character.EqualTo(')'), AplToken.RParen)
        .Match(Character.EqualTo('&'), AplToken.And)
        .Match(Character.EqualTo('|'), AplToken.Or)
        .Match(Span.EqualTo("!="), AplToken.NotEqual)
        .Match(Character.EqualTo('!'), AplToken.Not)
        .Match(Character.EqualTo('-'), AplToken.Minus)
        .Match(Character.EqualTo('*'), AplToken.Star)
        .Match(Span.EqualTo("%%"), AplToken.DoublePercent)
        .Match(Character.EqualTo('%'), AplToken.Percent)
        .Match(Span.EqualTo("=="), AplToken.Equal)
        .Match(Character.EqualTo('='), AplToken.Equal)
        .Match(Span.EqualTo("<="), AplToken.LessEqual)
        .Match(Character.EqualTo('<'), AplToken.Less)
        .Match(Span.EqualTo(">="), AplToken.GreaterEqual)
        .Match(Character.EqualTo('>'), AplToken.Greater)
        .Match(Span.EqualTo("+="), AplToken.PlusEqual)
        .Match(Character.EqualTo('+'), AplToken.Plus)
        .Match(Character.EqualTo('/'), AplToken.Slash)
        .Match(Character.EqualTo(','), AplToken.Comma)
        .Match(Character.EqualTo(':'), AplToken.Colon)
        .Match(Word.Where(w => IsNumber(w.ToStringValue()), "number"), AplToken.Number)
        .Match(Keyword("and"), AplToken.And)
        .Match(Keyword("or"), AplToken.Or)
        .Match(Keyword("not"), AplToken.Not)
        .Match(Word, AplToken.Identifier)
        .Build();

    private static BinaryOperator? InfixOperator(AplToken kind) => kind switch
    {
        AplToken.Or => BinaryOperator.Or,
        AplToken.And => BinaryOperator.And,
        AplToken.Plus => BinaryOperator.Add,
        AplToken.Minus => BinaryOperator.Subtract,
        AplToken.Star => BinaryOperator.Multiply,
        AplToken.Percent => BinaryOperator.Divide,
        AplToken.DoublePercent => BinaryOperator.Modulo,
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
        AplToken.Minus => UnaryOperator.Minus,
        _ => null
    };

    private static IAplExpression Binary(BinaryOperator op, IAplExpression left, IAplExpression right) =>
        new BinaryOpExpression(left, op, right);

    /// <summary>A left-associative precedence level over <paramref name="operand"/> for the given operators.</summary>
    private static TokenListParser<AplToken, IAplExpression> Level(TokenListParser<AplToken, IAplExpression> operand, params BinaryOperator[] operators) =>
        Parse.Chain(
            Token.Matching<AplToken>(kind => InfixOperator(kind) is { } op && operators.Contains(op), string.Join(" or ", operators.Select(o => $"`{o.Symbol()}`")))
                .Select(t => InfixOperator(t.Kind)!.Value),
            operand,
            Binary);

    private static readonly TokenListParser<AplToken, IAplExpression> Constant =
        Token.EqualTo(AplToken.Number)
            .Select(t => (IAplExpression)new ConstantExpression(double.Parse(t.ToStringValue(), NumberStyles.Float, CultureInfo.InvariantCulture)));

    // An aggregate over every enemy in combat: 'min' or 'max', then ':', before a path.
    private static readonly TokenListParser<AplToken, string?> Aggregate =
        (from name in Token.EqualTo(AplToken.Identifier).Where(t => AplEnemyAggregates.ByPrefix.ContainsKey(t.ToStringValue()), "`min` or `max`")
         from colon in Token.EqualTo(AplToken.Colon)
         select (string?)$"{name.ToStringValue()}:").Try().OptionalOrDefault();

    private static readonly TokenListParser<AplToken, IAplExpression> Property =
        from aggregate in Aggregate
        from path in Token.EqualTo(AplToken.Identifier)
        select (IAplExpression)new PropertyExpression(aggregate + path.ToStringValue());

    private static readonly TokenListParser<AplToken, IAplExpression> Group =
        Parse.Ref(() => Expression!).Between(Token.EqualTo(AplToken.LParen), Token.EqualTo(AplToken.RParen))
            .Select(inner => (IAplExpression)new GroupExpression(inner));

    // Call: a function's name, then its arguments in parentheses, separated by ','.
    private static readonly TokenListParser<AplToken, IAplExpression> Call =
        (from name in Token.EqualTo(AplToken.Identifier).Where(t => AplFunctions.ByName.ContainsKey(t.ToStringValue()), "function")
         from open in Token.EqualTo(AplToken.LParen)
         select AplFunctions.ByName[name.ToStringValue()]).Try()
        .Then(function =>
            (from arguments in Parse.Ref(() => Expression!).ManyDelimitedBy(Token.EqualTo(AplToken.Comma))
             from close in Token.EqualTo(AplToken.RParen)
             select arguments)
            .Where(arguments => function.Takes(arguments.Length), $"`{function.Name()}` with {function.ArgumentsText()}")
            .Select(arguments => (IAplExpression)new FunctionExpression(function, arguments)));

    private static readonly TokenListParser<AplToken, IAplExpression> Prefixed =
        from op in Token.Matching<AplToken>(kind => PrefixOperator(kind) is not null, "prefix operator").Select(t => PrefixOperator(t.Kind)!.Value)
        from operand in Parse.Ref(() => Primary!)
        select (IAplExpression)new UnaryOpExpression(op, operand);

    // Primary: '!' Primary, '-' Primary, '(' Expression ')', Number, Call, Property
    private static readonly TokenListParser<AplToken, IAplExpression> Primary =
        Prefixed.Or(Group).Or(Constant).Or(Call).Or(Property);

    // Product: '*', '%' (divide), '%%' (remainder)
    private static readonly TokenListParser<AplToken, IAplExpression> Product =
        Level(Primary, BinaryOperator.Multiply, BinaryOperator.Divide, BinaryOperator.Modulo);

    // Sum: '+', '-'
    private static readonly TokenListParser<AplToken, IAplExpression> Sum =
        Level(Product, BinaryOperator.Add, BinaryOperator.Subtract);

    // Comparison: '=', '==', '!=', '<', '<=', '>', '>='
    private static readonly TokenListParser<AplToken, IAplExpression> Comparison = Level(Sum,
        BinaryOperator.Equal, BinaryOperator.NotEqual, BinaryOperator.Less, BinaryOperator.LessEqual, BinaryOperator.Greater, BinaryOperator.GreaterEqual);

    // Logical AND: '&'
    private static readonly TokenListParser<AplToken, IAplExpression> Conjunction = Level(Comparison, BinaryOperator.And);

    // Logical OR: '|'
    private static readonly TokenListParser<AplToken, IAplExpression> Expression = Level(Conjunction, BinaryOperator.Or);

    // List prefix: 'actions' or 'actions.<list>', then '=' or '+=', then an optional '/'. Returns the list's name.
    private static readonly TokenListParser<AplToken, string> ListPrefix =
        (from list in Token.EqualTo(AplToken.Identifier).Where(t => AplActionLine.ListOf(t.ToStringValue()) != null, "`actions` or `actions.<list>`")
         from assign in Token.EqualTo(AplToken.Equal).Or(Token.EqualTo(AplToken.PlusEqual))
         from slash in Token.EqualTo(AplToken.Slash).OptionalOrDefault()
         select AplActionLine.ListOf(list.ToStringValue())!).Try();

    // Target selection: 'first', 'min' or 'max', then ':'.
    private static readonly TokenListParser<AplToken, AplTargetSelection> Selection =
        (from name in Token.Matching<AplToken>(kind => kind == AplToken.Identifier, "target selection")
             .Where(t => Enum.TryParse<AplTargetSelection>(t.ToStringValue(), ignoreCase: true, out _), "`first`, `min` or `max`")
         from colon in Token.EqualTo(AplToken.Colon)
         select Enum.Parse<AplTargetSelection>(name.ToStringValue(), ignoreCase: true)).Try();

    // Argument: ',' key '=' Expression, or ',' 'target_if' '=' [Selection] Expression. Only 'if' and 'target_if' have an effect.
    private static readonly TokenListParser<AplToken, (string Key, AplTargetSelection? Selection, IAplExpression Value)> Argument =
        from comma in Token.EqualTo(AplToken.Comma)
        from key in Token.EqualTo(AplToken.Identifier)
        from equal in Token.EqualTo(AplToken.Equal)
        from selection in key.ToStringValue().Equals(AplActionLine.TargetIfKey, StringComparison.OrdinalIgnoreCase)
            ? Selection.Select(s => (AplTargetSelection?)s).OptionalOrDefault()
            : Parse.Return<AplToken, AplTargetSelection?>(null)
        from value in Expression
        select (key.ToStringValue(), selection, value);

    private static readonly TokenListParser<AplToken, AplActionLine> ActionLine =
        (from list in ListPrefix.OptionalOrDefault(AplActionLine.DefaultList)
         from name in Token.EqualTo(AplToken.Identifier)
         from arguments in Argument.Many()
         select new AplActionLine(
             list,
             name.ToStringValue(),
             arguments.LastOrDefault(a => a.Key.Equals(AplActionLine.ConditionKey, StringComparison.OrdinalIgnoreCase)).Value,
             arguments.Where(a => a.Key.Equals(AplActionLine.TargetIfKey, StringComparison.OrdinalIgnoreCase))
                 .Select(a => new AplTargetIf(a.Selection ?? AplTargetSelection.First, a.Value))
                 .LastOrDefault(),
             arguments.Where(a => !a.Key.Equals(AplActionLine.ConditionKey, StringComparison.OrdinalIgnoreCase)
                                  && !a.Key.Equals(AplActionLine.TargetIfKey, StringComparison.OrdinalIgnoreCase))
                 .Select(a => new AplArgument(a.Key, a.Value))
                 .ToList())).AtEnd();

    /// <summary>The script's action lists. A list named by <c>run_action_list</c> or <c>call_action_list</c> has a line of its own.</summary>
    public static AplActionLists ParseScript(string script)
    {
        var lists = new Dictionary<string, List<AplAction>>();
        var listActions = new List<(int LineNumber, AplAction Action)>();
        using var reader = new StringReader(script);
        string? line;
        int lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            AplActionLine syntax;
            AplAction? action;
            try
            {
                syntax = ParseActionSyntax(trimmed);
                action = ToAction(syntax, trimmed);
            }
            catch (ParseException e)
            {
                throw new ParseException($"APL line {lineNumber}: {e.Message}", e.ErrorPosition, e);
            }

            if (action == null) continue;
            action.LineNumber = lineNumber;
            if (!lists.TryGetValue(syntax.List, out var actions))
            {
                lists[syntax.List] = actions = [];
            }
            actions.Add(action);
            if (action.ListAction != null) listActions.Add((lineNumber, action));
        }

        foreach (var (number, action) in listActions)
        {
            if (!lists.ContainsKey(action.ListName!))
            {
                throw new ParseException($"APL line {number}: no action list named `{action.ListName}`.", Position.Empty);
            }
        }
        return new AplActionLists(lists);
    }

    /// <summary>The action on one line, or null for an action in <see cref="SkippedActions"/>.</summary>
    public static AplAction? ParseActionLine(string line) => ToAction(ParseActionSyntax(line), line);

    private static AplAction? ToAction(AplActionLine syntax, string line)
    {
        if (SkippedActions.Contains(syntax.Name))
            return null;

        if (syntax.Name == AplVariable.ActionName)
        {
            return new AplAction { AbilityName = syntax.Name, Condition = syntax.Condition, Variable = ToVariable(syntax), RawLine = line };
        }

        if (syntax.Name == AplSwitchTarget.ActionName)
        {
            if (syntax.TargetIf == null)
            {
                throw new ParseException($"`{syntax.Name}` needs `{AplActionLine.TargetIfKey}=<expression>`.", Position.Empty);
            }
            return new AplAction { AbilityName = syntax.Name, Condition = syntax.Condition, TargetIf = syntax.TargetIf, SwitchTarget = true, RawLine = line };
        }

        var listAction = AplListActions.Parse(syntax.Name);
        if (listAction != null && syntax.ListName == null)
        {
            throw new ParseException($"`{syntax.Name}` needs `{AplActionLine.ListNameKey}=<list>`.", Position.Empty);
        }

        return new AplAction
        {
            AbilityName = syntax.Name,
            Condition = listAction == null ? OfAction(syntax.Condition, syntax.Name) : syntax.Condition,
            TargetIf = listAction == null && syntax.TargetIf is { } targetIf
                ? targetIf with { Expression = OfAction(targetIf.Expression, syntax.Name)! }
                : syntax.TargetIf,
            ListAction = listAction,
            ListName = listAction != null ? syntax.ListName : null,
            UseOffGcd = listAction == null && syntax.Flag(AplActionLine.UseOffGcdKey),
            UseWhileCasting = listAction == null && syntax.Flag(AplActionLine.UseWhileCastingKey),
            InterruptIf = listAction == null ? OfAction(syntax.Argument(AplActionLine.InterruptIfKey), syntax.Name) : null,
            InterruptImmediate = listAction == null && syntax.Flag(AplActionLine.InterruptImmediateKey),
            RawLine = line
        };
    }

    private static AplVariable ToVariable(AplActionLine syntax)
    {
        IAplExpression? Argument(string key) =>
            syntax.Arguments.LastOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

        if (Argument(AplVariable.NameKey) is not PropertyExpression name)
        {
            throw new ParseException($"`{AplVariable.ActionName}` needs `{AplVariable.NameKey}=<variable>`.", Position.Empty);
        }
        var op = AplVariableOp.Set;
        if (Argument(AplVariable.OpKey) is { } opArgument
            && !(opArgument is PropertyExpression opName && Enum.TryParse(opName.PropertyPath, ignoreCase: true, out op)))
        {
            throw new ParseException($"`{AplVariable.OpKey}=` is one of {string.Join(", ", Enum.GetNames<AplVariableOp>().Select(n => n.ToLowerInvariant()))}.", Position.Empty);
        }
        var value = Argument(AplVariable.ValueKey);
        if (value == null && op != AplVariableOp.Reset)
        {
            throw new ParseException($"`{AplVariable.ActionName}` needs `{AplVariable.ValueKey}=<expression>`.", Position.Empty);
        }
        double @default = Argument(AplVariable.DefaultKey) is ConstantExpression c ? c.Value : 0.0;
        return new AplVariable(name.PropertyPath, op, value, @default);
    }

    /// <summary>
    /// <paramref name="expression"/> with the paths an action reads about its own ability ("refreshable") written as the
    /// scoped paths they stand for (<see cref="PropertyExpression.OfAction"/>).
    /// </summary>
    private static IAplExpression? OfAction(IAplExpression? expression, string ability) => expression switch
    {
        PropertyExpression p when PropertyExpression.OfAction(p.PropertyPath, ability) is { } path => new PropertyExpression(path),
        GroupExpression g => new GroupExpression(OfAction(g.Inner, ability)!),
        UnaryOpExpression u => new UnaryOpExpression(u.Operator, OfAction(u.Operand, ability)!),
        BinaryOpExpression b => new BinaryOpExpression(OfAction(b.Left, ability)!, b.Operator, OfAction(b.Right, ability)!),
        FunctionExpression f => new FunctionExpression(f.Function, f.Arguments.Select(a => OfAction(a, ability)!).ToList()),
        _ => expression,
    };

    /// <summary>One action line as written, including the list name and the actions that control the list.</summary>
    public static AplActionLine ParseActionSyntax(string line) => ActionLine.Parse(Tokenizer.Tokenize(line));

    public static IAplExpression ParseExpression(string text) => Expression.AtEnd().Parse(Tokenizer.Tokenize(text));

    /// <summary>The tokens of <paramref name="text"/>, or the tokenizer's error (the text has a character no token starts with).</summary>
    public static Result<TokenList<AplToken>> Tokenize(string text) => Tokenizer.TryTokenize(text);
}
