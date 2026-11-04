using System.Buffers;

namespace FellowshipSim.Engine.Replay;

/// <summary>
/// A part of a replay script as UTF-8, in chunks of growing size up to 1 MB. A chunk is never copied to a larger one, so
/// the part allocates its own length plus at most one chunk.
/// </summary>
internal sealed class ScriptBuffer(int firstChunk = 4096) : IBufferWriter<byte>
{
    private const int MaxChunk = 1 << 20;

    private readonly List<byte[]> _chunks = [];
    // The bytes before each chunk, and the bytes written to each chunk.
    private readonly List<long> _starts = [];
    private readonly List<int> _lengths = [];

    public long Length { get; private set; }

    public void Advance(int count)
    {
        _lengths[^1] += count;
        Length += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0) => Room(sizeHint).AsMemory(_lengths[^1]);

    public Span<byte> GetSpan(int sizeHint = 0) => Room(sizeHint).AsSpan(_lengths[^1]);

    // The last chunk, with at least sizeHint bytes free.
    private byte[] Room(int sizeHint)
    {
        sizeHint = Math.Max(sizeHint, 1);
        if (_chunks.Count > 0 && _chunks[^1].Length - _lengths[^1] >= sizeHint) return _chunks[^1];
        int size = _chunks.Count == 0 ? firstChunk : Math.Min(_chunks[^1].Length * 2, MaxChunk);
        var chunk = new byte[Math.Max(size, sizeHint)];
        _chunks.Add(chunk);
        _starts.Add(Length);
        _lengths.Add(0);
        return chunk;
    }

    /// <summary>Copies the bytes from <paramref name="start"/> to fill <paramref name="destination"/>.</summary>
    public void CopyTo(long start, Span<byte> destination)
    {
        int i = _starts.BinarySearch(start);
        if (i < 0) i = ~i - 1;
        while (destination.Length > 0)
        {
            int from = (int)(start - _starts[i]);
            int count = Math.Min(_lengths[i] - from, destination.Length);
            _chunks[i].AsSpan(from, count).CopyTo(destination);
            destination = destination[count..];
            start += count;
            i++;
        }
    }

    /// <summary>Copies every byte to the start of <paramref name="destination"/>.</summary>
    public void CopyTo(Span<byte> destination)
    {
        for (int i = 0; i < _chunks.Count; i++)
        {
            _chunks[i].AsSpan(0, _lengths[i]).CopyTo(destination);
            destination = destination[_lengths[i]..];
        }
    }
}
