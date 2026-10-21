using Superpower.Display;

namespace FellowshipSim.Core.APL;

/// <summary>Kind of a lexical token in an APL action line.</summary>
public enum AplToken
{
    [Token(Example = "(")] LParen,
    [Token(Example = ")")] RParen,
    [Token(Example = "&")] And,
    [Token(Example = "|")] Or,
    [Token(Example = "!")] Not,
    [Token(Example = "-")] Minus,
    [Token(Example = "=")] Equal,
    [Token(Example = "!=")] NotEqual,
    [Token(Example = "<")] Less,
    [Token(Example = "<=")] LessEqual,
    [Token(Example = ">")] Greater,
    [Token(Example = ">=")] GreaterEqual,
    [Token(Example = "+=")] PlusEqual,
    [Token(Example = "/")] Slash,
    [Token(Example = ",")] Comma,
    [Token(Category = "number")] Number,
    [Token(Category = "identifier")] Identifier,
}
