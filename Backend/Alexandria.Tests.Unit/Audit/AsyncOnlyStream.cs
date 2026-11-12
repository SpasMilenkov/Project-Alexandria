namespace Alexandria.Tests.Unit.Audit;

/// <summary>
/// A response body that, like Kestrel's, refuses blocking writes, so a writer that falls back
/// to synchronous I/O fails the test instead of a real download.
/// </summary>
internal sealed class AsyncOnlyStream : Stream
{
    private readonly MemoryStream _inner = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public byte[] ToArray() => _inner.ToArray();

    public override void Flush() => throw new InvalidOperationException("Synchronous operations are disallowed.");

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException("Synchronous operations are disallowed.");

    public override void Write(ReadOnlySpan<byte> buffer) =>
        throw new InvalidOperationException("Synchronous operations are disallowed.");

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _inner.Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _inner.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
