using System.Buffers;

namespace FlameCsv.IO;

internal sealed class TextPipeReader : CsvPipeReader<char>
{
    private readonly TextReader _reader;

    public TextPipeReader(
        TextReader reader,
        MemoryPool<char> memoryPool,
        in CsvReaderOptions options)
        : base(memoryPool, in options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    protected override int ReadFromInner(Span<char> buffer)
    {
        return _reader.Read(buffer);
    }

    protected override ValueTask<int> ReadFromInnerAsync(Memory<char> buffer, CancellationToken cancellationToken)
    {
        return _reader.ReadAsync(buffer, cancellationToken);
    }

    protected override void DisposeCore()
    {
        if (!LeaveOpen)
        {
            _reader.Dispose();
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        DisposeCore();
        return default;
    }
}
