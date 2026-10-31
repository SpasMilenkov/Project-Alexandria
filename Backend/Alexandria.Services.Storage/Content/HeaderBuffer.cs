namespace Alexandria.Services.Storage.Content;

/// <summary>
/// Captures the first <c>capacity</c> bytes of a stream that is read in arbitrary chunk sizes.
/// Short reads accumulate across calls until the capacity is reached.
/// </summary>
internal sealed class HeaderBuffer
{
    private readonly byte[] _buffer;
    private int _length;

    public HeaderBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _buffer = new byte[capacity];
    }

    /// <summary>Copies at most the remaining capacity from <paramref name="chunk"/>.</summary>
    public void Append(ReadOnlySpan<byte> chunk)
    {
        var remaining = _buffer.Length - _length;
        if (remaining <= 0 || chunk.IsEmpty) return;

        var take = Math.Min(remaining, chunk.Length);
        chunk[..take].CopyTo(_buffer.AsSpan(_length));
        _length += take;
    }

    /// <summary>Exactly the bytes captured so far.</summary>
    public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();
}
