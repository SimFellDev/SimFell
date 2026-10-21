using Superpower.Display;

namespace FellowshipSim.Core.APL;

/// <summary>Kind of a lexical token in an APL action line.</summary>
public enum AplToken
{
    [Token(Example = "(")] LParen,
    [Token(Example = ")")] RParen,
    [Token(Example = "&")] And, // also the word 'and'
    [Token(Example = "|")] Or, // also the word 'or'
    [Token(Example = "!")] Not, // also the word 'not'
    [Token(Example = "-")] Minus,
    [Token(Example = "+")] Plus,
    [Token(Example = "*")] Star,
    [Token(Example = "%")] Percent,
    [Token(Example = "%%")] DoublePercent,
    [Token(Example = "=")] Equal,
    [Token(Example = "!=")] NotEqual,
    [Token(Example = "<")] Less,
    [Token(Example = "<=")] LessEqual,
    [Token(Example = ">")] Greater,
    [Token(Example = ">=")] GreaterEqual,
    [Token(Example = "+=")] PlusEqual,
    [Token(Example = "/")] Slash,
    [Token(Example = ",")] Comma,
    [Token(Example = ":")] Colon,
    [Token(Category = "number")] Number,
    [Token(Category = "identifier")] Identifier,
}
