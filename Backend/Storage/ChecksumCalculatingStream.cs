using System.Security.Cryptography;

namespace Storage;

public class ChecksumCalculatingStream(Stream innerStream) : Stream
{
    private readonly Stream _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
    private readonly SHA256 _sha256 = SHA256.Create();
    private bool _disposed;

    public string GetChecksum()
    {
        _sha256.TransformFinalBlock([], 0, 0);
        var hash = _sha256.Hash ?? [];
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _innerStream.Read(buffer, offset, count);
        if (bytesRead > 0)
        {
            _sha256.TransformBlock(buffer, offset, bytesRead, buffer, offset);
        }

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        if (bytesRead > 0)
        {
            _sha256.TransformBlock(buffer, offset, bytesRead, buffer, offset);
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("Seeking is not supported on checksum calculating stream");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        _innerStream.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _sha256?.Dispose();
            _innerStream?.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}