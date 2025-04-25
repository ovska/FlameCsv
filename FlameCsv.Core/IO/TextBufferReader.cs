using System.Buffers;

namespace FlameCsv.IO;

internal sealed class TextBufferReader : CsvBufferReader<char>
{
    private readonly TextReader _reader;
    private readonly bool _leaveOpen;

    public TextBufferReader(
        TextReader reader,
        MemoryPool<char> pool,
        in CsvIOOptions options) : base(pool, in options)
    {
        _reader = reader;
        _leaveOpen = options.LeaveOpen;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return ReferenceEquals(_reader, TextReader.Null);
    }

    protected override int ReadCore(Memory<char> buffer)
    {
        return _reader.Read(buffer.Span);
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
