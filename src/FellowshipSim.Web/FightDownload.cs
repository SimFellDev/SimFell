namespace FellowshipSim.Web;

/// <summary>
/// A downloaded fight's UTF-8 JSON as it arrives, in chunks of 1 MB. A <see cref="MemoryStream"/> doubles its buffer as it
/// grows, copying each one into the next and ending with up to twice the fight's size. These chunks are copied once, into
/// an array of the fight's exact size (<see cref="ToArray"/>).
/// </summary>
internal sealed class FightDownload : Stream
{
    private const int ChunkSize = 1 << 20;

    private readonly List<byte[]> _chunks = [];
    private int _used = ChunkSize;
    private long _length;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _length;

    public override long Position
    {
        get => _length;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        while (buffer.Length > 0)
        {
            if (_used == ChunkSize)
            {
                _chunks.Add(new byte[ChunkSize]);
                _used = 0;
            }
            int count = Math.Min(ChunkSize - _used, buffer.Length);
            buffer[..count].CopyTo(_chunks[^1].AsSpan(_used));
            _used += count;
            _length += count;
            buffer = buffer[count..];
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    /// <summary>The fight's bytes in one array, and the chunks released.</summary>
    public byte[] ToArray()
    {
        var bytes = new byte[_length];
        var rest = bytes.AsSpan();
        foreach (var chunk in _chunks)
        {
            int count = Math.Min(chunk.Length, rest.Length);
            chunk.AsSpan(0, count).CopyTo(rest);
            rest = rest[count..];
        }
        _chunks.Clear();
        return bytes;
    }

    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
