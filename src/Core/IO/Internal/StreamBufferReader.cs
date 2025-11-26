namespace FlameCsv.IO.Internal;

internal sealed class StreamBufferReader : CsvBufferReader<byte>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public StreamBufferReader(Stream stream, in CsvIOOptions options)
        : base(in options)
    {
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_stream.CanSeek)
        {
            _stream.Seek(0, SeekOrigin.Begin);
            Position = 0;
            return true;
        }

        return false;
    }

    protected override int ReadCore(Span<byte> buffer)
    {
        return _stream.Read(buffer);
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _stream.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}
