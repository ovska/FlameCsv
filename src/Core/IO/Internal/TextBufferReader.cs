namespace FlameCsv.IO.Internal;

internal sealed class TextBufferReader : CsvBufferReader<char>
{
    private readonly TextReader _reader;
    private readonly bool _leaveOpen;

    public TextBufferReader(TextReader reader, in CsvIOOptions options)
        : base(in options)
    {
        _reader = reader;
        _leaveOpen = options.LeaveOpen;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (ReferenceEquals(_reader, TextReader.Null))
        {
            Position = 0;
            return true;
        }

        return false;
    }

    protected override int ReadCore(Span<char> buffer)
    {
        return _reader.Read(buffer);
    }

    protected override ValueTask<int> ReadAsyncCore(Memory<char> buffer, CancellationToken cancellationToken)
    {
        return _reader.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        if (!_leaveOpen)
        {
            _reader.Dispose();
        }
    }
}
