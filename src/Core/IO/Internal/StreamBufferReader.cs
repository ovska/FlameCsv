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

    protected override bool TryResetCore()
    {
        if (_stream.CanSeek)
        {
            _stream.Position = 0;
            return true;
        }

        return false;
    }

    protected override int ReadCore(Span<byte> buffer)
    {
        int result = _stream.Read(buffer);

        return result;
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
