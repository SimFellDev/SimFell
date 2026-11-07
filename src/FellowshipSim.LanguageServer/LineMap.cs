namespace FellowshipSim.LanguageServer;

/// <summary>
/// The start of each line of a text, for the protocol's positions: a line index and a UTF-16 character in that line, the
/// units of a C# string.
/// </summary>
public sealed class LineMap
{
    private readonly List<int> _starts = [0];
    private readonly int _length;

    public LineMap(string text)
    {
        _length = text.Length;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') _starts.Add(i + 1);
        }
    }

    public (int Line, int Character) PositionOf(int offset)
    {
        offset = Math.Clamp(offset, 0, _length);
        int line = _starts.BinarySearch(offset);
        if (line < 0) line = ~line - 1;
        return (line, offset - _starts[line]);
    }

    public int OffsetOf(int line, int character) =>
        line >= _starts.Count ? _length : Math.Min(_starts[line] + character, line + 1 < _starts.Count ? _starts[line + 1] - 1 : _length);
}
